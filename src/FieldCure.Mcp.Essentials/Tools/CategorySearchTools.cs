using System.ComponentModel;
using System.Text.Json;
using FieldCure.Mcp.Essentials.Search;
using FieldCure.Mcp.Essentials.Services;
using ModelContextProtocol.Server;

namespace FieldCure.Mcp.Essentials.Tools;

/// <summary>
/// Handler methods for category search MCP tools.
/// These are NOT auto-discovered (no [McpServerToolType]).
/// Registered programmatically via McpServerTool.Create() in Program.cs.
/// </summary>
public static class CategorySearchTools
{
    /// <summary>
    /// Searches recent news articles.
    /// </summary>
    public static async Task<string> SearchNews(
        McpServer? server,
        ICategorySearchEngine engine,
        [Description("News search query")]
        string query,
        [Description("Time range filter: '1d' (past day), '1w' (past week), '1m' (past month)")]
        string? time_range = null,
        [Description("Maximum number of results to return (default: 10)")]
        int max_results = 10,
        [Description("Region code for localized results (e.g. 'ko-kr', 'en-us')")]
        string? region = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(server, engine, new CategorySearchRequest
        {
            Category = SearchCategory.News,
            Query = query,
            MaxResults = Math.Clamp(max_results, 1, 20),
            Region = region,
            TimeRange = time_range,
        }, cancellationToken);
    }

    /// <summary>
    /// Searches images.
    /// </summary>
    public static async Task<string> SearchImages(
        McpServer? server,
        ICategorySearchEngine engine,
        [Description("Image search query")]
        string query,
        [Description("Filter by image size: 'large', 'medium', 'icon'")]
        string? image_size = null,
        [Description("Filter by image type: 'photo', 'clipart', 'lineart'")]
        string? image_type = null,
        [Description("Maximum number of results to return (default: 10)")]
        int max_results = 10,
        [Description("Region code for localized results (e.g. 'ko-kr', 'en-us')")]
        string? region = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(server, engine, new CategorySearchRequest
        {
            Category = SearchCategory.Images,
            Query = query,
            MaxResults = Math.Clamp(max_results, 1, 20),
            Region = region,
            ImageSize = image_size,
            ImageType = image_type,
        }, cancellationToken);
    }

    /// <summary>
    /// Searches academic papers.
    /// </summary>
    public static async Task<string> SearchScholar(
        McpServer? server,
        ICategorySearchEngine engine,
        [Description("Academic search query")]
        string query,
        [Description("Filter by author name")]
        string? author = null,
        [Description("Find papers citing this paper ID")]
        string? cited_by = null,
        [Description("Maximum number of results to return (default: 10)")]
        int max_results = 10,
        [Description("Region code for localized results (e.g. 'ko-kr', 'en-us')")]
        string? region = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(server, engine, new CategorySearchRequest
        {
            Category = SearchCategory.Scholar,
            Query = query,
            MaxResults = Math.Clamp(max_results, 1, 20),
            Region = region,
            Author = author,
            CitedBy = cited_by,
        }, cancellationToken);
    }

    /// <summary>
    /// Searches patent documents.
    /// </summary>
    public static async Task<string> SearchPatents(
        McpServer? server,
        ICategorySearchEngine engine,
        [Description("Patent search query")]
        string query,
        [Description("Filter by inventor name")]
        string? inventor = null,
        [Description("Filter by assignee/company name")]
        string? assignee = null,
        [Description("Priority date range (e.g., '2020-01-01:2024-12-31')")]
        string? date_range = null,
        [Description("Maximum number of results to return (default: 10)")]
        int max_results = 10,
        [Description("Region code for localized results (e.g. 'ko-kr', 'en-us')")]
        string? region = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(server, engine, new CategorySearchRequest
        {
            Category = SearchCategory.Patents,
            Query = query,
            MaxResults = Math.Clamp(max_results, 1, 20),
            Region = region,
            Inventor = inventor,
            Assignee = assignee,
            DateRange = date_range,
        }, cancellationToken);
    }

    /// <summary>
    /// Executes a category search and serializes the result into the shared MCP
    /// JSON response shape used by all category tools.
    /// </summary>
    /// <param name="server">The active MCP server instance, or <see langword="null"/> for direct calls.</param>
    /// <param name="engine">The injected category-capable search engine.</param>
    /// <param name="request">The normalized category search request.</param>
    /// <param name="ct">Cancellation token for the search operation.</param>
    /// <returns>A JSON payload containing either results or an error.</returns>
    static async Task<string> ExecuteAsync(
        McpServer? server, ICategorySearchEngine engine, CategorySearchRequest request, CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Query))
                return JsonSerializer.Serialize(new { error = "Query must not be empty." }, McpJson.Options);

            var gate = server is null ? null : new McpServerElicitGate(server);

            var result = engine is IMcpAwareCategorySearchEngine mcpAware
                ? await mcpAware.SearchAsync(gate, request, ct)
                : await engine.SearchAsync(request, ct);

            return JsonSerializer.Serialize(new
            {
                engine = result.Engine,
                category = result.Category.ToString().ToLowerInvariant(),
                results = result.Items,
            }, McpJson.Options);
        }
        catch (OperationCanceledException)
        {
            return JsonSerializer.Serialize(new { error = "Search request timed out." }, McpJson.Options);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, McpJson.Options);
        }
    }
}
