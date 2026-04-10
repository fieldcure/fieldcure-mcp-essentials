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

    #region max_response_chars Tests

    [TestMethod]
    public async Task MaxResponseChars_TruncatesBody()
    {
        // httpbin.org/get returns ~300+ chars; limit to 50
        var json = await HttpRequestTool.HttpRequest(
            "https://httpbin.org/get",
            max_response_chars: 50);
        using var doc = JsonDocument.Parse(json);

        Assert.AreEqual(200, doc.RootElement.GetProperty("status_code").GetInt32());
        Assert.IsTrue(doc.RootElement.GetProperty("truncated").GetBoolean());

        var body = doc.RootElement.GetProperty("body").GetString()!;
        Assert.IsTrue(body.Contains("[Truncated:"));
        Assert.IsTrue(body.Contains("more chars omitted"));
    }

    [TestMethod]
    public async Task MaxResponseChars_Null_NoTruncation()
    {
        var json = await HttpRequestTool.HttpRequest(
            "https://httpbin.org/get",
            max_response_chars: null);
        using var doc = JsonDocument.Parse(json);

        Assert.AreEqual(200, doc.RootElement.GetProperty("status_code").GetInt32());
        Assert.IsFalse(doc.RootElement.TryGetProperty("truncated", out _));

        var body = doc.RootElement.GetProperty("body").GetString()!;
        Assert.IsFalse(body.Contains("[Truncated:"));
    }

    [TestMethod]
    public async Task MaxResponseChars_LargerThanBody_NoTruncation()
    {
        // httpbin.org/get returns ~300-500 chars; limit to 100000
        var json = await HttpRequestTool.HttpRequest(
            "https://httpbin.org/get",
            max_response_chars: 100_000);
        using var doc = JsonDocument.Parse(json);

        Assert.AreEqual(200, doc.RootElement.GetProperty("status_code").GetInt32());
        Assert.IsFalse(doc.RootElement.TryGetProperty("truncated", out _));
    }

    #endregion
}
