using System.ComponentModel;
using System.Text.Json;
using FieldCure.Mcp.Essentials.Search;
using ModelContextProtocol.Server;

namespace FieldCure.Mcp.Essentials.Tools;

/// <summary>
/// MCP tool that searches the web and returns snippet results.
/// </summary>
[McpServerToolType]
public static class WebSearchTool
{
    /// <summary>
    /// Searches the web and returns results as JSON.
    /// </summary>
    [McpServerTool(Name = "web_search")]
    [Description("Search the web and return a list of results with title, URL, and snippet. Use region for localized results (e.g. 'ko-kr' for Korean).")]
    public static async Task<string> WebSearch(
        ISearchEngine searchEngine,
        [Description("Search query")]
        string query,
        [Description("Maximum number of results to return (default: 5, max: 10)")]
        int max_results = 5,
        [Description("Region code for localized results (e.g. 'ko-kr', 'en-us', 'ja-jp'). Omit for global results.")]
        string? region = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query))
                return JsonSerializer.Serialize(new { error = "Query must not be empty." }, McpJson.Options);

            max_results = Math.Clamp(max_results, 1, 10);

            var results = await searchEngine.SearchAsync(query, max_results, region, cancellationToken);

            return JsonSerializer.Serialize(new { results }, McpJson.Options);
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
