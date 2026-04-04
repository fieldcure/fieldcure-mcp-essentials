using AngleSharp.Html.Parser;

namespace FieldCure.Mcp.Essentials.Search;

/// <summary>
/// Searches the web using Bing (HTML scraping).
/// </summary>
public sealed class BingSearchEngine : ISearchEngine
{
    /// <summary>
    /// Shared HTTP client for Bing requests.
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
    /// Enforces minimum delay between requests to avoid rate-limiting.
    /// </summary>
    static readonly RequestThrottle Throttle = new(TimeSpan.FromSeconds(2));

    /// <inheritdoc />
    public async Task<SearchResult[]> SearchAsync(
        string query, int maxResults, string? region = null, CancellationToken ct = default)
    {
        await Throttle.WaitAsync(ct);

        var url = $"https://www.bing.com/search?q={Uri.EscapeDataString(query)}";

        if (TryParseRegion(region, out var lang, out var cc))
            url += $"&setLang={lang}&cc={cc}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await Http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(ct);
        var parser = new HtmlParser();
        using var doc = await parser.ParseDocumentAsync(html, ct);

        var results = new List<SearchResult>();

        var items = doc.QuerySelectorAll("li.b_algo");

        foreach (var item in items)
        {
            if (results.Count >= maxResults)
                break;

            var anchor = item.QuerySelector("h2 > a");
            if (anchor is null) continue;

            var href = anchor.GetAttribute("href") ?? "";
            var title = anchor.TextContent.Trim();

            var snippetEl = item.QuerySelector("p.b_lineclamp2, p.b_lineclamp3, p.b_lineclamp4, div.b_caption p");
            var snippet = snippetEl?.TextContent.Trim() ?? "";

            if (string.IsNullOrEmpty(href) || string.IsNullOrEmpty(title))
                continue;

            results.Add(new SearchResult(title, href, snippet));
        }

        return [.. results];
    }

    /// <summary>
    /// Parses a BCP 47 region code (e.g. "ko-kr") into language and country parts.
    /// </summary>
    static bool TryParseRegion(string? region, out string lang, out string cc)
    {
        lang = "";
        cc = "";

        if (string.IsNullOrWhiteSpace(region))
            return false;

        var parts = region.Split('-');
        if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0)
            return false;

        lang = parts[0].ToLowerInvariant();
        cc = parts[1].ToUpperInvariant();
        return true;
    }
}
