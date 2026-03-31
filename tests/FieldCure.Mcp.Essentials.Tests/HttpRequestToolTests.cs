using System.Text.Json;
using FieldCure.Mcp.Essentials.Tools;

namespace FieldCure.Mcp.Essentials.Tests;

[TestClass]
public class HttpRequestToolTests
{
    [TestMethod]
    public async Task GetRequest200()
    {
        var json = await HttpRequestTool.HttpRequest("https://httpbin.org/get");
        using var doc = JsonDocument.Parse(json);
        Assert.AreEqual(200, doc.RootElement.GetProperty("status_code").GetInt32());
        Assert.IsTrue(doc.RootElement.GetProperty("elapsed_ms").GetInt64() > 0);
        Assert.IsTrue(doc.RootElement.GetProperty("body").GetString()!.Length > 0);
    }

    [TestMethod]
    public async Task PostRequestWithBody()
    {
        var json = await HttpRequestTool.HttpRequest(
            "https://httpbin.org/post",
            method: "POST",
            body: "{\"test\": true}");
        using var doc = JsonDocument.Parse(json);
        Assert.AreEqual(200, doc.RootElement.GetProperty("status_code").GetInt32());
        Assert.IsTrue(doc.RootElement.GetProperty("body").GetString()!.Contains("test"));
    }

    [TestMethod]
    public async Task CustomHeaders()
    {
        var json = await HttpRequestTool.HttpRequest(
            "https://httpbin.org/headers",
            headers: "{\"X-Custom\": \"test-value\"}");
        using var doc = JsonDocument.Parse(json);
        Assert.IsTrue(doc.RootElement.GetProperty("body").GetString()!.Contains("test-value"));
    }

    [TestMethod]
    public async Task NotFoundStatus()
    {
        var json = await HttpRequestTool.HttpRequest("https://httpbin.org/status/404");
        using var doc = JsonDocument.Parse(json);
        Assert.AreEqual(404, doc.RootElement.GetProperty("status_code").GetInt32());
    }

    [TestMethod]
    public async Task InvalidUrl()
    {
        var json = await HttpRequestTool.HttpRequest("ftp://invalid.com");
        using var doc = JsonDocument.Parse(json);
        Assert.IsTrue(doc.RootElement.GetProperty("error").GetString()!.Contains("Only http"));
    }

    [TestMethod]
    public async Task SsrfBlocksLocalhost()
    {
        var json = await HttpRequestTool.HttpRequest("http://localhost:9999");
        using var doc = JsonDocument.Parse(json);
        Assert.IsTrue(doc.RootElement.GetProperty("error").GetString()!.Contains("SSRF"));
    }

    [TestMethod]
    public async Task SsrfBlocksPrivateIp()
    {
        var json = await HttpRequestTool.HttpRequest("http://192.168.1.1");
        using var doc = JsonDocument.Parse(json);
        Assert.IsTrue(doc.RootElement.GetProperty("error").GetString()!.Contains("SSRF"));
    }

    [TestMethod]
    public async Task TimeoutReturnsError()
    {
        var json = await HttpRequestTool.HttpRequest(
            "https://httpbin.org/delay/10",
            timeout_seconds: 2);
        using var doc = JsonDocument.Parse(json);
        Assert.IsTrue(doc.RootElement.GetProperty("error").GetString()!.Contains("timed out"));
    }

    [TestMethod]
    public async Task InvalidHeadersJson()
    {
        var json = await HttpRequestTool.HttpRequest(
            "https://httpbin.org/get",
            headers: "not json");
        using var doc = JsonDocument.Parse(json);
        Assert.IsTrue(doc.RootElement.GetProperty("error").GetString()!.Contains("Invalid headers"));
    }
}
