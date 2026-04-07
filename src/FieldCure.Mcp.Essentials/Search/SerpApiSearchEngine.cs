using System.Text.Json;

namespace FieldCure.Mcp.Essentials.Search;

/// <summary>
/// Google search via SerpApi. Requires API key.
/// Supports 80+ search engines including Google Scholar and Patents.
/// Free: 100 searches/month. Paid: from $75/month.
/// </summary>
public sealed class SerpApiSearchEngine : ISearchEngine, ICategorySearchEngine
{
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    private readonly string _apiKey;

    public SerpApiSearchEngine(string apiKey) => _apiKey = apiKey;

    /// <inheritdoc />
    public string EngineName => "SerpApi";

    /// <inheritdoc />
    public IReadOnlySet<SearchCategory> SupportedCategories { get; } =
        new HashSet<SearchCategory>
        {
            SearchCategory.News,
            SearchCategory.Images,
            SearchCategory.Scholar,
            SearchCategory.Patents,
        };

    /// <inheritdoc />
    public async Task<SearchResult[]> SearchAsync(
        string query, int maxResults, string? region = null, CancellationToken ct = default)
    {
        var url = $"https://serpapi.com/search?engine=google&q={Uri.EscapeDataString(query)}&api_key={_apiKey}";

        if (TryParseRegion(region, out var lang, out var country))
            url += $"&hl={lang}&gl={country}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await Http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        var results = ParseOrganicResults(json);
        return results.Length <= maxResults ? results : results[..maxResults];
    }

    /// <inheritdoc />
    public async Task<CategorySearchResult> SearchAsync(
        CategorySearchRequest request, CancellationToken ct = default)
    {
        var engine = request.Category switch
        {
            SearchCategory.News => "google_news",
            SearchCategory.Images => "google_images",
            SearchCategory.Scholar => "google_scholar",
            SearchCategory.Patents => "google_patents",
            _ => throw new NotSupportedException($"Category {request.Category} is not supported."),
        };

        var url = $"https://serpapi.com/search?engine={engine}&q={Uri.EscapeDataString(request.Query)}&api_key={_apiKey}";

        if (TryParseRegion(request.Region, out var lang, out var country))
            url += $"&hl={lang}&gl={country}";

        // Category-specific parameters
        switch (request.Category)
        {
            case SearchCategory.Scholar:
                if (request.CitedBy is not null)
                    url += $"&cites={Uri.EscapeDataString(request.CitedBy)}";
                if (request.Author is not null)
                    url = url.Replace($"q={Uri.EscapeDataString(request.Query)}",
                        $"q={Uri.EscapeDataString($"author:{request.Author} {request.Query}")}");
                url += $"&num={request.MaxResults}";
                break;

            case SearchCategory.Patents:
                if (request.Inventor is not null)
                    url += $"&inventor={Uri.EscapeDataString(request.Inventor)}";
                if (request.Assignee is not null)
                    url += $"&assignee={Uri.EscapeDataString(request.Assignee)}";
                if (request.DateRange is not null)
                {
                    // Format: "2020-01-01:2024-12-31" → after=priority:20200101&before=priority:20241231
                    var parts = request.DateRange.Split(':');
                    if (parts.Length == 2)
                    {
                        var after = parts[0].Replace("-", "");
                        var before = parts[1].Replace("-", "");
                        url += $"&after=priority:{after}&before=priority:{before}";
                    }
                }
                url += $"&num={Math.Clamp(request.MaxResults, 10, 100)}";
                break;

            case SearchCategory.Images:
                var tbs = new List<string>();
                if (request.ImageSize is not null)
                {
                    var size = request.ImageSize.ToLowerInvariant() switch
                    {
                        "large" => "isz:l",
                        "medium" => "isz:m",
                        "icon" => "isz:i",
                        _ => null,
                    };
                    if (size is not null) tbs.Add(size);
                }
                if (request.ImageType is not null)
                {
                    var type = request.ImageType.ToLowerInvariant() switch
                    {
                        "photo" => "itp:photos",
                        "clipart" => "itp:clipart",
                        "lineart" => "itp:lineart",
                        _ => null,
                    };
                    if (type is not null) tbs.Add(type);
                }
                if (tbs.Count > 0)
                    url += $"&tbs={string.Join(",", tbs)}";
                break;

            case SearchCategory.News:
                // SerpApi Google News doesn't support num; results are returned as-is
                break;
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await Http.SendAsync(httpRequest, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        var items = request.Category switch
        {
            SearchCategory.News => ParseNewsResults(json),
            SearchCategory.Images => ParseImageResults(json),
            SearchCategory.Scholar => ParseScholarResults(json),
            SearchCategory.Patents => ParsePatentResults(json),
            _ => [],
        };

        var limited = items.Count <= request.MaxResults ? items : items.Take(request.MaxResults).ToList();

        return new CategorySearchResult
        {
            Category = request.Category,
            Engine = EngineName,
            Items = limited,
        };
    }

    // --- Parsers ---

    static SearchResult[] ParseOrganicResults(string json)
    {
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("organic_results", out var organic))
            return [];

        var results = new List<SearchResult>();
        foreach (var item in organic.EnumerateArray())
        {
            var title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
            var link = item.TryGetProperty("link", out var l) ? l.GetString() ?? "" : "";
            var snippet = item.TryGetProperty("snippet", out var s) ? s.GetString() ?? "" : "";

            if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(link))
                results.Add(new SearchResult(title, link, snippet));
        }

        return [.. results];
    }

    static List<CategorySearchResultItem> ParseNewsResults(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var items = new List<CategorySearchResultItem>();

        if (!doc.RootElement.TryGetProperty("news_results", out var results))
            return items;

        foreach (var item in results.EnumerateArray())
        {
            var title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
            var link = item.TryGetProperty("link", out var l) ? l.GetString() ?? "" : "";
            if (string.IsNullOrEmpty(title)) continue;

            string? source = null;
            if (item.TryGetProperty("source", out var src))
            {
                if (src.ValueKind == JsonValueKind.Object)
                    source = src.TryGetProperty("name", out var n) ? n.GetString() : null;
                else if (src.ValueKind == JsonValueKind.String)
                    source = src.GetString();
            }

            items.Add(new CategorySearchResultItem
            {
                Title = title,
                Url = link,
                Snippet = item.TryGetProperty("snippet", out var s) ? s.GetString() : null,
                PublishedDate = item.TryGetProperty("date", out var d) ? d.GetString() : null,
                Source = source,
            });
        }

        return items;
    }

    static List<CategorySearchResultItem> ParseImageResults(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var items = new List<CategorySearchResultItem>();

        if (!doc.RootElement.TryGetProperty("images_results", out var results))
            return items;

        foreach (var item in results.EnumerateArray())
        {
            var title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
            if (string.IsNullOrEmpty(title)) continue;

            items.Add(new CategorySearchResultItem
            {
                Title = title,
                Url = item.TryGetProperty("link", out var l) ? l.GetString() : null,
                ImageUrl = item.TryGetProperty("original", out var o) ? o.GetString() : null,
                ThumbnailUrl = item.TryGetProperty("thumbnail", out var th) ? th.GetString() : null,
                Width = item.TryGetProperty("original_width", out var w) ? w.GetInt32() : null,
                Height = item.TryGetProperty("original_height", out var h) ? h.GetInt32() : null,
                Source = item.TryGetProperty("source", out var src) ? src.GetString() : null,
            });
        }

        return items;
    }

    static List<CategorySearchResultItem> ParseScholarResults(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var items = new List<CategorySearchResultItem>();

        if (!doc.RootElement.TryGetProperty("organic_results", out var results))
            return items;

        foreach (var item in results.EnumerateArray())
        {
            var title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
            if (string.IsNullOrEmpty(title)) continue;

            // Parse citation count from inline_links.cited_by.total
            int? citationCount = null;
            if (item.TryGetProperty("inline_links", out var links) &&
                links.TryGetProperty("cited_by", out var citedBy) &&
                citedBy.TryGetProperty("total", out var total))
            {
                citationCount = total.GetInt32();
            }

            // Parse publication info summary for authors/year/journal
            string? pubInfo = null;
            if (item.TryGetProperty("publication_info", out var pub) &&
                pub.TryGetProperty("summary", out var summary))
            {
                pubInfo = summary.GetString();
            }

            items.Add(new CategorySearchResultItem
            {
                Title = title,
                Url = item.TryGetProperty("link", out var l) ? l.GetString() : null,
                Snippet = item.TryGetProperty("snippet", out var s) ? s.GetString() : null,
                CitationCount = citationCount,
                Journal = pubInfo, // publication_info.summary contains "authors - journal, year"
            });
        }

        return items;
    }

    static List<CategorySearchResultItem> ParsePatentResults(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var items = new List<CategorySearchResultItem>();

        if (!doc.RootElement.TryGetProperty("organic_results", out var results))
            return items;

        foreach (var item in results.EnumerateArray())
        {
            var title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
            if (string.IsNullOrEmpty(title)) continue;

            items.Add(new CategorySearchResultItem
            {
                Title = title,
                Url = item.TryGetProperty("patent_link", out var pl) ? pl.GetString()
                    : item.TryGetProperty("link", out var l) ? l.GetString() : null,
                Snippet = item.TryGetProperty("snippet", out var s) ? s.GetString() : null,
                PatentId = item.TryGetProperty("patent_id", out var pid) ? pid.GetString() : null,
                Inventor = item.TryGetProperty("inventor", out var inv) ? inv.GetString() : null,
                Assignee = item.TryGetProperty("assignee", out var asg) ? asg.GetString() : null,
                PriorityDate = item.TryGetProperty("priority_date", out var pd) ? pd.GetString() : null,
                FilingDate = item.TryGetProperty("filing_date", out var fd) ? fd.GetString() : null,
            });
        }

        return items;
    }

    /// <summary>
    /// Splits a BCP 47 region code into language and country for SerpApi gl/hl params.
    /// </summary>
    static bool TryParseRegion(string? region, out string lang, out string country)
    {
        lang = "";
        country = "";

        if (string.IsNullOrWhiteSpace(region))
            return false;

        var parts = region.Split('-');
        if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0)
            return false;

        lang = parts[0].ToLowerInvariant();
        country = parts[1].ToLowerInvariant();
        return true;
    }
}
