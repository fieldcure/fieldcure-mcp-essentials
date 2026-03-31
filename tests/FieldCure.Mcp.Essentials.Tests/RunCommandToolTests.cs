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
    public async Task NonZeroExitCode()
    {
        var json = await RunCommandTool.RunCommand("exit /b 42");
        using var doc = JsonDocument.Parse(json);
        Assert.AreEqual(42, doc.RootElement.GetProperty("exit_code").GetInt32());
    }

    [TestMethod]
    public async Task TimeoutKillsProcess()
    {
        // ping -n 60 = ~60 seconds on Windows
        var json = await RunCommandTool.RunCommand("ping -n 60 127.0.0.1", timeout_seconds: 2);
        using var doc = JsonDocument.Parse(json);
        Assert.IsTrue(doc.RootElement.GetProperty("timed_out").GetBoolean());
    }

    [TestMethod]
    public async Task WorkingDirectory()
    {
        var tempDir = Path.GetTempPath();
        var json = await RunCommandTool.RunCommand("cd", working_directory: tempDir);
        using var doc = JsonDocument.Parse(json);
        Assert.AreEqual(0, doc.RootElement.GetProperty("exit_code").GetInt32());
        // cmd /c cd prints the current directory
        var stdout = doc.RootElement.GetProperty("stdout").GetString()!.Trim();
        Assert.IsTrue(stdout.Contains(Path.GetTempPath().TrimEnd('\\')),
            $"Expected temp path in stdout, got: {stdout}");
    }

    [TestMethod]
    public async Task InvalidWorkingDirectory()
    {
        var json = await RunCommandTool.RunCommand("echo hi", working_directory: @"C:\nonexistent_dir_12345");
        using var doc = JsonDocument.Parse(json);
        Assert.IsTrue(doc.RootElement.TryGetProperty("error", out _));
    }

    [TestMethod]
    public async Task EnvironmentVariables()
    {
        var json = await RunCommandTool.RunCommand("echo %TEST_VAR%",
            environment: "{\"TEST_VAR\": \"hello_from_env\"}");
        using var doc = JsonDocument.Parse(json);
        Assert.IsTrue(doc.RootElement.GetProperty("stdout").GetString()!.Contains("hello_from_env"));
    }

    [TestMethod]
    public async Task StderrCapture()
    {
        var json = await RunCommandTool.RunCommand("echo error_msg 1>&2");
        using var doc = JsonDocument.Parse(json);
        Assert.IsTrue(doc.RootElement.GetProperty("stderr").GetString()!.Contains("error_msg"));
    }
}
