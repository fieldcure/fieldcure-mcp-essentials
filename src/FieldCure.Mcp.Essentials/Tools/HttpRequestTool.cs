using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using FieldCure.Mcp.Essentials.Http;
using ModelContextProtocol.Server;

namespace FieldCure.Mcp.Essentials.Tools;

/// <summary>
/// MCP tool that makes HTTP requests with SSRF protection.
/// </summary>
[McpServerToolType]
public static class HttpRequestTool
{
    /// <summary>
    /// Shared HTTP client with no built-in timeout (per-request timeout used instead).
    /// </summary>
    static readonly HttpClient SharedClient = new()
    {
        Timeout = Timeout.InfiniteTimeSpan,
    };

    /// <summary>
    /// Maximum response body size (1 MB).
    /// </summary>
    const int MaxResponseBytes = 1_048_576;

    /// <summary>
    /// Executes an HTTP request and returns the response as JSON.
    /// </summary>
    [McpServerTool(Name = "http_request")]
    [Description("Make HTTP requests (GET, POST, PUT, DELETE, PATCH, HEAD). Supports custom headers and request body. Returns status code, headers, and response body.")]
    public static async Task<string> HttpRequest(
        [Description("Request URL (http or https)")]
        string url,
        [Description("HTTP method: GET, POST, PUT, DELETE, PATCH, HEAD (default: GET)")]
        string? method = "GET",
        [Description("Request headers as JSON object, e.g. {\"Authorization\": \"Bearer ...\"}")]
        string? headers = null,
        [Description("Request body text")]
        string? body = null,
        [Description("Timeout in seconds (default: 30, max: 120)")]
        int timeout_seconds = 30,
        [Description("Maximum characters of response body to return. "
            + "RECOMMENDED for HTML/text responses when you only need a portion (e.g., 3000-5000). "
            + "AVOID for JSON API responses — truncated JSON cannot be parsed. "
            + "Default: unlimited (up to 1MB).")]
        int? max_response_chars = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var (uri, urlError) = SsrfGuard.ValidateUrl(url);
            if (uri is null)
                return JsonSerializer.Serialize(new { error = urlError }, McpJson.Options);

            // SSRF guard: resolve DNS and block private IPs
            var ssrfError = await SsrfGuard.CheckAsync(uri, cancellationToken);
            if (ssrfError is not null)
                return JsonSerializer.Serialize(new { error = ssrfError }, McpJson.Options);

            timeout_seconds = Math.Clamp(timeout_seconds, 1, 120);
            var httpMethod = new HttpMethod(method?.ToUpperInvariant() ?? "GET");

            using var request = new HttpRequestMessage(httpMethod, uri);

            Dictionary<string, string>? headerDict = null;
            if (headers is not null)
            {
                try
                {
                    headerDict = JsonSerializer.Deserialize<Dictionary<string, string>>(headers);
                }
                catch (JsonException)
                {
                    return JsonSerializer.Serialize(new { error = "Invalid headers JSON." }, McpJson.Options);
                }

                if (headerDict is not null)
                {
                    foreach (var (key, value) in headerDict)
                    {
                        if (key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                            continue; // handled via content
                        request.Headers.TryAddWithoutValidation(key, value);
                    }
                }
            }

            if (body is not null)
            {
                var contentType = "application/json";
                if (headerDict?.TryGetValue("Content-Type", out var ct) == true)
                    contentType = ct;
                request.Content = new StringContent(body, Encoding.UTF8, contentType);
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(timeout_seconds));

            var sw = Stopwatch.StartNew();
            using var response = await SharedClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            sw.Stop();

            var responseHeaders = new Dictionary<string, string>();
            foreach (var h in response.Headers.Concat(response.Content.Headers))
                responseHeaders[h.Key] = string.Join(", ", h.Value);

            var stream = await response.Content.ReadAsStreamAsync(cts.Token);
            var buffer = new byte[MaxResponseBytes];
            var totalRead = 0;
            int read;
            while (totalRead < MaxResponseBytes &&
                   (read = await stream.ReadAsync(buffer.AsMemory(totalRead, MaxResponseBytes - totalRead), cts.Token)) > 0)
            {
                totalRead += read;
            }

            var truncated = totalRead >= MaxResponseBytes;
            var bodyText = Encoding.UTF8.GetString(buffer, 0, totalRead);

            // Apply user-specified character limit (finer-grained than the 1MB byte limit)
            if (max_response_chars is > 0 && bodyText.Length > max_response_chars.Value)
            {
                var limit = max_response_chars.Value;
                // Avoid splitting a UTF-16 surrogate pair (emojis, some CJK, etc.)
                if (limit > 0 && char.IsHighSurrogate(bodyText[limit - 1]))
                    limit--;

                var remaining = bodyText.Length - limit;
                bodyText = bodyText[..limit]
                    + $"\n\n[Truncated: {remaining:N0} more chars omitted. "
                    + "Use a smaller max_response_chars or fetch a more specific URL.]";
                truncated = true;
            }

            var result = new
            {
                StatusCode = (int)response.StatusCode,
                Headers = responseHeaders,
                Body = bodyText,
                ElapsedMs = sw.ElapsedMilliseconds,
                Truncated = truncated ? (bool?)true : null,
                MaxResponseChars = max_response_chars, // null → omitted by WhenWritingNull
            };

            return JsonSerializer.Serialize(result, McpJson.Options);
        }
        catch (OperationCanceledException)
        {
            return JsonSerializer.Serialize(new { error = $"Request timed out after {timeout_seconds}s." }, McpJson.Options);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("SSRF"))
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, McpJson.Options);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, McpJson.Options);
        }
    }

}
