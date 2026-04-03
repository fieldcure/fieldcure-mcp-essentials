using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FieldCure.Mcp.Essentials.Http;
using ModelContextProtocol.Server;

namespace FieldCure.Mcp.Essentials.Tools;

[McpServerToolType]
public static class HttpRequestTool
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    static readonly HttpClient SharedClient = new()
    {
        Timeout = Timeout.InfiniteTimeSpan, // per-request timeout below
    };

    const int MaxResponseBytes = 1_048_576; // 1 MB

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
        CancellationToken cancellationToken = default)
    {
        try
        {
            var (uri, urlError) = SsrfGuard.ValidateUrl(url);
            if (uri is null)
                return JsonSerializer.Serialize(new { error = urlError }, JsonOptions);

            // SSRF guard: resolve DNS and block private IPs
            var ssrfError = await SsrfGuard.CheckAsync(uri, cancellationToken);
            if (ssrfError is not null)
                return JsonSerializer.Serialize(new { error = ssrfError }, JsonOptions);

            timeout_seconds = Math.Clamp(timeout_seconds, 1, 120);
            var httpMethod = new HttpMethod(method?.ToUpperInvariant() ?? "GET");

            using var request = new HttpRequestMessage(httpMethod, uri);

            if (headers is not null)
            {
                try
                {
                    var headerDict = JsonSerializer.Deserialize<Dictionary<string, string>>(headers);
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
                catch (JsonException)
                {
                    return JsonSerializer.Serialize(new { error = "Invalid headers JSON." }, JsonOptions);
                }
            }

            if (body is not null)
            {
                var contentType = "application/json";
                if (headers is not null)
                {
                    var headerDict = JsonSerializer.Deserialize<Dictionary<string, string>>(headers);
                    if (headerDict?.TryGetValue("Content-Type", out var ct) == true)
                        contentType = ct;
                }
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

            var result = new
            {
                StatusCode = (int)response.StatusCode,
                Headers = responseHeaders,
                Body = bodyText,
                ElapsedMs = sw.ElapsedMilliseconds,
                Truncated = truncated ? true : (bool?)null,
            };

            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (OperationCanceledException)
        {
            return JsonSerializer.Serialize(new { error = $"Request timed out after {timeout_seconds}s." }, JsonOptions);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("SSRF"))
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

}
