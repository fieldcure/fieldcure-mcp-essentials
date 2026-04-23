using System.ComponentModel;
using System.Text.Json;
using FieldCure.Mcp.Essentials.Search;
using ModelContextProtocol.Server;

namespace FieldCure.Mcp.Essentials.Tools;

/// <summary>
/// MCP tool that reports the currently active search engine. Pairs with
/// <see cref="SetSearchEngineTool"/> to let host UIs render the engine that
/// the server is actually using — which may differ from the initial CLI/env
/// arg after a runtime <c>set_search_engine</c> call.
/// </summary>
[McpServerToolType]
public static class GetSearchEngineTool
{
    /// <summary>
    /// Returns the currently active search engine name and its category
    /// capabilities without side effects. Intended for host UIs (e.g.
    /// AssistStudio Settings) that need to reflect the live engine state,
    /// and for models that want to check engine capabilities before
    /// invoking a category search.
    /// </summary>
    /// <param name="engineManager">The shared <see cref="SearchEngineManager"/> singleton.</param>
    /// <returns>A JSON payload describing the active engine and its category capabilities.</returns>
    [McpServerTool(Name = "get_search_engine")]
    [Description("Return the currently active search engine and its category capabilities. Read-only, no side effects. Use this before calling a category search tool to confirm the active engine supports the category, or to reflect the live engine state in a host UI.")]
    public static string GetSearchEngine(SearchEngineManager engineManager)
    {
        var current = engineManager.Current;

        var engineLabel = current is ICategorySearchEngine named
            ? named.EngineName
            : current.GetType().Name.Replace("SearchEngine", string.Empty);

        var categories = current is ICategorySearchEngine cat
            ? cat.SupportedCategories.Select(c => c.ToString().ToLowerInvariant()).ToArray()
            : Array.Empty<string>();

        return JsonSerializer.Serialize(new
        {
            engine = engineLabel,
            key = ResolveCanonicalKey(current),
            categories,
            supports_category_search = categories.Length > 0,
        }, McpJson.Options);
    }

    /// <summary>
    /// Maps an <see cref="ISearchEngine"/> instance to the canonical key accepted
    /// by <c>set_search_engine</c> (e.g. <c>"bing"</c>, <c>"serper"</c>). Hosts
    /// use this value to match the active engine to their own UI selection
    /// without string-normalizing display names.
    /// </summary>
    /// <param name="engine">The currently active engine reported by <see cref="SearchEngineManager"/>.</param>
    /// <returns>A lowercase canonical key, or the lowercase type-name fallback for unknown engines.</returns>
    static string ResolveCanonicalKey(ISearchEngine engine) => engine switch
    {
        BingSearchEngine => "bing",
        DuckDuckGoSearchEngine => "duckduckgo",
        SerperSearchEngine => "serper",
        TavilySearchEngine => "tavily",
        SerpApiSearchEngine => "serpapi",
        LazyPaidSearchEngine lazy => lazy.EngineName.ToLowerInvariant(),
        // FallbackSearchEngine wraps Bing (primary) + DuckDuckGo (secondary);
        // report the primary so host UIs can render a single stable selection.
        FallbackSearchEngine => "bing",
        _ => engine.GetType().Name.Replace("SearchEngine", string.Empty).ToLowerInvariant(),
    };
}
