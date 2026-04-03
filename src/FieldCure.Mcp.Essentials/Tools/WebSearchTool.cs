using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using FieldCure.Mcp.Essentials.Search;
using ModelContextProtocol.Server;

namespace FieldCure.Mcp.Essentials.Tools;

[McpServerToolType]
public static class WebSearchTool
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    [McpServerTool(Name = "web_search")]
    [Description("Search the web and return a list of results with title, URL, and snippet. No full page content is fetched — use web_fetch for that.")]
    public static async Task<string> WebSearch(
        ISearchEngine searchEngine,
        [Description("Search query")]
        string query,
        [Description("Maximum number of results to return (default: 5, max: 10)")]
        int max_results = 5,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query))
                return JsonSerializer.Serialize(new { error = "Query must not be empty." }, JsonOptions);

            max_results = Math.Clamp(max_results, 1, 10);

            var results = await searchEngine.SearchAsync(query, max_results, cancellationToken);

            return JsonSerializer.Serialize(new { results }, JsonOptions);
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
