using System.Text.Json;

namespace FieldCure.Mcp.Essentials.Search;

/// <summary>
/// AI-optimized search via Tavily API. Returns content snippets with results.
/// Free: 1,000 credits/month. Paid: $0.008/credit.
/// </summary>
public sealed class TavilySearchEngine : ISearchEngine
{
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    private readonly string _apiKey;

    public TavilySearchEngine(string apiKey) => _apiKey = apiKey;

    /// <inheritdoc />
    public async Task<SearchResult[]> SearchAsync(
        string query, int maxResults, string? region = null, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object>
        {
            ["api_key"] = _apiKey,
            ["query"] = query,
            ["max_results"] = maxResults,
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.tavily.com/search")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json"),
        };

        using var response = await Http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        return ParseResults(json);
    }

    /// <summary>
    /// Parses Tavily JSON response into search results.
    /// </summary>
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
}
