using System.Text.Json;
using FieldCure.Mcp.Essentials.Search;
using FieldCure.Mcp.Essentials.Tools;

namespace FieldCure.Mcp.Essentials.Tests;

[TestClass]
public class WebSearchToolTests
{
    [TestMethod]
    public async Task EmptyQueryReturnsError()
    {
        var engine = new DuckDuckGoSearchEngine();
        var json = await WebSearchTool.WebSearch(engine, "   ", cancellationToken: TestContext.CancellationToken);
        using var doc = JsonDocument.Parse(json);
        Assert.Contains("empty", doc.RootElement.GetProperty("error").GetString()!);
    }

    [TestMethod]
    public async Task MaxResultsIsClamped()
    {
        var engine = new FakeSearchEngine(20);
        var json = await WebSearchTool.WebSearch(engine, "test", max_results: 50, cancellationToken: TestContext.CancellationToken);
        using var doc = JsonDocument.Parse(json);
        // FakeSearchEngine returns min(requested, available), and max_results is clamped to 10
        var results = doc.RootElement.GetProperty("results");
        Assert.IsLessThanOrEqualTo(10, results.GetArrayLength());
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task DuckDuckGoReturnsResults()
    {
        var engine = new DuckDuckGoSearchEngine();
        var json = await WebSearchTool.WebSearch(engine, "C# programming language", max_results: 3, cancellationToken: TestContext.CancellationToken);
        using var doc = JsonDocument.Parse(json);
        var results = doc.RootElement.GetProperty("results");
        Assert.IsGreaterThan(0, results.GetArrayLength());

        var first = results[0];
        Assert.IsFalse(string.IsNullOrEmpty(first.GetProperty("title").GetString()));
        Assert.IsFalse(string.IsNullOrEmpty(first.GetProperty("url").GetString()));
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task BingReturnsResults()
    {
        var engine = new BingSearchEngine();
        var json = await WebSearchTool.WebSearch(engine, "C# programming language", max_results: 3, cancellationToken: TestContext.CancellationToken);
        using var doc = JsonDocument.Parse(json);
        var results = doc.RootElement.GetProperty("results");
        Assert.IsGreaterThan(0, results.GetArrayLength());

        var first = results[0];
        Assert.IsFalse(string.IsNullOrEmpty(first.GetProperty("title").GetString()));
        Assert.IsFalse(string.IsNullOrEmpty(first.GetProperty("url").GetString()));
    }

    [TestMethod]
    public async Task RegionParameterIsPassedThrough()
    {
        var engine = new FakeSearchEngine(5);
        var json = await WebSearchTool.WebSearch(engine, "test", region: "ko-kr", cancellationToken: TestContext.CancellationToken);
        using var doc = JsonDocument.Parse(json);
        var results = doc.RootElement.GetProperty("results");
        Assert.IsGreaterThan(0, results.GetArrayLength());
        Assert.AreEqual("ko-kr", engine.LastRegion);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task DuckDuckGoWithKoreanRegion()
    {
        var engine = new DuckDuckGoSearchEngine();
        var json = await WebSearchTool.WebSearch(engine, "서울 날씨", region: "ko-kr", cancellationToken: TestContext.CancellationToken);
        using var doc = JsonDocument.Parse(json);
        var results = doc.RootElement.GetProperty("results");
        Assert.IsGreaterThan(0, results.GetArrayLength());
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task NullRegionWorksLikeGlobal()
    {
        var engine = new DuckDuckGoSearchEngine();
        var json = await WebSearchTool.WebSearch(engine, "test query", region: null, cancellationToken: TestContext.CancellationToken);
        using var doc = JsonDocument.Parse(json);
        Assert.IsFalse(doc.RootElement.TryGetProperty("error", out _));
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task InvalidRegionFallsBackToGlobal()
    {
        var engine = new DuckDuckGoSearchEngine();
        var json = await WebSearchTool.WebSearch(engine, "test", region: "xx-yy", cancellationToken: TestContext.CancellationToken);
        using var doc = JsonDocument.Parse(json);
        Assert.IsFalse(doc.RootElement.TryGetProperty("error", out _));
    }

    public required TestContext TestContext { get; init; }

    /// <summary>
    /// A fake search engine for unit testing.
    /// </summary>
    sealed class FakeSearchEngine : ISearchEngine
    {
        readonly int _available;

        /// <summary>
        /// The last region value passed to <see cref="SearchAsync"/>.
        /// </summary>
        public string? LastRegion { get; private set; }

        public FakeSearchEngine(int available) => _available = available;

        public Task<SearchResult[]> SearchAsync(
            string query, int maxResults, string? region = null, CancellationToken ct = default)
        {
            LastRegion = region;
            var count = Math.Min(maxResults, _available);
            var results = Enumerable.Range(1, count)
                .Select(i => new SearchResult($"Title {i}", $"https://example.com/{i}", $"Snippet {i}"))
                .ToArray();
            return Task.FromResult(results);
        }
    }
}
