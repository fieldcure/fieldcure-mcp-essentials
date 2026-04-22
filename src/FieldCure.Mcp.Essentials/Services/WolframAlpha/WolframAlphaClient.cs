using System.Net.Http.Json;

namespace FieldCure.Mcp.Essentials.Services.WolframAlpha;

/// <summary>
/// HTTP client for the Wolfram|Alpha Full Results API v2. Uses the
/// <c>appid</c> query-parameter authentication defined by the Full Results
/// API docs and enables <c>reinterpret=true</c> so the API auto-corrects
/// most failed queries without a client round trip.
/// </summary>
public sealed class WolframAlphaClient
{
    const string BaseUrl = "https://api.wolframalpha.com/v2/query";
    const string Format = "plaintext,mathml,image";
    const int DefaultTimeoutSeconds = 8;
    const string DefaultUnits = "metric";
    const string DefaultCountryCode = "KR";

    readonly HttpClient _http;

    public WolframAlphaClient(HttpClient http)
    {
        _http = http;
    }

    /// <summary>
    /// Sends a Full Results query and returns the parsed <see cref="QueryResult"/>.
    /// HTTP errors (including 403 for invalid AppID) surface as
    /// <see cref="HttpRequestException"/> so the caller can invalidate the key
    /// and retry.
    /// </summary>
    public async Task<QueryResult> QueryAsync(
        string appId,
        string input,
        string? assumption = null,
        string? includePodId = null,
        string? units = null,
        string? currency = null,
        string? countryCode = null,
        string? location = null,
        CancellationToken ct = default)
    {
        var url = BuildUrl(appId, input, assumption, includePodId, units, currency, countryCode, location);

        using var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<WolframResponse>(ct);
        return json?.QueryResult ?? throw new HttpRequestException("Empty Wolfram|Alpha response.");
    }

    static string BuildUrl(
        string appId, string input, string? assumption, string? includePodId,
        string? units, string? currency, string? countryCode, string? location)
    {
        var qs = new List<string>(capacity: 12)
        {
            $"appid={Uri.EscapeDataString(appId)}",
            $"input={Uri.EscapeDataString(input)}",
            "output=JSON",
            $"format={Format}",
            "reinterpret=true",
            $"totaltimeout={DefaultTimeoutSeconds}",
            $"units={Uri.EscapeDataString(units ?? DefaultUnits)}",
            $"countrycode={Uri.EscapeDataString(countryCode ?? DefaultCountryCode)}",
        };

        if (!string.IsNullOrWhiteSpace(assumption))
            qs.Add($"assumption={Uri.EscapeDataString(assumption)}");
        if (!string.IsNullOrWhiteSpace(includePodId))
            qs.Add($"includepodid={Uri.EscapeDataString(includePodId)}");
        if (!string.IsNullOrWhiteSpace(currency))
            qs.Add($"currency={Uri.EscapeDataString(currency)}");
        if (!string.IsNullOrWhiteSpace(location))
            qs.Add($"location={Uri.EscapeDataString(location)}");

        return $"{BaseUrl}?{string.Join("&", qs)}";
    }
}
