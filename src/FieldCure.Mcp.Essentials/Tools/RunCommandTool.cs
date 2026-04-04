using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;

namespace FieldCure.Mcp.Essentials.Tools;

/// <summary>
/// MCP tool that executes shell commands and returns stdout, stderr, and exit code.
/// </summary>
[McpServerToolType]
public static class RunCommandTool
{
    /// <summary>
    /// JSON serialization options shared across all responses.
    /// </summary>
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Maximum bytes to capture per output stream (100 KB).
    /// </summary>
    const int MaxOutputBytes = 102_400;

    /// <summary>
    /// Executes a shell command and returns the result as JSON.
    /// </summary>
    [McpServerTool(Name = "run_command", Destructive = true)]
    [Description("Execute shell commands. Returns stdout, stderr, and exit code. Use for system operations, scripts, and CLI tools.")]
    public static async Task<string> RunCommand(
        [Description("Shell command to execute")]
        string command,
        [Description("Working directory (default: user home)")]
        string? working_directory = null,
        [Description("Timeout in seconds (default: 30, max: 300)")]
        int timeout_seconds = 30,
        [Description("Additional environment variables as JSON object, e.g. {\"KEY\": \"value\"}")]
        string? environment = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            timeout_seconds = Math.Clamp(timeout_seconds, 1, 300);
            var workDir = working_directory ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            if (!Directory.Exists(workDir))
                return JsonSerializer.Serialize(new { error = $"Working directory not found: {workDir}" }, JsonOptions);

            var isWindows = OperatingSystem.IsWindows();
            var psi = new ProcessStartInfo
            {
                FileName = isWindows ? "cmd.exe" : "/bin/sh",
                Arguments = isWindows ? $"/c {command}" : $"-c \"{command.Replace("\"", "\\\"")}\"",
                WorkingDirectory = workDir,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

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
                    return JsonSerializer.Serialize(new { error = "Invalid environment JSON." }, JsonOptions);
                }
            }

            using var process = new Process { StartInfo = psi };
            process.Start();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(timeout_seconds));

            var stdoutTask = ReadStreamLimited(process.StandardOutput, MaxOutputBytes, cts.Token);
            var stderrTask = ReadStreamLimited(process.StandardError, MaxOutputBytes, cts.Token);

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
                Stdout = stdout,
                Stderr = stderr,
                TimedOut = timedOut,
            };

            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    /// <summary>
    /// Reads from a stream up to a maximum byte count.
    /// </summary>
    static async Task<string> ReadStreamLimited(StreamReader reader, int maxBytes, CancellationToken ct)
    {
        var sb = new StringBuilder();
        var buffer = new char[4096];
        var totalChars = 0;
        var maxChars = maxBytes / 2; // conservative estimate

        try
        {
            int read;
            while ((read = await reader.ReadAsync(buffer, ct)) > 0)
            {
                var remaining = maxChars - totalChars;
                if (remaining <= 0) break;
                sb.Append(buffer, 0, Math.Min(read, remaining));
                totalChars += read;
            }
        }
        catch (OperationCanceledException) { /* timeout — return what we have */ }

        return sb.ToString();
    }
}
