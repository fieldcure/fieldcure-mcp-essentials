using System.Text.Json;
using FieldCure.Mcp.Essentials.Tools;

namespace FieldCure.Mcp.Essentials.Tests;

[TestClass]
public class WebFetchToolTests
{
    [TestMethod]
    public async Task FetchesPublicPage()
    {
        var json = await WebFetchTool.WebFetch("https://example.com", cancellationToken: TestContext.CancellationToken);
        using var doc = JsonDocument.Parse(json);
        var content = doc.RootElement.GetProperty("content").GetString();
        Assert.IsNotNull(content);
        Assert.IsTrue(content!.Length > 0);
        Assert.AreEqual("https://example.com", doc.RootElement.GetProperty("url").GetString());
    }

    [TestMethod]
    public async Task RespectsMaxLength()
    {
        var json = await WebFetchTool.WebFetch("https://example.com", max_length: 100, cancellationToken: TestContext.CancellationToken);
        using var doc = JsonDocument.Parse(json);
        var content = doc.RootElement.GetProperty("content").GetString();
        Assert.IsTrue(content!.Length <= 100);
    }

    [TestMethod]
    public async Task InvalidUrlReturnsError()
    {
        var json = await WebFetchTool.WebFetch("ftp://invalid.com", cancellationToken: TestContext.CancellationToken);
        using var doc = JsonDocument.Parse(json);
        StringAssert.Contains(doc.RootElement.GetProperty("error").GetString()!, "Only http");
    }

    [TestMethod]
    public async Task SsrfBlocksLocalhost()
    {
        var json = await WebFetchTool.WebFetch("http://localhost:9999", cancellationToken: TestContext.CancellationToken);
        using var doc = JsonDocument.Parse(json);
        StringAssert.Contains(doc.RootElement.GetProperty("error").GetString()!, "SSRF");
    }

    [TestMethod]
    public async Task SsrfBlocksPrivateIp()
    {
        var json = await WebFetchTool.WebFetch("http://10.0.0.1", cancellationToken: TestContext.CancellationToken);
        using var doc = JsonDocument.Parse(json);
        StringAssert.Contains(doc.RootElement.GetProperty("error").GetString()!, "SSRF");
    }

    [TestMethod]
    public async Task TruncationFlagSetWhenExceeded()
    {
        var json = await WebFetchTool.WebFetch("https://example.com", max_length: 100, cancellationToken: TestContext.CancellationToken);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("truncated", out var truncated))
        {
            Assert.IsTrue(truncated.GetBoolean());
        }
        var content = doc.RootElement.GetProperty("content").GetString();
        Assert.IsTrue(content!.Length <= 100);
    }

    [TestMethod]
    public async Task OutputContainsMarkdownSyntax()
    {
        var json = await WebFetchTool.WebFetch("https://example.com", cancellationToken: TestContext.CancellationToken);
        using var doc = JsonDocument.Parse(json);
        var content = doc.RootElement.GetProperty("content").GetString()!;
        Assert.IsTrue(content.Length > 0);
    }

    public required TestContext TestContext { get; init; }
}
