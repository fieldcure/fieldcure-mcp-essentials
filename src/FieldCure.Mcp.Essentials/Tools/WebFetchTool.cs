using System.ComponentModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using FieldCure.Mcp.Essentials.Http;
using ModelContextProtocol.Server;
using ReverseMarkdown;
using SmartReader;

namespace FieldCure.Mcp.Essentials.Tools;

/// <summary>
/// MCP tool that fetches a web page and extracts its content as Markdown.
/// Supports HTML pages and binary documents (PDF, DOCX, HWPX, PPTX, XLSX).
/// </summary>
[McpServerToolType]
public static class WebFetchTool
{
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

    /// <summary>Default maximum character length for returned content.</summary>
    const int DefaultMaxLength = 5000;

    /// <summary>Absolute upper bound for max_length parameter.</summary>
    const int AbsoluteMaxLength = 20000;

    /// <summary>
    /// Fetches a URL and extracts its main content as Markdown.
    /// Binary documents (PDF, DOCX, HWPX, PPTX, XLSX) are parsed via Content-Type
    /// with URL extension fallback for formats without standard Content-Type (e.g. HWPX).
    /// </summary>
    [McpServerTool(Name = "web_fetch")]
    [Description("Fetch a URL and extract content as Markdown. Supports web pages (HTML) and documents (PDF, DOCX, HWPX, PPTX, XLSX).")]
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
                return JsonSerializer.Serialize(new { error = urlError }, McpJson.Options);

            var ssrfError = await SsrfGuard.CheckAsync(uri, cancellationToken);
            if (ssrfError is not null)
                return JsonSerializer.Serialize(new { error = ssrfError }, McpJson.Options);

            max_length = Math.Clamp(max_length, 100, AbsoluteMaxLength);

            using var response = await SharedClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            // Route by Content-Type, with URL extension fallback (for HWPX etc.)
            var contentType = response.Content.Headers.ContentType?.MediaType;
            var docExtension = DocumentHelper.ContentTypeToExtension(contentType)
                ?? DocumentHelper.UrlToExtension(url);

            if (docExtension is not null)
                return await HandleDocument(response, url, contentType, docExtension, max_length, cancellationToken);

            return await HandleHtml(response, url, max_length, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return JsonSerializer.Serialize(new { error = "Request timed out." }, McpJson.Options);
        }
        catch (HttpRequestException ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, McpJson.Options);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, McpJson.Options);
        }
    }

    /// <summary>
    /// Reads the response as binary bytes and parses the document into Markdown.
    /// </summary>
    static async Task<string> HandleDocument(
        HttpResponseMessage response, string url, string? contentType, string docExtension,
        int maxLength, CancellationToken ct)
    {
        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        var text = DocumentHelper.Parse(bytes, docExtension);

        var truncated = text.Length > maxLength;
        if (truncated)
            text = text[..maxLength];

        return JsonSerializer.Serialize(new
        {
            Url = url,
            ContentType = contentType,
            Content = text,
            Length = text.Length,
            Truncated = truncated ? true : (bool?)null,
        }, McpJson.Options);
    }

    /// <summary>
    /// Reads the response as HTML, extracts article content via SmartReader,
    /// and converts it to Markdown.
    /// </summary>
    static async Task<string> HandleHtml(
        HttpResponseMessage response, string url, int maxLength, CancellationToken ct)
    {
        var html = await response.Content.ReadAsStringAsync(ct);

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
            text = Regex.Replace(html, "<[^>]+>", " ");
            text = Regex.Replace(text, @"\s+", " ").Trim();
        }

        var truncated = text.Length > maxLength;
        if (truncated)
            text = text[..maxLength];

        return JsonSerializer.Serialize(new
        {
            Url = url,
            Title = title,
            Content = text,
            Length = text.Length,
            Truncated = truncated ? true : (bool?)null,
        }, McpJson.Options);
    }
}
