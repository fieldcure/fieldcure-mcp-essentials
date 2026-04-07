using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using FieldCure.Mcp.Essentials.Search;

namespace FieldCure.Mcp.Essentials.Tools;

/// <summary>
/// Handler methods for category search MCP tools.
/// These are NOT auto-discovered (no [McpServerToolType]).
/// Registered programmatically via McpServerTool.Create() in Program.cs.
/// </summary>
public static class CategorySearchTools
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Searches recent news articles.
    /// </summary>
    public static async Task<string> SearchNews(
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
        return await ExecuteAsync(engine, new CategorySearchRequest
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
        return await ExecuteAsync(engine, new CategorySearchRequest
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
        return await ExecuteAsync(engine, new CategorySearchRequest
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
        return await ExecuteAsync(engine, new CategorySearchRequest
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

    static async Task<string> ExecuteAsync(
        ICategorySearchEngine engine, CategorySearchRequest request, CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Query))
                return JsonSerializer.Serialize(new { error = "Query must not be empty." }, JsonOptions);

            var result = await engine.SearchAsync(request, ct);

            return JsonSerializer.Serialize(new
            {
                engine = result.Engine,
                category = result.Category.ToString().ToLowerInvariant(),
                results = result.Items,
            }, JsonOptions);
        }
        catch (OperationCanceledException)
        {
            return JsonSerializer.Serialize(new { error = "Search request timed out." }, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }
}
