using AngleSharp.Html.Parser;

namespace FieldCure.Mcp.Essentials.Search;

/// <summary>
/// Searches the web using DuckDuckGo Lite (HTML scraping).
/// </summary>
public sealed class DuckDuckGoSearchEngine : ISearchEngine
{
    /// <summary>
    /// Shared HTTP client for DuckDuckGo requests.
    /// </summary>
    static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(15),
        DefaultRequestHeaders =
        {
            { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36" },
            { "Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8" },
            { "Accept-Language", "en-US,en;q=0.9" },
        },
    };

    /// <summary>
    /// Maps BCP 47 region codes to DuckDuckGo <c>kl</c> parameter values.
    /// </summary>
    static readonly Dictionary<string, string> RegionMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ko-kr"] = "kr-ko",
        ["en-us"] = "us-en",
        ["ja-jp"] = "jp-ja",
        ["zh-cn"] = "cn-zh",
        ["de-de"] = "de-de",
        ["fr-fr"] = "fr-fr",
    };

    /// <inheritdoc />
    public async Task<SearchResult[]> SearchAsync(
        string query, int maxResults, string? region = null, CancellationToken ct = default)
    {
        var form = new Dictionary<string, string> { ["q"] = query };

        if (region is not null && RegionMap.TryGetValue(region, out var kl))
            form["kl"] = kl;

        var content = new FormUrlEncodedContent(form);

        using var response = await Http.PostAsync("https://lite.duckduckgo.com/lite/", content, ct);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(ct);
        var parser = new HtmlParser();
        using var doc = await parser.ParseDocumentAsync(html, ct);

        var results = new List<SearchResult>();

        var links = doc.QuerySelectorAll("a.result-link");
        var snippets = doc.QuerySelectorAll("td.result-snippet");

        var count = Math.Min(links.Length, Math.Min(snippets.Length, maxResults));

        for (var i = 0; i < count; i++)
        {
            var link = links[i];
            var href = link.GetAttribute("href") ?? "";
            var title = link.TextContent.Trim();
            var snippet = snippets[i].TextContent.Trim();

            if (string.IsNullOrEmpty(href) || string.IsNullOrEmpty(title))
                continue;

            results.Add(new SearchResult(title, href, snippet));
        }

        return results.ToArray();
    }
}
