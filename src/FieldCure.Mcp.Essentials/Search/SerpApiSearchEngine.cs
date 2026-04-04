using System.Text.Json;

namespace FieldCure.Mcp.Essentials.Search;

/// <summary>
/// Google search via SerpApi. Requires API key.
/// Supports 80+ search engines including Google Scholar and Patents.
/// Free: 100 searches/month. Paid: from $75/month.
/// </summary>
public sealed class SerpApiSearchEngine : ISearchEngine
{
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    private readonly string _apiKey;

    public SerpApiSearchEngine(string apiKey) => _apiKey = apiKey;

    /// <inheritdoc />
    public async Task<SearchResult[]> SearchAsync(
        string query, int maxResults, string? region = null, CancellationToken ct = default)
    {
        var url = $"https://serpapi.com/search?engine=google&q={Uri.EscapeDataString(query)}&api_key={_apiKey}&num={maxResults}";

        if (TryParseRegion(region, out var lang, out var country))
            url += $"&hl={lang}&gl={country}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await Http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        return ParseResults(json);
    }

    /// <summary>
    /// Parses SerpApi JSON response into search results.
    /// </summary>
    static SearchResult[] ParseResults(string json)
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
