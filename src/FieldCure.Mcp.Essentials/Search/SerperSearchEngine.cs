using System.Text.Json;

namespace FieldCure.Mcp.Essentials.Search;

/// <summary>
/// Google search via Serper.dev API. Requires API key.
/// Free: 2,500 credits (one-time). Paid: $1/1K queries.
/// </summary>
public sealed class SerperSearchEngine : ISearchEngine, ICategorySearchEngine
{
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    private readonly string _apiKey;

    public SerperSearchEngine(string apiKey) => _apiKey = apiKey;

    /// <inheritdoc />
    public string EngineName => "Serper";

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
        var body = new Dictionary<string, object> { ["q"] = query, ["num"] = maxResults };

        if (TryParseRegion(region, out var lang, out var country))
        {
            body["hl"] = lang;
            body["gl"] = country;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://google.serper.dev/search")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("X-API-KEY", _apiKey);

        using var response = await Http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        return ParseOrganicResults(json);
    }

    /// <inheritdoc />
    public async Task<CategorySearchResult> SearchAsync(
        CategorySearchRequest request, CancellationToken ct = default)
    {
        var endpoint = request.Category switch
        {
            SearchCategory.News => "https://google.serper.dev/news",
            SearchCategory.Images => "https://google.serper.dev/images",
            SearchCategory.Scholar => "https://google.serper.dev/scholar",
            SearchCategory.Patents => "https://google.serper.dev/patents",
            _ => throw new NotSupportedException($"Category {request.Category} is not supported."),
        };

        var body = new Dictionary<string, object>
        {
            ["q"] = request.Query,
            ["num"] = request.MaxResults,
        };

        if (TryParseRegion(request.Region, out var lang, out var country))
        {
            body["hl"] = lang;
            body["gl"] = country;
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json"),
        };
        httpRequest.Headers.TryAddWithoutValidation("X-API-KEY", _apiKey);

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

        return new CategorySearchResult
        {
            Category = request.Category,
            Engine = EngineName,
            Items = items,
        };
    }

    // --- Parsers ---

    static SearchResult[] ParseOrganicResults(string json)
    {
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("organic", out var organic))
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

        if (!doc.RootElement.TryGetProperty("news", out var results))
            return items;

        foreach (var item in results.EnumerateArray())
        {
            var title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
            if (string.IsNullOrEmpty(title)) continue;

            items.Add(new CategorySearchResultItem
            {
                Title = title,
                Url = item.TryGetProperty("link", out var l) ? l.GetString() : null,
                Snippet = item.TryGetProperty("snippet", out var s) ? s.GetString() : null,
                PublishedDate = item.TryGetProperty("date", out var d) ? d.GetString() : null,
                Source = item.TryGetProperty("source", out var src) ? src.GetString() : null,
            });
        }

        return items;
    }

    static List<CategorySearchResultItem> ParseImageResults(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var items = new List<CategorySearchResultItem>();

        if (!doc.RootElement.TryGetProperty("images", out var results))
            return items;

        foreach (var item in results.EnumerateArray())
        {
            var title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
            if (string.IsNullOrEmpty(title)) continue;

            items.Add(new CategorySearchResultItem
            {
                Title = title,
                Url = item.TryGetProperty("link", out var l) ? l.GetString() : null,
                ImageUrl = item.TryGetProperty("imageUrl", out var img) ? img.GetString() : null,
                ThumbnailUrl = item.TryGetProperty("thumbnailUrl", out var th) ? th.GetString() : null,
                Width = item.TryGetProperty("imageWidth", out var w) && w.TryGetInt32(out var wv) ? wv : null,
                Height = item.TryGetProperty("imageHeight", out var h) && h.TryGetInt32(out var hv) ? hv : null,
                Source = item.TryGetProperty("source", out var src) ? src.GetString() : null,
            });
        }

        return items;
    }

    static List<CategorySearchResultItem> ParseScholarResults(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var items = new List<CategorySearchResultItem>();

        if (!doc.RootElement.TryGetProperty("organic", out var results))
            return items;

        foreach (var item in results.EnumerateArray())
        {
            var title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
            if (string.IsNullOrEmpty(title)) continue;

            int? citedBy = null;
            if (item.TryGetProperty("citedBy", out var cb) && cb.TryGetInt32(out var cbv))
                citedBy = cbv;

            items.Add(new CategorySearchResultItem
            {
                Title = title,
                Url = item.TryGetProperty("link", out var l) ? l.GetString() : null,
                Snippet = item.TryGetProperty("snippet", out var s) ? s.GetString() : null,
                CitationCount = citedBy,
                Journal = item.TryGetProperty("publicationInfo", out var pub) ? pub.GetString() : null,
                Year = item.TryGetProperty("year", out var y) ? y.GetInt32() : null,
            });
        }

        return items;
    }

    static List<CategorySearchResultItem> ParsePatentResults(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var items = new List<CategorySearchResultItem>();

        if (!doc.RootElement.TryGetProperty("organic", out var results))
            return items;

        foreach (var item in results.EnumerateArray())
        {
            var title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
            if (string.IsNullOrEmpty(title)) continue;

            items.Add(new CategorySearchResultItem
            {
                Title = title,
                Url = item.TryGetProperty("link", out var l) ? l.GetString() : null,
                Snippet = item.TryGetProperty("snippet", out var s) ? s.GetString() : null,
                PatentId = item.TryGetProperty("patentId", out var pid) ? pid.GetString() : null,
                Inventor = item.TryGetProperty("inventor", out var inv) ? inv.GetString() : null,
                Assignee = item.TryGetProperty("assignee", out var asg) ? asg.GetString() : null,
                PriorityDate = item.TryGetProperty("priorityDate", out var pd) ? pd.GetString() : null,
                FilingDate = item.TryGetProperty("filingDate", out var fd) ? fd.GetString() : null,
            });
        }

        return items;
    }

    /// <summary>
    /// Splits a BCP 47 region code into language and country for Serper gl/hl params.
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
