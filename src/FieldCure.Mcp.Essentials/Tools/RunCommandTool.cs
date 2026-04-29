using ModelContextProtocol.Server;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace FieldCure.Mcp.Essentials.Tools;

/// <summary>
/// MCP tool that executes shell commands and returns stdout, stderr, and exit code.
/// </summary>
[McpServerToolType]
public static class RunCommandTool
{
    /// <summary>
    /// Default maximum characters to return per output stream.
    /// </summary>
    const int DefaultMaxOutputChars = 100_000;

    /// <summary>
    /// Maximum time to wait for the shell-availability probe process before declaring it unavailable.
    /// </summary>
    const int ShellProbeTimeoutMs = 5_000;

    const string PwshExecutable = "pwsh";
    const string WindowsPowerShellExecutable = "powershell";
    const string CmdExecutable = "cmd.exe";
    const string BashExecutable = "bash";
    const string ShExecutable = "sh";

    /// <summary>
    /// Caches successful per-process shell-availability probes. Failures are not cached so a transient
    /// probe miss (cold-start, AV scan) does not poison the rest of the process lifetime.
    /// </summary>
    static readonly ConcurrentDictionary<string, bool> ShellAvailabilityCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Executes a shell command and returns the result as JSON.
    /// </summary>
    /// <param name="command">The shell command text to execute.</param>
    /// <param name="working_directory">
    /// Optional working directory. Defaults to the current user's home directory when omitted.
    /// </param>
    /// <param name="timeout_seconds">Command timeout in seconds, clamped to the supported range.</param>
    /// <param name="environment">Optional JSON object containing environment variable overrides.</param>
    /// <param name="shell">
    /// Shell selector. Use <c>auto</c> for the host default, or an explicit shell name
    /// such as <c>pwsh</c>, <c>powershell</c>, <c>cmd</c>, <c>bash</c>, or <c>sh</c>.
    /// </param>
    /// <param name="max_output_chars">Maximum visible characters to return for stdout and stderr independently.</param>
    /// <param name="cancellationToken">Cancellation token for the MCP tool invocation.</param>
    /// <returns>A JSON string containing process output, exit status, timeout status, shell used, and truncation flags.</returns>
    [McpServerTool(Name = "run_command", Destructive = true)]
    [Description("Execute shell commands. Returns stdout, stderr, exit code, shell_used, and truncation flags. "
        + "Use shell='pwsh' for PowerShell Core commands, shell='powershell' on Windows when pwsh is unavailable, "
        + "or shell='cmd'/'bash'/'sh' when that syntax is required. "
        + "For verbose commands, redirect to a file and read selectively, or pass max_output_chars to cap the response. "
        + "Output is truncated by default; stdout_truncated and stderr_truncated report incomplete streams.")]
    public static async Task<string> RunCommand(
        [Description("Shell command to execute")]
        string command,
        [Description("Working directory (default: user home)")]
        string? working_directory = null,
        [Description("Timeout in seconds (default: 30, max: 300)")]
        int timeout_seconds = 30,
        [Description("Additional environment variables as JSON object, e.g. {\"KEY\": \"value\"}")]
        string? environment = null,
        [Description("Shell to execute the command in. Options: auto (default: cmd.exe on Windows, /bin/sh on Unix), "
            + "pwsh (PowerShell Core), powershell (Windows PowerShell), cmd, bash, sh. Explicit shells fail if unavailable.")]
        string? shell = "auto",
        [Description("Maximum characters to return per output stream (stdout and stderr independently). "
            + "Default: 100000. Truncated streams include a marker and set stdout_truncated/stderr_truncated.")]
        int? max_output_chars = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            timeout_seconds = Math.Clamp(timeout_seconds, 1, 300);

            var maxOutputChars = max_output_chars ?? DefaultMaxOutputChars;
            if (maxOutputChars <= 0)
                return JsonSerializer.Serialize(new { error = "max_output_chars must be greater than 0." }, McpJson.Options);

            var workDir = working_directory ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            if (!Directory.Exists(workDir))
                return JsonSerializer.Serialize(new { error = $"Working directory not found: {workDir}" }, McpJson.Options);

            var resolvedShell = ResolveShell(shell, command);
            var psi = new ProcessStartInfo
            {
                FileName = resolvedShell.FileName,
                WorkingDirectory = workDir,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            // cmd.exe does not parse its argv with CommandLineToArgvW conventions, so .NET's
            // ArgumentList escaping leaks backslashes into commands containing embedded quotes.
            // Cmd-bound shells therefore use the raw Arguments string; everything else uses
            // ArgumentList for proper execve-style argument passing.
            if (resolvedShell.RawArguments is not null)
                psi.Arguments = resolvedShell.RawArguments;
            else
                foreach (var argument in resolvedShell.ArgumentList)
                    psi.ArgumentList.Add(argument);

            if (environment is not null)
            {
                try
                {
                    var envVars = JsonSerializer.Deserialize<Dictionary<string, string>>(environment);
                    if (envVars is not null)
                    {
                        foreach (var (key, value) in envVars)
                            psi.EnvironmentVariables[key] = value;
                    }
                }
                catch (JsonException)
                {
                    return JsonSerializer.Serialize(new { error = "Invalid environment JSON." }, McpJson.Options);
                }
            }

            using var process = new Process { StartInfo = psi };
            process.Start();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(timeout_seconds));

            var stdoutTask = ReadStreamLimited(process.StandardOutput, maxOutputChars, cts.Token);
            var stderrTask = ReadStreamLimited(process.StandardError, maxOutputChars, cts.Token);

            var timedOut = false;
            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                timedOut = true;
                try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
            }

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            var result = new
            {
                ExitCode = timedOut ? -1 : process.ExitCode,
                Stdout = stdout.Text,
                Stderr = stderr.Text,
                TimedOut = timedOut,
                StdoutTruncated = stdout.Truncated,
                StderrTruncated = stderr.Truncated,
                ShellUsed = resolvedShell.ShellUsed,
            };

            return JsonSerializer.Serialize(result, McpJson.Options);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, McpJson.Options);
        }
    }

    /// <summary>
    /// Reads from a stream up to a maximum visible character count, then drains
    /// the rest so verbose child processes cannot block on a full pipe.
    /// </summary>
    /// <param name="reader">The redirected process stream to read.</param>
    /// <param name="maxChars">Maximum visible characters to retain before appending a truncation marker.</param>
    /// <param name="ct">Cancellation token used to stop reading on timeout or caller cancellation.</param>
    /// <returns>The captured output text and whether any characters were omitted.</returns>
    static async Task<CapturedOutput> ReadStreamLimited(StreamReader reader, int maxChars, CancellationToken ct)
    {
        var sb = new StringBuilder();
        var buffer = new char[4096];
        var capturedChars = 0;
        var omittedChars = 0;

        try
        {
            int read;
            while ((read = await reader.ReadAsync(buffer, ct)) > 0)
            {
                var remaining = maxChars - capturedChars;
                if (remaining > 0)
                {
                    var take = Math.Min(read, remaining);
                    sb.Append(buffer, 0, take);
                    capturedChars += take;
                    omittedChars += read - take;
                }
                else
                {
                    omittedChars += read;
                }
            }
        }
        catch (OperationCanceledException) { /* timeout: return what we have */ }

        var truncated = omittedChars > 0;
        if (truncated)
        {
            // Avoid ending captured text with half of a UTF-16 surrogate pair.
            if (sb.Length > 0 && char.IsHighSurrogate(sb[^1]))
            {
                sb.Length--;
                omittedChars++;
            }

            sb.Append("\n\n[Truncated: ");
            sb.Append(omittedChars.ToString("N0"));
            sb.Append(" more chars omitted. Use a smaller max_output_chars or narrow the command.]");
        }

        return new CapturedOutput(sb.ToString(), truncated);
    }

    /// <summary>
    /// Resolves the caller-provided shell option into a process executable,
    /// argument list, and response-facing shell name.
    /// </summary>
    /// <param name="shellOption">The requested shell option, or <c>null</c>/<c>auto</c> for the platform default.</param>
    /// <param name="command">The command text to run through the selected shell.</param>
    /// <returns>The resolved shell launch configuration.</returns>
    static ResolvedShell ResolveShell(string? shellOption, string command)
    {
        var normalized = string.IsNullOrWhiteSpace(shellOption)
            ? "auto"
            : shellOption.Trim().ToLowerInvariant();

        return normalized switch
        {
            "auto" => ResolveAuto(command),
            "pwsh" => ResolvePowerShellCore(command),
            "powershell" => ResolveWindowsPowerShell(command),
            "cmd" => ResolveCmd(command),
            "bash" => ResolveBash(command),
            "sh" => ResolveSh(command),
            _ => throw new ArgumentException(
                $"Unknown shell '{shellOption}'. Valid options: auto, pwsh, powershell, cmd, bash, sh."),
        };
    }

    /// <summary>
    /// Resolves the backward-compatible host default shell.
    /// </summary>
    /// <param name="command">The command text to run.</param>
    /// <returns><c>cmd.exe</c> on Windows, otherwise <c>/bin/sh</c>.</returns>
    static ResolvedShell ResolveAuto(string command)
    {
        return OperatingSystem.IsWindows()
            ? CmdShell(command)
            : new ResolvedShell("/bin/sh", ["-c", command], null, "sh");
    }

    /// <summary>
    /// Resolves PowerShell Core (<c>pwsh</c>) and fails when it is unavailable.
    /// </summary>
    /// <param name="command">The command text to encode for PowerShell.</param>
    /// <returns>The PowerShell Core launch configuration.</returns>
    static ResolvedShell ResolvePowerShellCore(string command)
    {
        if (!IsShellAvailable(PwshExecutable))
        {
            throw new InvalidOperationException(
                "Requested shell 'pwsh' is not available on this host. " +
                "Install PowerShell Core or use shell: 'auto', 'powershell', or 'cmd'.");
        }

        return new ResolvedShell(PwshExecutable, PowerShellArguments(command), null, "pwsh");
    }

    /// <summary>
    /// Resolves Windows PowerShell (<c>powershell.exe</c>) and fails when it is unavailable.
    /// </summary>
    /// <param name="command">The command text to encode for Windows PowerShell.</param>
    /// <returns>The Windows PowerShell launch configuration.</returns>
    static ResolvedShell ResolveWindowsPowerShell(string command)
    {
        if (!OperatingSystem.IsWindows())
            throw new InvalidOperationException("Requested shell 'powershell' is only available on Windows hosts.");

        if (!IsShellAvailable(WindowsPowerShellExecutable))
        {
            throw new InvalidOperationException(
                "Requested shell 'powershell' is not available on this host. " +
                "Install Windows PowerShell or use shell: 'auto' / 'cmd'.");
        }

        return new ResolvedShell(WindowsPowerShellExecutable, PowerShellArguments(command), null, "powershell");
    }

    /// <summary>
    /// Resolves Windows <c>cmd.exe</c> and fails on non-Windows hosts.
    /// </summary>
    /// <param name="command">The command text to run through <c>cmd.exe /c</c>.</param>
    /// <returns>The cmd launch configuration.</returns>
    static ResolvedShell ResolveCmd(string command)
    {
        if (!OperatingSystem.IsWindows())
            throw new InvalidOperationException("Requested shell 'cmd' is only available on Windows hosts.");

        return CmdShell(command);
    }

    /// <summary>
    /// Builds the cmd.exe launch configuration using the raw <see cref="ProcessStartInfo.Arguments"/>
    /// path. cmd parses <c>/c</c> with its own quote-stripping rules rather than CommandLineToArgvW,
    /// so .NET's ArgumentList escaping would leak backslashes into commands containing embedded quotes.
    /// </summary>
    static ResolvedShell CmdShell(string command)
        => new(CmdExecutable, [], $"/c {command}", "cmd");

    /// <summary>
    /// Resolves Bash either from <c>/bin/bash</c> on Unix-like hosts or from PATH on Windows.
    /// </summary>
    /// <param name="command">The command text to run through <c>bash -c</c>.</param>
    /// <returns>The Bash launch configuration.</returns>
    static ResolvedShell ResolveBash(string command)
    {
        if (OperatingSystem.IsWindows())
        {
            if (!IsShellAvailable(BashExecutable))
                throw new InvalidOperationException("Requested shell 'bash' is not available on this host.");

            return new ResolvedShell(BashExecutable, ["-c", command], null, "bash");
        }

        if (File.Exists("/bin/bash"))
            return new ResolvedShell("/bin/bash", ["-c", command], null, "bash");

        if (!IsShellAvailable(BashExecutable))
            throw new InvalidOperationException("Requested shell 'bash' is not available on this host.");

        return new ResolvedShell(BashExecutable, ["-c", command], null, "bash");
    }

    /// <summary>
    /// Resolves POSIX <c>sh</c> either from <c>/bin/sh</c> on Unix-like hosts or from PATH on Windows.
    /// </summary>
    /// <param name="command">The command text to run through <c>sh -c</c>.</param>
    /// <returns>The sh launch configuration.</returns>
    static ResolvedShell ResolveSh(string command)
    {
        if (!OperatingSystem.IsWindows())
        {
            if (!File.Exists("/bin/sh"))
                throw new InvalidOperationException("Requested shell 'sh' is not available on this host.");

            return new ResolvedShell("/bin/sh", ["-c", command], null, "sh");
        }

        if (!IsShellAvailable(ShExecutable))
            throw new InvalidOperationException("Requested shell 'sh' is not available on this host.");

        return new ResolvedShell(ShExecutable, ["-c", command], null, "sh");
    }

    /// <summary>
    /// Forces UTF-8 stdout/pipe encoding so non-ASCII output (Korean, emoji, etc.) round-trips
    /// through the parent's UTF-8 StandardOutputEncoding. Windows PowerShell 5.1 defaults to the
    /// system OEM codepage (CP949 on Korean Windows), which would otherwise produce mojibake.
    /// pwsh 7+ already defaults to UTF-8; the reassignment is idempotent there.
    /// </summary>
    const string PowerShellEncodingPrelude =
        "$OutputEncoding=[Console]::OutputEncoding=[System.Text.Encoding]::UTF8;";

    /// <summary>
    /// Builds PowerShell arguments using <c>-EncodedCommand</c> to avoid shell quoting hazards.
    /// </summary>
    /// <param name="command">The command text to encode as UTF-16LE Base64.</param>
    /// <returns>The PowerShell argument list.</returns>
    static string[] PowerShellArguments(string command)
    {
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(PowerShellEncodingPrelude + command));
        return ["-NoProfile", "-NonInteractive", "-EncodedCommand", encoded];
    }

    /// <summary>
    /// Returns whether a shell executable appears runnable. Successful probes are cached for the
    /// process lifetime; failures are re-probed on the next call so a transient miss (cold-start,
    /// AV scan) does not poison subsequent invocations.
    /// </summary>
    /// <param name="fileName">Shell executable name or path to probe.</param>
    /// <returns><see langword="true"/> when the shell probe exits successfully.</returns>
    static bool IsShellAvailable(string fileName)
    {
        if (ShellAvailabilityCache.TryGetValue(fileName, out var cached))
            return cached;

        var available = ProbeShellAvailable(fileName);
        if (available)
            ShellAvailabilityCache.TryAdd(fileName, true);
        return available;
    }

    /// <summary>
    /// Probes a shell executable with a short no-op command.
    /// </summary>
    /// <param name="fileName">Shell executable name or path to probe.</param>
    /// <returns><see langword="true"/> when the shell starts and exits with code 0.</returns>
    static bool ProbeShellAvailable(string fileName)
    {
        try
        {
            var probe = new ProcessStartInfo
            {
                FileName = fileName,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            string[] args = fileName.Equals(PwshExecutable, StringComparison.OrdinalIgnoreCase)
                || fileName.Equals(WindowsPowerShellExecutable, StringComparison.OrdinalIgnoreCase)
                ? ["-NoProfile", "-NonInteractive", "-Command", "exit"]
                : ["-c", "exit"];

            foreach (var arg in args)
                probe.ArgumentList.Add(arg);

            using var process = Process.Start(probe);
            if (process is null) return false;

            if (!process.WaitForExit(ShellProbeTimeoutMs))
            {
                try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
                return false;
            }

            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Process launch details for a resolved shell.
    /// </summary>
    /// <param name="FileName">Executable file name or path.</param>
    /// <param name="ArgumentList">Arguments to pass via <see cref="ProcessStartInfo.ArgumentList"/>; ignored when <paramref name="RawArguments"/> is non-null.</param>
    /// <param name="RawArguments">When non-null, assigned verbatim to <see cref="ProcessStartInfo.Arguments"/>; required for cmd.exe to bypass CommandLineToArgvW-style escaping.</param>
    /// <param name="ShellUsed">Stable shell name returned to the caller.</param>
    readonly record struct ResolvedShell(string FileName, string[] ArgumentList, string? RawArguments, string ShellUsed);

    /// <summary>
    /// Captured output from one redirected process stream.
    /// </summary>
    /// <param name="Text">Visible stream text, including a truncation marker when applicable.</param>
    /// <param name="Truncated">Whether characters were omitted from the visible text.</param>
    readonly record struct CapturedOutput(string Text, bool Truncated);
}
