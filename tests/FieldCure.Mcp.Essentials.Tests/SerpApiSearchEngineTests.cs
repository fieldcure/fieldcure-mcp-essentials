using FieldCure.Mcp.Essentials.Search;

namespace FieldCure.Mcp.Essentials.Tests;

[TestClass]
public class SerpApiSearchEngineTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task ReturnsResults()
    {
        var apiKey = Environment.GetEnvironmentVariable("SERPAPI_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
            Assert.Inconclusive("SERPAPI_API_KEY not set");

        var engine = new SerpApiSearchEngine(apiKey);
        var results = await engine.SearchAsync("C# programming", 3, ct: TestContext.CancellationToken);
        Assert.IsTrue(results.Length > 0);

        var first = results[0];
        Assert.IsFalse(string.IsNullOrEmpty(first.Title));
        Assert.IsFalse(string.IsNullOrEmpty(first.Url));
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task RegionWorks()
    {
        var apiKey = Environment.GetEnvironmentVariable("SERPAPI_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
            Assert.Inconclusive("SERPAPI_API_KEY not set");

        var engine = new SerpApiSearchEngine(apiKey);
        var results = await engine.SearchAsync("서울 날씨", 3, region: "ko-kr", ct: TestContext.CancellationToken);
        Assert.IsTrue(results.Length > 0);
    }
}
