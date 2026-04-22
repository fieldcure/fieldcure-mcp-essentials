using System.ComponentModel;
using System.Text.Json;
using FieldCure.Mcp.Essentials.Search;
using FieldCure.Mcp.Essentials.Services;
using ModelContextProtocol.Server;

namespace FieldCure.Mcp.Essentials.Tools;

/// <summary>
/// MCP tools for category-specific search (news, images, scholar, patents).
/// All four tools are always registered (superset); the current engine's
/// <see cref="ICategorySearchEngine.SupportedCategories"/> is checked at
/// invocation time so switches via <c>set_search_engine</c> take effect
/// without restarting the server.
/// </summary>
[McpServerToolType]
public static class CategorySearchTools
{
    /// <summary>
    /// Searches recent news articles via the active category-capable engine.
    /// </summary>
    [McpServerTool(Name = "search_news")]
    [Description("Search recent news articles with title, URL, snippet, source, and publication date. Requires a category-capable engine (SerpApi, Serper, or Tavily). Returns an error if the active engine does not support news search — use set_search_engine to switch.")]
    public static Task<string> SearchNews(
        McpServer? server,
        SearchEngineManager engineManager,
        [Description("News search query")]
        string query,
        [Description("Time range filter: '1d' (past day), '1w' (past week), '1m' (past month)")]
        string? time_range = null,
        [Description("Maximum number of results to return (default: 10)")]
        int max_results = 10,
        [Description("Region code for localized results (e.g. 'ko-kr', 'en-us')")]
        string? region = null,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(server, engineManager, new CategorySearchRequest
        {
            Category = SearchCategory.News,
            Query = query,
            MaxResults = Math.Clamp(max_results, 1, 20),
            Region = region,
            TimeRange = time_range,
        }, cancellationToken);

    /// <summary>
    /// Searches images via the active category-capable engine.
    /// </summary>
    [McpServerTool(Name = "search_images")]
    [Description("Search images and return URLs, thumbnails, dimensions, and source info. Requires a category-capable engine (SerpApi or Serper). Returns an error if the active engine does not support image search — use set_search_engine to switch.")]
    public static Task<string> SearchImages(
        McpServer? server,
        SearchEngineManager engineManager,
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
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(server, engineManager, new CategorySearchRequest
        {
            Category = SearchCategory.Images,
            Query = query,
            MaxResults = Math.Clamp(max_results, 1, 20),
            Region = region,
            ImageSize = image_size,
            ImageType = image_type,
        }, cancellationToken);

    /// <summary>
    /// Searches academic papers via the active category-capable engine.
    /// </summary>
    [McpServerTool(Name = "search_scholar")]
    [Description("Search academic papers and return titles, authors, citation counts, and publication details. Requires a category-capable engine (SerpApi or Serper). Returns an error if the active engine does not support scholar search — use set_search_engine to switch.")]
    public static Task<string> SearchScholar(
        McpServer? server,
        SearchEngineManager engineManager,
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
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(server, engineManager, new CategorySearchRequest
        {
            Category = SearchCategory.Scholar,
            Query = query,
            MaxResults = Math.Clamp(max_results, 1, 20),
            Region = region,
            Author = author,
            CitedBy = cited_by,
        }, cancellationToken);

    /// <summary>
    /// Searches patent documents via the active category-capable engine.
    /// </summary>
    [McpServerTool(Name = "search_patents")]
    [Description("Search patent documents for prior art, filings, and IP information. Requires a category-capable engine (SerpApi or Serper). Returns an error if the active engine does not support patent search — use set_search_engine to switch.")]
    public static Task<string> SearchPatents(
        McpServer? server,
        SearchEngineManager engineManager,
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
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(server, engineManager, new CategorySearchRequest
        {
            Category = SearchCategory.Patents,
            Query = query,
            MaxResults = Math.Clamp(max_results, 1, 20),
            Region = region,
            Inventor = inventor,
            Assignee = assignee,
            DateRange = date_range,
        }, cancellationToken);

    /// <summary>
    /// Executes a category search against the active engine, returning the
    /// shared MCP JSON response shape used by all four category tools. If
    /// the active engine is not category-capable or does not advertise the
    /// requested category, a descriptive error is returned instead of
    /// throwing so the model can recover by switching engines.
    /// </summary>
    /// <param name="server">The active MCP server instance, or <see langword="null"/> for direct calls.</param>
    /// <param name="engineManager">Provides the currently active engine.</param>
    /// <param name="request">The normalized category search request.</param>
    /// <param name="ct">Cancellation token for the search operation.</param>
    /// <returns>A JSON payload containing either results or an error.</returns>
    static async Task<string> ExecuteAsync(
        McpServer? server,
        SearchEngineManager engineManager,
        CategorySearchRequest request,
        CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Query))
                return JsonSerializer.Serialize(new { error = "Query must not be empty." }, McpJson.Options);

            var current = engineManager.Current;
            var categoryKey = request.Category.ToString().ToLowerInvariant();

            if (current is not ICategorySearchEngine categoryEngine)
            {
                return JsonSerializer.Serialize(new
                {
                    error = $"The active search engine does not support category search. " +
                            $"Use 'set_search_engine' to switch to a category-capable engine (serper, serpapi, or tavily).",
                    category = categoryKey,
                }, McpJson.Options);
            }

            if (!categoryEngine.SupportedCategories.Contains(request.Category))
            {
                var supported = string.Join(
                    ", ",
                    categoryEngine.SupportedCategories.Select(c => c.ToString().ToLowerInvariant()));
                return JsonSerializer.Serialize(new
                {
                    error = $"Engine '{categoryEngine.EngineName}' does not support '{categoryKey}' search. " +
                            $"Supported categories: [{supported}]. Use 'set_search_engine' to switch.",
                    category = categoryKey,
                }, McpJson.Options);
            }

            var gate = server is null ? null : new McpServerElicitGate(server);

            var result = categoryEngine is IMcpAwareCategorySearchEngine mcpAware
                ? await mcpAware.SearchAsync(gate, request, ct)
                : await categoryEngine.SearchAsync(request, ct);

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
