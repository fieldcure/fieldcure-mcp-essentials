using System.Text.Json;
using FieldCure.Mcp.Essentials.Search;
using FieldCure.Mcp.Essentials.Services;
using ModelContextProtocol.Protocol;

namespace FieldCure.Mcp.Essentials.Tests;

[TestClass]
public class LazyPaidSearchEngineTests
{
    [TestMethod]
    public async Task ExplicitPaidEngineWithoutKey_WithoutElicitationSupport_FallsBackToFreeSearch()
    {
        var fallback = new FakeSearchEngine();
        var engine = CreateLazyEngine(fallback);

        var results = await ((IMcpAwareSearchEngine)engine).SearchAsync(
            gate: null,
            "test",
            maxResults: 5,
            region: null,
            ct: CancellationToken.None);

        Assert.AreEqual(1, results.Length);
        Assert.AreEqual("Fallback", results[0].Title);
        Assert.AreEqual(1, fallback.Calls);
    }

    [TestMethod]
    public async Task ExplicitPaidEngineWithoutKey_ElicitsAndUsesProvidedKey()
    {
        var fallback = new FakeSearchEngine();
        var paid = new FakeSearchEngine("Paid");
        var gate = new FakeElicitGate();
        gate.Results.Enqueue(new ElicitGateResult(
            IsAccepted: true,
            Content: new Dictionary<string, JsonElement>
            {
                ["api_key"] = JsonDocument.Parse("{\"v\":\"test-key\"}").RootElement.GetProperty("v"),
            }));

        var engine = CreateLazyEngine(fallback, paid);
        var results = await ((IMcpAwareSearchEngine)engine).SearchAsync(
            gate,
            "test",
            maxResults: 5,
            region: null,
            ct: CancellationToken.None);

        Assert.AreEqual(0, fallback.Calls);
        Assert.AreEqual(1, paid.Calls);
        Assert.AreEqual(1, results.Length);
        Assert.AreEqual("Paid", results[0].Title);
        Assert.AreEqual(1, gate.Calls.Count);
        StringAssert.Contains(gate.Calls[0].Message, "Serper");
    }

    [TestMethod]
    public async Task CategoryFallback_MapsGenericResultsIntoCategoryShape()
    {
        var fallback = new FakeSearchEngine();
        var engine = CreateLazyEngine(fallback);

        var result = await ((IMcpAwareCategorySearchEngine)engine).SearchAsync(
            gate: null,
            new CategorySearchRequest
            {
                Category = SearchCategory.News,
                Query = "test",
                MaxResults = 5,
            },
            CancellationToken.None);

        Assert.AreEqual(SearchCategory.News, result.Category);
        Assert.AreEqual("Bing/DuckDuckGo (fallback)", result.Engine);
        Assert.AreEqual(1, result.Items.Count);
        Assert.AreEqual("Fallback", result.Items[0].Title);
        Assert.AreEqual("https://example.com/fallback", result.Items[0].Url);
    }

    [TestMethod]
    public async Task ExplicitPaidEngineWithoutKey_DeclinedKeyThenAcceptedFallback_UsesFreeSearch()
    {
        var fallback = new FakeSearchEngine();
        var gate = new FakeElicitGate();
        gate.Results.Enqueue(new ElicitGateResult(
            IsAccepted: false,
            Content: null));
        gate.Results.Enqueue(new ElicitGateResult(
            IsAccepted: true,
            Content: new Dictionary<string, JsonElement>
            {
                ["use_fallback"] = JsonDocument.Parse("{\"v\":true}").RootElement.GetProperty("v"),
            }));

        var engine = CreateLazyEngine(fallback);
        var results = await ((IMcpAwareSearchEngine)engine).SearchAsync(
            gate,
            "test",
            maxResults: 5,
            region: null,
            ct: CancellationToken.None);

        Assert.AreEqual(1, fallback.Calls);
        Assert.AreEqual(2, gate.Calls.Count);
        Assert.AreEqual("Fallback", results[0].Title);
    }

    [TestMethod]
    public async Task Invalidate_ForcesReElicitAndSkipsEnvFallback()
    {
        const string envVar = "TEST_INVALIDATE_KEY";
        Environment.SetEnvironmentVariable(envVar, "stale-key");
        try
        {
            var registry = new ApiKeyResolverRegistry();
            var gate = new FakeElicitGate();
            gate.Results.Enqueue(new ElicitGateResult(
                IsAccepted: true,
                Content: new Dictionary<string, JsonElement>
                {
                    ["api_key"] = JsonDocument.Parse("{\"v\":\"fresh-key\"}").RootElement.GetProperty("v"),
                }));

            // First resolve picks up the env var.
            var first = await registry.ResolveAsync(gate, envVar, "TestProvider", CancellationToken.None);
            Assert.AreEqual("stale-key", first);
            Assert.AreEqual(0, gate.Calls.Count);

            // Simulate upstream 401 → invalidate.
            registry.Invalidate(envVar);

            // Second resolve must skip the env var and elicit instead.
            var second = await registry.ResolveAsync(gate, envVar, "TestProvider", CancellationToken.None);
            Assert.AreEqual("fresh-key", second);
            Assert.AreEqual(1, gate.Calls.Count);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVar, null);
        }
    }

    [TestMethod]
    public void SupportedCategories_AreExposedBeforeLazyResolution()
    {
        var engine = new LazyPaidSearchEngine(
            "tavily",
            "Tavily",
            "TAVILY_API_KEY",
            new ApiKeyResolverRegistry(),
            new FakeSearchEngine(),
            static (_, key) => new TavilySearchEngine(key));

        CollectionAssert.AreEquivalent(
            new[] { SearchCategory.News },
            engine.SupportedCategories.ToArray());
    }

    static LazyPaidSearchEngine CreateLazyEngine(FakeSearchEngine fallback, ISearchEngine? paid = null) =>
        new(
            "serper",
            "Serper",
            "SERPER_API_KEY",
            new ApiKeyResolverRegistry(),
            fallback,
            (_, _) => paid ?? new FakeSearchEngine("Paid"));

    sealed class FakeSearchEngine : ISearchEngine
    {
        readonly string _title;

        public FakeSearchEngine(string title = "Fallback") => _title = title;

        public int Calls { get; private set; }

        public Task<SearchResult[]> SearchAsync(string query, int maxResults, string? region = null, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(new[]
            {
                new SearchResult(_title, "https://example.com/fallback", "Fallback snippet"),
            });
        }
    }

    sealed class FakeElicitGate : IElicitGate
    {
        public bool IsSupported { get; init; } = true;

        public Queue<ElicitGateResult> Results { get; } = new();

        public List<ElicitRequestParams> Calls { get; } = new();

        public Task<ElicitGateResult> ElicitAsync(ElicitRequestParams request, CancellationToken ct)
        {
            Calls.Add(request);
            return Task.FromResult(Results.Dequeue());
        }
    }
}
