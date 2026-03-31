using System.Text.Json;
using FieldCure.Mcp.Essentials.Tools;

namespace FieldCure.Mcp.Essentials.Tests;

[TestClass]
public class GetEnvironmentToolTests
{
    [TestMethod]
    public void ReturnsAllExpectedFields()
    {
        var json = GetEnvironmentTool.GetEnvironment();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.IsTrue(root.TryGetProperty("current_time", out _));
        Assert.IsTrue(root.TryGetProperty("current_time_utc", out _));
        Assert.IsTrue(root.TryGetProperty("timezone", out _));
        Assert.IsTrue(root.TryGetProperty("os", out _));
        Assert.IsTrue(root.TryGetProperty("hostname", out _));
        Assert.IsTrue(root.TryGetProperty("username", out _));
        Assert.IsTrue(root.TryGetProperty("working_directory", out _));
        Assert.IsTrue(root.TryGetProperty("dotnet_version", out _));
    }

    [TestMethod]
    public void CurrentTimeIsValidIso8601()
    {
        var json = GetEnvironmentTool.GetEnvironment();
        using var doc = JsonDocument.Parse(json);

        var timeStr = doc.RootElement.GetProperty("current_time").GetString()!;
        Assert.IsTrue(DateTimeOffset.TryParse(timeStr, out _), $"Invalid ISO 8601: {timeStr}");

        var utcStr = doc.RootElement.GetProperty("current_time_utc").GetString()!;
        Assert.IsTrue(DateTimeOffset.TryParse(utcStr, out var utc), $"Invalid ISO 8601: {utcStr}");
        Assert.AreEqual(0, utc.Offset.TotalMinutes, "UTC time should have zero offset");
    }

    [TestMethod]
    public void HostnameMatchesMachineName()
    {
        var json = GetEnvironmentTool.GetEnvironment();
        using var doc = JsonDocument.Parse(json);

        var hostname = doc.RootElement.GetProperty("hostname").GetString();
        Assert.AreEqual(Environment.MachineName, hostname);
    }
}
