using FieldCure.Mcp.Essentials.Search;

namespace FieldCure.Mcp.Essentials.Tests;

[TestClass]
public class CategorySearchTests
{
    public required TestContext TestContext { get; init; }

    // --- SerpApi Integration Tests ---

    [TestMethod]
    [TestCategory("Integration")]
    public async Task SerpApi_NewsSearch_ReturnsResults()
    {
        var engine = CreateSerpApiEngine();
        var result = await engine.SearchAsync(new CategorySearchRequest
        {
            Category = SearchCategory.News,
            Query = "artificial intelligence",
            MaxResults = 5,
        }, TestContext.CancellationToken);

        Assert.AreEqual(SearchCategory.News, result.Category);
        Assert.AreEqual("SerpApi", result.Engine);
        Assert.IsTrue(result.Items.Count > 0, "Expected news results");
        Assert.IsFalse(string.IsNullOrEmpty(result.Items[0].Title));
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task SerpApi_ImageSearch_ReturnsResults()
    {
        var engine = CreateSerpApiEngine();
        var result = await engine.SearchAsync(new CategorySearchRequest
        {
            Category = SearchCategory.Images,
            Query = "sunset landscape",
            MaxResults = 5,
        }, TestContext.CancellationToken);

        Assert.AreEqual(SearchCategory.Images, result.Category);
        Assert.IsTrue(result.Items.Count > 0, "Expected image results");
        Assert.IsFalse(string.IsNullOrEmpty(result.Items[0].ImageUrl ?? result.Items[0].ThumbnailUrl));
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task SerpApi_ScholarSearch_ReturnsResults()
    {
        var engine = CreateSerpApiEngine();
        var result = await engine.SearchAsync(new CategorySearchRequest
        {
            Category = SearchCategory.Scholar,
            Query = "machine learning",
            MaxResults = 5,
        }, TestContext.CancellationToken);

        Assert.AreEqual(SearchCategory.Scholar, result.Category);
        Assert.IsTrue(result.Items.Count > 0, "Expected scholar results");
        Assert.IsFalse(string.IsNullOrEmpty(result.Items[0].Title));
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task SerpApi_PatentSearch_ReturnsResults()
    {
        var engine = CreateSerpApiEngine();
        var result = await engine.SearchAsync(new CategorySearchRequest
        {
            Category = SearchCategory.Patents,
            Query = "solar panel",
            MaxResults = 10,
        }, TestContext.CancellationToken);

        Assert.AreEqual(SearchCategory.Patents, result.Category);
        Assert.IsTrue(result.Items.Count > 0, "Expected patent results");
        Assert.IsFalse(string.IsNullOrEmpty(result.Items[0].Title));
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task SerpApi_PatentSearch_InventorFilter()
    {
        var engine = CreateSerpApiEngine();
        var result = await engine.SearchAsync(new CategorySearchRequest
        {
            Category = SearchCategory.Patents,
            Query = "battery",
            Inventor = "Elon Musk",
            MaxResults = 10,
        }, TestContext.CancellationToken);

        Assert.AreEqual(SearchCategory.Patents, result.Category);
        // May or may not return results depending on patents filed
    }

    // --- Unit Tests (no API key needed) ---

    [TestMethod]
    public void SerpApi_SupportedCategories_AllFour()
    {
        var engine = new SerpApiSearchEngine("fake-key");
        Assert.IsTrue(engine.SupportedCategories.Contains(SearchCategory.News));
        Assert.IsTrue(engine.SupportedCategories.Contains(SearchCategory.Images));
        Assert.IsTrue(engine.SupportedCategories.Contains(SearchCategory.Scholar));
        Assert.IsTrue(engine.SupportedCategories.Contains(SearchCategory.Patents));
        Assert.AreEqual("SerpApi", engine.EngineName);
    }

    [TestMethod]
    public void Serper_SupportedCategories_AllFour()
    {
        var engine = new SerperSearchEngine("fake-key");
        Assert.IsTrue(engine.SupportedCategories.Contains(SearchCategory.News));
        Assert.IsTrue(engine.SupportedCategories.Contains(SearchCategory.Images));
        Assert.IsTrue(engine.SupportedCategories.Contains(SearchCategory.Scholar));
        Assert.IsTrue(engine.SupportedCategories.Contains(SearchCategory.Patents));
        Assert.AreEqual("Serper", engine.EngineName);
    }

    [TestMethod]
    public void Tavily_SupportedCategories_NewsOnly()
    {
        var engine = new TavilySearchEngine("fake-key");
        Assert.IsTrue(engine.SupportedCategories.Contains(SearchCategory.News));
        Assert.IsFalse(engine.SupportedCategories.Contains(SearchCategory.Images));
        Assert.IsFalse(engine.SupportedCategories.Contains(SearchCategory.Scholar));
        Assert.IsFalse(engine.SupportedCategories.Contains(SearchCategory.Patents));
        Assert.AreEqual("Tavily", engine.EngineName);
    }

    [TestMethod]
    public void NonCategoryEngines_DoNotImplementInterface()
    {
        // These engines should not implement ICategorySearchEngine.
        // The cast check is intentional even though the compiler knows the sealed types.
#pragma warning disable CS0184
        Assert.IsFalse(new BingSearchEngine() is ICategorySearchEngine);
        Assert.IsFalse(new DuckDuckGoSearchEngine() is ICategorySearchEngine);
        Assert.IsFalse(new FallbackSearchEngine(new BingSearchEngine(), new DuckDuckGoSearchEngine()) is ICategorySearchEngine);
#pragma warning restore CS0184
    }

    // --- Helpers ---

    SerpApiSearchEngine CreateSerpApiEngine()
    {
        var apiKey = Environment.GetEnvironmentVariable("SERPAPI_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
            Assert.Inconclusive("SERPAPI_API_KEY not set");
        return new SerpApiSearchEngine(apiKey);
    }
}
