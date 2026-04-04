using FieldCure.Mcp.Essentials.Search;

namespace FieldCure.Mcp.Essentials.Tests;

[TestClass]
public class TavilySearchEngineTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task ReturnsResults()
    {
        var apiKey = Environment.GetEnvironmentVariable("TAVILY_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
            Assert.Inconclusive("TAVILY_API_KEY not set");

        var engine = new TavilySearchEngine(apiKey);
        var results = await engine.SearchAsync("C# programming", 3, ct: TestContext.CancellationToken);
        Assert.IsTrue(results.Length > 0);

        var first = results[0];
        Assert.IsFalse(string.IsNullOrEmpty(first.Title));
        Assert.IsFalse(string.IsNullOrEmpty(first.Url));
        Assert.IsFalse(string.IsNullOrEmpty(first.Snippet), "Tavily should return content snippets");
    }
}
