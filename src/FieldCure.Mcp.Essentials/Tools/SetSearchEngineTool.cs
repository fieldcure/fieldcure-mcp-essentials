using System.ComponentModel;
using System.Text.Json;
using FieldCure.Mcp.Essentials.Search;
using ModelContextProtocol.Server;

namespace FieldCure.Mcp.Essentials.Tools;

/// <summary>
/// MCP tool that switches the active search engine at runtime.
/// The paid-engine API key resolution path (env var → Elicitation → soft
/// fallback) is unchanged — this tool only selects which engine is active;
/// credentials are resolved lazily on the next search invocation.
/// </summary>
[McpServerToolType]
public static class SetSearchEngineTool
{
    /// <summary>
    /// Switches the active search engine and notifies the client that the
    /// effective tool descriptions may have changed. The four category tools
    /// (<c>search_news</c>, <c>search_images</c>, <c>search_scholar</c>,
    /// <c>search_patents</c>) remain registered regardless of engine; when
    /// the active engine does not support a given category, the tool returns
    /// a descriptive error at invocation time.
    /// </summary>
    /// <param name="server">MCP server instance injected by the SDK; used to send <c>notifications/tools/list_changed</c>.</param>
    /// <param name="engineManager">The shared <see cref="SearchEngineManager"/> singleton.</param>
    /// <param name="engine">Engine name: <c>bing</c>, <c>duckduckgo</c>, <c>serper</c>, <c>tavily</c>, or <c>serpapi</c>.</param>
    /// <param name="cancellationToken">Cancellation token for the switch operation.</param>
    /// <returns>A JSON payload describing the newly active engine and its category capabilities.</returns>
    [McpServerTool(Name = "set_search_engine")]
    [Description("Switch the active search engine for web_search and category search tools. Available engines: bing, duckduckgo, serper, tavily, serpapi. Paid engines (serper, tavily, serpapi) resolve their API key lazily from environment variables or via MCP Elicitation on the next search invocation — set_search_engine itself does not require a key.")]
    public static async Task<string> SetSearchEngine(
        McpServer? server,
        SearchEngineManager engineManager,
        [Description("Engine name to activate: 'bing', 'duckduckgo', 'serper', 'tavily', or 'serpapi'.")]
        string engine,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(engine))
            {
                return JsonSerializer.Serialize(new
                {
                    error = "Engine name must not be empty.",
                    supported = SearchEngineFactory.SupportedNames,
                }, McpJson.Options);
            }

            var activated = await engineManager.SwitchAsync(engine, server, cancellationToken);

            var categories = activated is ICategorySearchEngine cat
                ? cat.SupportedCategories.Select(c => c.ToString().ToLowerInvariant()).ToArray()
                : Array.Empty<string>();

            var engineLabel = activated is ICategorySearchEngine named
                ? named.EngineName
                : activated.GetType().Name.Replace("SearchEngine", string.Empty);

            return JsonSerializer.Serialize(new
            {
                engine = engineLabel,
                active = engine.ToLowerInvariant(),
                categories,
                note = categories.Length == 0
                    ? "Current engine does not support category search. Category tools will return an error until a category-capable engine (serper, tavily, serpapi) is set."
                    : null,
            }, McpJson.Options);
        }
        catch (ArgumentException ex)
        {
            return JsonSerializer.Serialize(new
            {
                error = ex.Message,
                supported = SearchEngineFactory.SupportedNames,
            }, McpJson.Options);
        }
        catch (OperationCanceledException)
        {
            return JsonSerializer.Serialize(new { error = "Engine switch was cancelled." }, McpJson.Options);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, McpJson.Options);
        }
    }
}
