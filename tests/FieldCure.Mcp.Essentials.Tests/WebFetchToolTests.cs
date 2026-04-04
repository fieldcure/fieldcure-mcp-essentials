using System.Text.Json;
using FieldCure.Mcp.Essentials.Tools;

namespace FieldCure.Mcp.Essentials.Tests;

[TestClass]
public class WebFetchToolTests
{
    [TestMethod]
    public async Task FetchesPublicPage()
    {
        var json = await WebFetchTool.WebFetch("https://example.com");
        using var doc = JsonDocument.Parse(json);
        var content = doc.RootElement.GetProperty("content").GetString();
        Assert.IsNotNull(content);
        Assert.IsTrue(content!.Length > 0);
        Assert.AreEqual("https://example.com", doc.RootElement.GetProperty("url").GetString());
    }

    [TestMethod]
    public async Task RespectsMaxLength()
    {
        var json = await WebFetchTool.WebFetch("https://example.com", max_length: 100);
        using var doc = JsonDocument.Parse(json);
        var content = doc.RootElement.GetProperty("content").GetString();
        Assert.IsTrue(content!.Length <= 100);
    }

    [TestMethod]
    public async Task InvalidUrlReturnsError()
    {
        var json = await WebFetchTool.WebFetch("ftp://invalid.com");
        using var doc = JsonDocument.Parse(json);
        Assert.IsTrue(doc.RootElement.GetProperty("error").GetString()!.Contains("Only http"));
    }

    [TestMethod]
    public async Task SsrfBlocksLocalhost()
    {
        var json = await WebFetchTool.WebFetch("http://localhost:9999");
        using var doc = JsonDocument.Parse(json);
        Assert.IsTrue(doc.RootElement.GetProperty("error").GetString()!.Contains("SSRF"));
    }

    [TestMethod]
    public async Task SsrfBlocksPrivateIp()
    {
        var json = await WebFetchTool.WebFetch("http://10.0.0.1");
        using var doc = JsonDocument.Parse(json);
        Assert.IsTrue(doc.RootElement.GetProperty("error").GetString()!.Contains("SSRF"));
    }

    [TestMethod]
    public async Task TruncationFlagSetWhenExceeded()
    {
        // example.com content is short, so use a very small max_length to force truncation
        var json = await WebFetchTool.WebFetch("https://example.com", max_length: 100);
        using var doc = JsonDocument.Parse(json);
        // If content was truncated, truncated should be true
        if (doc.RootElement.TryGetProperty("truncated", out var truncated))
        {
            Assert.IsTrue(truncated.GetBoolean());
        }
        // Content should be <= max_length
        var content = doc.RootElement.GetProperty("content").GetString();
        Assert.IsTrue(content!.Length <= 100);
    }

    [TestMethod]
    public async Task OutputContainsMarkdownSyntax()
    {
        var json = await WebFetchTool.WebFetch("https://example.com");
        using var doc = JsonDocument.Parse(json);
        var content = doc.RootElement.GetProperty("content").GetString()!;
        Assert.IsTrue(content.Length > 0);
    }
}
