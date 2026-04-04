using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using FieldCure.Mcp.Essentials.Http;
using ModelContextProtocol.Server;
using ReverseMarkdown;
using SmartReader;

namespace FieldCure.Mcp.Essentials.Tools;

/// <summary>
/// MCP tool that fetches a web page and extracts its content as Markdown.
/// </summary>
[McpServerToolType]
public static class WebFetchTool
{
    /// <summary>
    /// JSON serialization options shared across all responses.
    /// </summary>
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Shared HTTP client for fetching web pages.
    /// </summary>
    static readonly HttpClient SharedClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30),
        DefaultRequestHeaders =
        {
            { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36" },
        },
    };

    /// <summary>
    /// Converts cleaned HTML to Markdown.
    /// </summary>
    static readonly Converter MdConverter = new(new ReverseMarkdown.Config
    {
        UnknownTags = Config.UnknownTagsOption.Bypass,
        SmartHrefHandling = true,
        GithubFlavored = true,
    });

    const int DefaultMaxLength = 5000;
    const int AbsoluteMaxLength = 20000;

    /// <summary>
    /// Fetches a URL and extracts its main content as Markdown.
    /// </summary>
    [McpServerTool(Name = "web_fetch")]
    [Description("Fetch a web page URL and extract its main content as Markdown (headings, links, tables, code blocks preserved). Use this to read articles, documentation, or any web page.")]
    public static async Task<string> WebFetch(
        [Description("URL to fetch (http or https)")]
        string url,
        [Description("Maximum length of returned text in characters (default: 5000, max: 20000)")]
        int max_length = DefaultMaxLength,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var (uri, urlError) = SsrfGuard.ValidateUrl(url);
            if (uri is null)
                return JsonSerializer.Serialize(new { error = urlError }, JsonOptions);

            var ssrfError = await SsrfGuard.CheckAsync(uri, cancellationToken);
            if (ssrfError is not null)
                return JsonSerializer.Serialize(new { error = ssrfError }, JsonOptions);

            max_length = Math.Clamp(max_length, 100, AbsoluteMaxLength);

            using var response = await SharedClient.GetAsync(uri, cancellationToken);
            response.EnsureSuccessStatusCode();

            var html = await response.Content.ReadAsStringAsync(cancellationToken);

            var reader = new Reader(url, html);
            var article = await reader.GetArticleAsync();

            string text;
            string? title = null;

            if (article.IsReadable)
            {
                title = article.Title;
                var cleanHtml = article.Content ?? "";
                text = MdConverter.Convert(cleanHtml).Trim();
            }
            else
            {
                // Fallback: strip HTML tags roughly (Markdown conversion not possible)
                text = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ");
                text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();
            }

            var truncated = text.Length > max_length;
            if (truncated)
                text = text[..max_length];

            return JsonSerializer.Serialize(new
            {
                Url = url,
                Title = title,
                Content = text,
                Length = text.Length,
                Truncated = truncated ? true : (bool?)null,
            }, JsonOptions);
        }
        catch (OperationCanceledException)
        {
            return JsonSerializer.Serialize(new { error = "Request timed out." }, JsonOptions);
        }
        catch (HttpRequestException ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }
}
