using AngleSharp.Html.Parser;

namespace FieldCure.Mcp.Essentials.Search;

public sealed class DuckDuckGoSearchEngine : ISearchEngine
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
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["q"] = query,
        });

        using var response = await Http.PostAsync("https://lite.duckduckgo.com/lite/", content, ct);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(ct);
        var parser = new HtmlParser();
        using var doc = await parser.ParseDocumentAsync(html, ct);

        var results = new List<SearchResult>();

        // DuckDuckGo lite returns results in a table structure.
        // Each result has: a link row, then a snippet row, then a spacer row.
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
