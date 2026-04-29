using System.Text.Json;
using FieldCure.Mcp.Essentials.Tools;

namespace FieldCure.Mcp.Essentials.Tests;

[TestClass]
public class RunCommandToolTests
{
    [TestMethod]
    public async Task EchoReturnsStdout()
    {
        var json = await RunCommandTool.RunCommand("echo hello");
        using var doc = JsonDocument.Parse(json);
        Assert.AreEqual(0, doc.RootElement.GetProperty("exit_code").GetInt32());
        Assert.IsTrue(doc.RootElement.GetProperty("stdout").GetString()!.Contains("hello"));
        Assert.IsFalse(doc.RootElement.GetProperty("timed_out").GetBoolean());
    }

    [TestMethod]
    public async Task AutoReportsResolvedShell()
    {
        var json = await RunCommandTool.RunCommand("echo hello");
        using var doc = JsonDocument.Parse(json);

        var expected = OperatingSystem.IsWindows() ? "cmd" : "sh";
        Assert.AreEqual(expected, doc.RootElement.GetProperty("shell_used").GetString());
        Assert.IsFalse(doc.RootElement.GetProperty("stdout_truncated").GetBoolean());
        Assert.IsFalse(doc.RootElement.GetProperty("stderr_truncated").GetBoolean());
    }

    [TestMethod]
    public async Task BlankShellUsesAuto()
    {
        var json = await RunCommandTool.RunCommand("echo hello", shell: "   ");
        using var doc = JsonDocument.Parse(json);

        var expected = OperatingSystem.IsWindows() ? "cmd" : "sh";
        Assert.AreEqual(expected, doc.RootElement.GetProperty("shell_used").GetString());
        Assert.AreEqual(0, doc.RootElement.GetProperty("exit_code").GetInt32());
    }

    [TestMethod]
    public async Task NonZeroExitCode()
    {
        var command = OperatingSystem.IsWindows() ? "exit /b 42" : "exit 42";
        var json = await RunCommandTool.RunCommand(command);
        using var doc = JsonDocument.Parse(json);
        Assert.AreEqual(42, doc.RootElement.GetProperty("exit_code").GetInt32());
    }

    [TestMethod]
    public async Task TimeoutKillsProcess()
    {
        var command = OperatingSystem.IsWindows()
            ? "ping -n 60 127.0.0.1"
            : "sleep 60";

        var json = await RunCommandTool.RunCommand(command, timeout_seconds: 2);
        using var doc = JsonDocument.Parse(json);
        Assert.IsTrue(doc.RootElement.GetProperty("timed_out").GetBoolean());
    }

    [TestMethod]
    public async Task TimeoutReturnsPartialOutput()
    {
        var command = OperatingSystem.IsWindows()
            ? "echo before_timeout & ping -n 60 127.0.0.1 > nul"
            : "echo before_timeout; sleep 60";

        var json = await RunCommandTool.RunCommand(command, timeout_seconds: 2);
        using var doc = JsonDocument.Parse(json);

        Assert.IsTrue(doc.RootElement.GetProperty("timed_out").GetBoolean());
        Assert.AreEqual(-1, doc.RootElement.GetProperty("exit_code").GetInt32());
        Assert.IsTrue(doc.RootElement.GetProperty("stdout").GetString()!.Contains("before_timeout"));
    }

    [TestMethod]
    public async Task WorkingDirectory()
    {
        var tempDir = Path.GetTempPath();
        var command = OperatingSystem.IsWindows() ? "cd" : "pwd";

        var json = await RunCommandTool.RunCommand(command, working_directory: tempDir);
        using var doc = JsonDocument.Parse(json);
        Assert.AreEqual(0, doc.RootElement.GetProperty("exit_code").GetInt32());

        var stdout = doc.RootElement.GetProperty("stdout").GetString()!.Trim();
        Assert.IsTrue(
            stdout.Contains(tempDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
            $"Expected temp path in stdout, got: {stdout}");
    }

    [TestMethod]
    public async Task InvalidWorkingDirectory()
    {
        var invalidPath = OperatingSystem.IsWindows()
            ? @"C:\nonexistent_dir_12345"
            : "/tmp/nonexistent_dir_12345";

        var json = await RunCommandTool.RunCommand("echo hi", working_directory: invalidPath);
        using var doc = JsonDocument.Parse(json);
        Assert.IsTrue(doc.RootElement.TryGetProperty("error", out _));
    }

    [TestMethod]
    public async Task EnvironmentVariables()
    {
        var command = OperatingSystem.IsWindows()
            ? "echo %TEST_VAR%"
            : "echo $TEST_VAR";

        var json = await RunCommandTool.RunCommand(command,
            environment: "{\"TEST_VAR\": \"hello_from_env\"}");
        using var doc = JsonDocument.Parse(json);
        Assert.IsTrue(doc.RootElement.GetProperty("stdout").GetString()!.Contains("hello_from_env"));
    }

    [TestMethod]
    public async Task InvalidEnvironmentJsonReturnsError()
    {
        var json = await RunCommandTool.RunCommand("echo hi", environment: "not json");
        using var doc = JsonDocument.Parse(json);
        Assert.AreEqual("Invalid environment JSON.", doc.RootElement.GetProperty("error").GetString());
    }

    [TestMethod]
    public async Task StderrCapture()
    {
        var command = OperatingSystem.IsWindows()
            ? "echo error_msg 1>&2"
            : "echo error_msg >&2";

        var json = await RunCommandTool.RunCommand(command);
        using var doc = JsonDocument.Parse(json);
        Assert.IsTrue(doc.RootElement.GetProperty("stderr").GetString()!.Contains("error_msg"));
    }

    [TestMethod]
    public async Task MaxOutputChars_TruncatesStdout()
    {
        var command = RepeatEchoCommand(lineCount: 200, toStderr: false);
        var json = await RunCommandTool.RunCommand(command, max_output_chars: 50);
        using var doc = JsonDocument.Parse(json);

        Assert.AreEqual(0, doc.RootElement.GetProperty("exit_code").GetInt32());
        Assert.IsTrue(doc.RootElement.GetProperty("stdout_truncated").GetBoolean());
        Assert.IsFalse(doc.RootElement.GetProperty("stderr_truncated").GetBoolean());
        Assert.IsTrue(doc.RootElement.GetProperty("stdout").GetString()!.Contains("[Truncated:"));
    }

    [TestMethod]
    public async Task MaxOutputChars_TruncatesStderrIndependently()
    {
        var command = RepeatEchoCommand(lineCount: 200, toStderr: true);
        var json = await RunCommandTool.RunCommand(command, max_output_chars: 50);
        using var doc = JsonDocument.Parse(json);

        Assert.AreEqual(0, doc.RootElement.GetProperty("exit_code").GetInt32());
        Assert.IsFalse(doc.RootElement.GetProperty("stdout_truncated").GetBoolean());
        Assert.IsTrue(doc.RootElement.GetProperty("stderr_truncated").GetBoolean());
        Assert.IsTrue(doc.RootElement.GetProperty("stderr").GetString()!.Contains("[Truncated:"));
    }

    [TestMethod]
    public async Task MaxOutputChars_TruncatesStdoutAndStderrIndependently()
    {
        var command = RepeatEchoBothStreamsCommand(lineCount: 200);
        var json = await RunCommandTool.RunCommand(command, max_output_chars: 50);
        using var doc = JsonDocument.Parse(json);

        Assert.AreEqual(0, doc.RootElement.GetProperty("exit_code").GetInt32());
        Assert.IsTrue(doc.RootElement.GetProperty("stdout_truncated").GetBoolean());
        Assert.IsTrue(doc.RootElement.GetProperty("stderr_truncated").GetBoolean());
        Assert.IsTrue(doc.RootElement.GetProperty("stdout").GetString()!.Contains("[Truncated:"));
        Assert.IsTrue(doc.RootElement.GetProperty("stderr").GetString()!.Contains("[Truncated:"));
    }

    [TestMethod]
    public async Task MaxOutputChars_ZeroReturnsValidationError()
    {
        var json = await RunCommandTool.RunCommand("echo hi", max_output_chars: 0);
        using var doc = JsonDocument.Parse(json);
        Assert.AreEqual("max_output_chars must be greater than 0.", doc.RootElement.GetProperty("error").GetString());
    }

    [TestMethod]
    public async Task LargeOutputOverLimit_DrainsAndCompletes()
    {
        var command = RepeatEchoCommand(lineCount: 20_000, toStderr: false);
        var json = await RunCommandTool.RunCommand(command, timeout_seconds: 10, max_output_chars: 10);
        using var doc = JsonDocument.Parse(json);

        Assert.AreEqual(0, doc.RootElement.GetProperty("exit_code").GetInt32());
        Assert.IsFalse(doc.RootElement.GetProperty("timed_out").GetBoolean());
        Assert.IsTrue(doc.RootElement.GetProperty("stdout_truncated").GetBoolean());
    }

    [TestMethod]
    public async Task ExplicitCmdRunsCommand_OnWindows()
    {
        if (!OperatingSystem.IsWindows())
            Assert.Inconclusive("cmd is Windows-only.");

        var json = await RunCommandTool.RunCommand("echo cmd_ok", shell: "cmd");
        using var doc = JsonDocument.Parse(json);

        Assert.AreEqual(0, doc.RootElement.GetProperty("exit_code").GetInt32());
        Assert.AreEqual("cmd", doc.RootElement.GetProperty("shell_used").GetString());
        Assert.IsTrue(doc.RootElement.GetProperty("stdout").GetString()!.Contains("cmd_ok"));
    }

    [TestMethod]
    public async Task ExplicitPowerShellRunsCommand_OnWindows()
    {
        if (!OperatingSystem.IsWindows())
            Assert.Inconclusive("Windows PowerShell fallback is Windows-only.");

        var json = await RunCommandTool.RunCommand("'ps_ok'", shell: "powershell");
        using var doc = JsonDocument.Parse(json);

        if (doc.RootElement.TryGetProperty("error", out var error)
            && error.GetString()!.Contains("not available", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Inconclusive(error.GetString());
        }

        Assert.AreEqual(0, doc.RootElement.GetProperty("exit_code").GetInt32());
        Assert.AreEqual("powershell", doc.RootElement.GetProperty("shell_used").GetString());
        Assert.IsTrue(doc.RootElement.GetProperty("stdout").GetString()!.Contains("ps_ok"));
    }

    [TestMethod]
    public async Task ExplicitPowerShellHandlesQuotes_OnWindows()
    {
        if (!OperatingSystem.IsWindows())
            Assert.Inconclusive("Windows PowerShell fallback is Windows-only.");

        var json = await RunCommandTool.RunCommand("$v = 'alpha \"beta\"'; Write-Output $v", shell: "powershell");
        using var doc = JsonDocument.Parse(json);

        if (doc.RootElement.TryGetProperty("error", out var error)
            && error.GetString()!.Contains("not available", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Inconclusive(error.GetString());
        }

        Assert.AreEqual(0, doc.RootElement.GetProperty("exit_code").GetInt32());
        Assert.AreEqual("powershell", doc.RootElement.GetProperty("shell_used").GetString());
        Assert.IsTrue(doc.RootElement.GetProperty("stdout").GetString()!.Contains("alpha \"beta\""));
    }

    [TestMethod]
    public async Task ExplicitPwshRunsOrFailsFast()
    {
        var json = await RunCommandTool.RunCommand("'pwsh_ok'", shell: "pwsh");
        using var doc = JsonDocument.Parse(json);

        if (doc.RootElement.TryGetProperty("error", out var error))
        {
            Assert.IsTrue(error.GetString()!.Contains("Requested shell 'pwsh' is not available"));
            return;
        }

        Assert.AreEqual(0, doc.RootElement.GetProperty("exit_code").GetInt32());
        Assert.AreEqual("pwsh", doc.RootElement.GetProperty("shell_used").GetString());
        Assert.IsTrue(doc.RootElement.GetProperty("stdout").GetString()!.Contains("pwsh_ok"));
    }

    [TestMethod]
    public async Task ExplicitCmdFailsFast_OnNonWindows()
    {
        if (OperatingSystem.IsWindows())
            Assert.Inconclusive("cmd is available on Windows.");

        var json = await RunCommandTool.RunCommand("echo hi", shell: "cmd");
        using var doc = JsonDocument.Parse(json);
        Assert.IsTrue(doc.RootElement.GetProperty("error").GetString()!.Contains("only available on Windows"));
    }

    [TestMethod]
    public async Task UnknownShellReturnsError()
    {
        var json = await RunCommandTool.RunCommand("echo hi", shell: "definitely_missing_shell");
        using var doc = JsonDocument.Parse(json);
        Assert.IsTrue(doc.RootElement.GetProperty("error").GetString()!.Contains("Unknown shell"));
    }

    [TestMethod]
    public async Task CmdPreservesEmbeddedQuotes_OnWindows()
    {
        if (!OperatingSystem.IsWindows())
            Assert.Inconclusive("cmd is Windows-only.");

        var json = await RunCommandTool.RunCommand("echo \"hello world\"", shell: "cmd");
        using var doc = JsonDocument.Parse(json);

        Assert.AreEqual(0, doc.RootElement.GetProperty("exit_code").GetInt32());
        var stdout = doc.RootElement.GetProperty("stdout").GetString()!;
        StringAssert.Contains(stdout, "\"hello world\"");
        Assert.IsFalse(stdout.Contains("\\\""), $"backslash should not leak through cmd.exe; got: {stdout}");
    }

    [TestMethod]
    public async Task AutoOnWindowsPreservesEmbeddedQuotes()
    {
        if (!OperatingSystem.IsWindows())
            Assert.Inconclusive("auto-on-Windows uses cmd; non-Windows path is /bin/sh and is exercised elsewhere.");

        var json = await RunCommandTool.RunCommand("echo \"alpha beta\"");
        using var doc = JsonDocument.Parse(json);

        Assert.AreEqual(0, doc.RootElement.GetProperty("exit_code").GetInt32());
        var stdout = doc.RootElement.GetProperty("stdout").GetString()!;
        StringAssert.Contains(stdout, "\"alpha beta\"");
        Assert.IsFalse(stdout.Contains("\\\""), $"backslash should not leak through cmd.exe; got: {stdout}");
    }

    [TestMethod]
    public async Task CmdPreservesQuotedAmpersand_OnWindows()
    {
        if (!OperatingSystem.IsWindows())
            Assert.Inconclusive("cmd is Windows-only.");

        var json = await RunCommandTool.RunCommand("echo \"a&b\"", shell: "cmd");
        using var doc = JsonDocument.Parse(json);

        Assert.AreEqual(0, doc.RootElement.GetProperty("exit_code").GetInt32());
        var stdout = doc.RootElement.GetProperty("stdout").GetString()!;
        StringAssert.Contains(stdout, "\"a&b\"");
    }

    [TestMethod]
    public async Task PwshHandlesSingleQuotes()
    {
        var json = await RunCommandTool.RunCommand("Write-Output 'it''s fine'", shell: "pwsh");
        if (TryReadUnavailable(json, out var skip)) { Assert.Inconclusive(skip); return; }

        using var doc = JsonDocument.Parse(json);
        Assert.AreEqual(0, doc.RootElement.GetProperty("exit_code").GetInt32());
        StringAssert.Contains(doc.RootElement.GetProperty("stdout").GetString()!, "it's fine");
    }

    [TestMethod]
    public async Task PwshHandlesBackticksAndDollarSign()
    {
        // Backtick escapes the $; the literal text $env should pass through verbatim.
        var json = await RunCommandTool.RunCommand("Write-Output \"price `$5\"", shell: "pwsh");
        if (TryReadUnavailable(json, out var skip)) { Assert.Inconclusive(skip); return; }

        using var doc = JsonDocument.Parse(json);
        Assert.AreEqual(0, doc.RootElement.GetProperty("exit_code").GetInt32());
        StringAssert.Contains(doc.RootElement.GetProperty("stdout").GetString()!, "price $5");
    }

    [TestMethod]
    public async Task PwshHandlesDollarVariables()
    {
        var json = await RunCommandTool.RunCommand("$x = 21; Write-Output ($x * 2)", shell: "pwsh");
        if (TryReadUnavailable(json, out var skip)) { Assert.Inconclusive(skip); return; }

        using var doc = JsonDocument.Parse(json);
        Assert.AreEqual(0, doc.RootElement.GetProperty("exit_code").GetInt32());
        StringAssert.Contains(doc.RootElement.GetProperty("stdout").GetString()!, "42");
    }

    [TestMethod]
    public async Task PwshHandlesMultilineCommand()
    {
        var json = await RunCommandTool.RunCommand("Write-Output 'line1'\nWrite-Output 'line2'", shell: "pwsh");
        if (TryReadUnavailable(json, out var skip)) { Assert.Inconclusive(skip); return; }

        using var doc = JsonDocument.Parse(json);
        Assert.AreEqual(0, doc.RootElement.GetProperty("exit_code").GetInt32());
        var stdout = doc.RootElement.GetProperty("stdout").GetString()!;
        StringAssert.Contains(stdout, "line1");
        StringAssert.Contains(stdout, "line2");
    }

    [TestMethod]
    public async Task PwshHandlesObjectPipeline()
    {
        var json = await RunCommandTool.RunCommand("(1,2,3,4) | Measure-Object -Sum | Select-Object -ExpandProperty Sum", shell: "pwsh");
        if (TryReadUnavailable(json, out var skip)) { Assert.Inconclusive(skip); return; }

        using var doc = JsonDocument.Parse(json);
        Assert.AreEqual(0, doc.RootElement.GetProperty("exit_code").GetInt32());
        StringAssert.Contains(doc.RootElement.GetProperty("stdout").GetString()!, "10");
    }

    [TestMethod]
    public async Task PwshPropagatesNonZeroExit()
    {
        var json = await RunCommandTool.RunCommand("exit 7", shell: "pwsh");
        if (TryReadUnavailable(json, out var skip)) { Assert.Inconclusive(skip); return; }

        using var doc = JsonDocument.Parse(json);
        Assert.AreEqual(7, doc.RootElement.GetProperty("exit_code").GetInt32());
    }

    [TestMethod]
    public async Task PwshFailsFast_OnNonWindowsWithoutPwshInstall()
    {
        // Cross-platform sanity check: pwsh on a host without pwsh installed must surface a clear
        // 'not available' error rather than silently falling back to another shell.
        var json = await RunCommandTool.RunCommand("Write-Output 'should-not-run'", shell: "pwsh");
        using var doc = JsonDocument.Parse(json);

        if (doc.RootElement.TryGetProperty("error", out var error))
        {
            StringAssert.Contains(error.GetString()!, "Requested shell 'pwsh' is not available");
            return;
        }

        // pwsh is installed; verify it ran and exit code is 0.
        Assert.AreEqual(0, doc.RootElement.GetProperty("exit_code").GetInt32());
        Assert.AreEqual("pwsh", doc.RootElement.GetProperty("shell_used").GetString());
    }

    [TestMethod]
    public async Task WindowsPowerShellHandlesBackticksAndDollarSign_OnWindows()
    {
        if (!OperatingSystem.IsWindows())
            Assert.Inconclusive("Windows PowerShell is Windows-only.");

        var json = await RunCommandTool.RunCommand("Write-Output \"price `$5\"", shell: "powershell");
        if (TryReadUnavailable(json, out var skip)) { Assert.Inconclusive(skip); return; }

        using var doc = JsonDocument.Parse(json);
        Assert.AreEqual(0, doc.RootElement.GetProperty("exit_code").GetInt32());
        StringAssert.Contains(doc.RootElement.GetProperty("stdout").GetString()!, "price $5");
    }

    [TestMethod]
    public async Task WindowsPowerShellHandlesDollarVariables_OnWindows()
    {
        if (!OperatingSystem.IsWindows())
            Assert.Inconclusive("Windows PowerShell is Windows-only.");

        var json = await RunCommandTool.RunCommand("$x = 21; Write-Output ($x * 2)", shell: "powershell");
        if (TryReadUnavailable(json, out var skip)) { Assert.Inconclusive(skip); return; }

        using var doc = JsonDocument.Parse(json);
        Assert.AreEqual(0, doc.RootElement.GetProperty("exit_code").GetInt32());
        StringAssert.Contains(doc.RootElement.GetProperty("stdout").GetString()!, "42");
    }

    [TestMethod]
    public async Task WindowsPowerShellHandlesMultilineCommand_OnWindows()
    {
        if (!OperatingSystem.IsWindows())
            Assert.Inconclusive("Windows PowerShell is Windows-only.");

        var json = await RunCommandTool.RunCommand("Write-Output 'line1'\nWrite-Output 'line2'", shell: "powershell");
        if (TryReadUnavailable(json, out var skip)) { Assert.Inconclusive(skip); return; }

        using var doc = JsonDocument.Parse(json);
        Assert.AreEqual(0, doc.RootElement.GetProperty("exit_code").GetInt32());
        var stdout = doc.RootElement.GetProperty("stdout").GetString()!;
        StringAssert.Contains(stdout, "line1");
        StringAssert.Contains(stdout, "line2");
    }

    [TestMethod]
    public async Task WindowsPowerShellFailsFast_OnNonWindows()
    {
        if (OperatingSystem.IsWindows())
            Assert.Inconclusive("powershell is available on Windows.");

        var json = await RunCommandTool.RunCommand("Write-Output 'no'", shell: "powershell");
        using var doc = JsonDocument.Parse(json);
        StringAssert.Contains(doc.RootElement.GetProperty("error").GetString()!, "only available on Windows");
    }

    static bool TryReadUnavailable(string json, out string message)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("error", out var error)
            && error.GetString() is { } text
            && text.Contains("not available", StringComparison.OrdinalIgnoreCase))
        {
            message = text;
            return true;
        }
        message = string.Empty;
        return false;
    }

    static string RepeatEchoCommand(int lineCount, bool toStderr)
    {
        if (OperatingSystem.IsWindows())
        {
            var redirect = toStderr ? " 1>&2" : "";
            return $"for /L %i in (1,1,{lineCount}) do @echo 1234567890{redirect}";
        }

        var streamRedirect = toStderr ? " >&2" : "";
        return $"i=0; while [ $i -lt {lineCount} ]; do echo 1234567890{streamRedirect}; i=$((i+1)); done";
    }

    static string RepeatEchoBothStreamsCommand(int lineCount)
    {
        if (OperatingSystem.IsWindows())
            return $"for /L %i in (1,1,{lineCount}) do @(echo 1234567890 & echo abcdefghij 1>&2)";

        return $"i=0; while [ $i -lt {lineCount} ]; do echo 1234567890; echo abcdefghij >&2; i=$((i+1)); done";
    }
}
