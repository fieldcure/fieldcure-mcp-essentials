using AngleSharp.Html.Parser;

namespace FieldCure.Mcp.Essentials.Search;

public sealed class BingSearchEngine : ISearchEngine
{
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

    public async Task<SearchResult[]> SearchAsync(string query, int maxResults, CancellationToken ct = default)
    {
        var url = $"https://www.bing.com/search?q={Uri.EscapeDataString(query)}";

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

        return results.ToArray();
    }
}
