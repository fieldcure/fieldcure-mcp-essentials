using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
                || (uri.Scheme != "http" && uri.Scheme != "https"))
            {
                return JsonSerializer.Serialize(new { error = "Invalid URL. Only http:// and https:// are allowed." }, JsonOptions);
            }

            // SSRF guard: resolve DNS and block private IPs
            var ssrfError = await CheckSsrf(uri, cancellationToken);
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

    /// <summary>
    /// SSRF guard: resolves DNS and blocks connections to private/loopback IPs.
    /// Returns error message if blocked, null if allowed.
    /// </summary>
    static async Task<string?> CheckSsrf(Uri uri, CancellationToken ct)
    {
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(uri.Host, ct);
            foreach (var addr in addresses)
            {
                if (IPAddress.IsLoopback(addr)
                    || (addr.AddressFamily == AddressFamily.InterNetworkV6 && addr.IsIPv6LinkLocal)
                    || (addr.AddressFamily == AddressFamily.InterNetworkV6 && addr.IsIPv6SiteLocal)
                    || IsPrivateIpv4(addr))
                {
                    return $"SSRF blocked: {uri.Host} resolves to private address {addr}.";
                }
            }
        }
        catch (SocketException ex)
        {
            return $"DNS resolution failed for {uri.Host}: {ex.Message}";
        }

        return null;
    }

    static bool IsPrivateIpv4(IPAddress addr)
    {
        if (addr.AddressFamily != AddressFamily.InterNetwork) return false;
        var bytes = addr.GetAddressBytes();
        return bytes[0] == 10
               || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
               || (bytes[0] == 192 && bytes[1] == 168)
               || (bytes[0] == 169 && bytes[1] == 254); // link-local
    }
}
