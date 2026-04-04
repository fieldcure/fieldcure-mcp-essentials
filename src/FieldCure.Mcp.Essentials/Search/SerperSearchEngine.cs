using System.Text.Json;

namespace FieldCure.Mcp.Essentials.Search;

/// <summary>
/// Google search via Serper.dev API. Requires API key.
/// Free: 2,500 credits (one-time). Paid: $1/1K queries.
/// </summary>
public sealed class SerperSearchEngine : ISearchEngine
{
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    private readonly string _apiKey;

    public SerperSearchEngine(string apiKey) => _apiKey = apiKey;

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
        return ParseResults(json);
    }

    /// <summary>
    /// Parses Serper JSON response into search results.
    /// </summary>
    static SearchResult[] ParseResults(string json)
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
