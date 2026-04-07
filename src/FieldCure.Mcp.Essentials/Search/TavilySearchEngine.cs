using System.Text.Json;

namespace FieldCure.Mcp.Essentials.Search;

/// <summary>
/// AI-optimized search via Tavily API. Returns content snippets with results.
/// Free: 1,000 credits/month. Paid: $0.008/credit.
/// </summary>
public sealed class TavilySearchEngine : ISearchEngine, ICategorySearchEngine
{
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    private readonly string _apiKey;

    public TavilySearchEngine(string apiKey) => _apiKey = apiKey;

    /// <inheritdoc />
    public string EngineName => "Tavily";

    /// <inheritdoc />
    public IReadOnlySet<SearchCategory> SupportedCategories { get; } =
        new HashSet<SearchCategory>
        {
            SearchCategory.News,
        };

    /// <inheritdoc />
    public async Task<SearchResult[]> SearchAsync(
        string query, int maxResults, string? region = null, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object>
        {
            ["query"] = query,
            ["max_results"] = maxResults,
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.tavily.com/search")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_apiKey}");

        using var response = await Http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        return ParseResults(json);
    }

    /// <inheritdoc />
    public async Task<CategorySearchResult> SearchAsync(
        CategorySearchRequest request, CancellationToken ct = default)
    {
        if (request.Category != SearchCategory.News)
            throw new NotSupportedException($"Category {request.Category} is not supported by Tavily.");

        var body = new Dictionary<string, object>
        {
            ["query"] = request.Query,
            ["max_results"] = request.MaxResults,
            ["topic"] = "news",
        };

        if (request.TimeRange is not null)
        {
            // Tavily accepts: "day", "week", "month", "year" or "d", "w", "m", "y"
            var timeRange = request.TimeRange.ToLowerInvariant() switch
            {
                "1d" => "day",
                "1w" => "week",
                "1m" => "month",
                _ => request.TimeRange,
            };
            body["time_range"] = timeRange;
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.tavily.com/search")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json"),
        };
        httpRequest.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_apiKey}");

        using var response = await Http.SendAsync(httpRequest, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        var items = ParseNewsResults(json);

        return new CategorySearchResult
        {
            Category = SearchCategory.News,
            Engine = EngineName,
            Items = items,
        };
    }

    // --- Parsers ---

    static SearchResult[] ParseResults(string json)
    {
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("results", out var results))
            return [];

        var list = new List<SearchResult>();
        foreach (var item in results.EnumerateArray())
        {
            var title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
            var url = item.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "";
            var content = item.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "";

            if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(url))
                list.Add(new SearchResult(title, url, content));
        }

        return [.. list];
    }

    static List<CategorySearchResultItem> ParseNewsResults(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var items = new List<CategorySearchResultItem>();

        if (!doc.RootElement.TryGetProperty("results", out var results))
            return items;

        foreach (var item in results.EnumerateArray())
        {
            var title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
            if (string.IsNullOrEmpty(title)) continue;

            items.Add(new CategorySearchResultItem
            {
                Title = title,
                Url = item.TryGetProperty("url", out var u) ? u.GetString() : null,
                Snippet = item.TryGetProperty("content", out var c) ? c.GetString() : null,
                PublishedDate = item.TryGetProperty("published_date", out var d) ? d.GetString() : null,
            });
        }

        return items;
    }
}
