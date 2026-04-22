using FieldCure.Mcp.Essentials.Services;

namespace FieldCure.Mcp.Essentials.Search;

/// <summary>
/// Creates <see cref="ISearchEngine"/> instances by name. Used at startup to
/// resolve the initial engine and at runtime by
/// <see cref="SearchEngineManager"/> for <c>set_search_engine</c> switches.
/// </summary>
internal static class SearchEngineFactory
{
    /// <summary>
    /// Supported engine names accepted by <see cref="Create"/>.
    /// Exposed so <c>set_search_engine</c> can advertise the list in its
    /// description and validate inputs.
    /// </summary>
    public static readonly IReadOnlyList<string> SupportedNames =
        ["bing", "duckduckgo", "serper", "tavily", "serpapi"];

    /// <summary>
    /// Creates a search engine by name.
    /// </summary>
    /// <param name="name">Engine key (case-insensitive): <c>bing</c>, <c>duckduckgo</c>/<c>ddg</c>, <c>serper</c>, <c>tavily</c>, <c>serpapi</c>.</param>
    /// <param name="apiKey">Optional API key for paid engines. When omitted, paid engines are wrapped in <see cref="LazyPaidSearchEngine"/> for env-var/Elicitation resolution on first use.</param>
    /// <param name="apiKeyResolvers">Shared API-key resolver registry used by lazy paid wrappers.</param>
    /// <returns>A configured engine instance.</returns>
    /// <exception cref="ArgumentException">Thrown for unknown engine names.</exception>
    public static ISearchEngine Create(string name, string? apiKey, ApiKeyResolverRegistry apiKeyResolvers)
    {
        var fallback = new FallbackSearchEngine(new BingSearchEngine(), new DuckDuckGoSearchEngine());

        return name.ToLowerInvariant() switch
        {
            "bing" => new BingSearchEngine(),
            "duckduckgo" or "ddg" => new DuckDuckGoSearchEngine(),
            "serper" => apiKey is not null
                ? CreateConcrete("serper", apiKey)
                : new LazyPaidSearchEngine("serper", "Serper", "SERPER_API_KEY", apiKeyResolvers, fallback, CreateConcrete),
            "tavily" => apiKey is not null
                ? CreateConcrete("tavily", apiKey)
                : new LazyPaidSearchEngine("tavily", "Tavily", "TAVILY_API_KEY", apiKeyResolvers, fallback, CreateConcrete),
            "serpapi" => apiKey is not null
                ? CreateConcrete("serpapi", apiKey)
                : new LazyPaidSearchEngine("serpapi", "SerpApi", "SERPAPI_API_KEY", apiKeyResolvers, fallback, CreateConcrete),
            _ => throw new ArgumentException(
                $"Unknown search engine: '{name}'. Supported: {string.Join(", ", SupportedNames)}",
                nameof(name)),
        };
    }

    /// <summary>
    /// Creates a concrete paid search engine once an API key is available.
    /// </summary>
    /// <param name="name">Internal engine key such as <c>serper</c>.</param>
    /// <param name="apiKey">Resolved API key for the selected engine.</param>
    /// <returns>A concrete paid search engine instance.</returns>
    public static ISearchEngine CreateConcrete(string name, string apiKey) => name.ToLowerInvariant() switch
    {
        "serper" => new SerperSearchEngine(apiKey),
        "tavily" => new TavilySearchEngine(apiKey),
        "serpapi" => new SerpApiSearchEngine(apiKey),
        _ => throw new NotSupportedException($"Paid search engine '{name}' is not supported."),
    };
}
