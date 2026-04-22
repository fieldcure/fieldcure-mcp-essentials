using System.Text.Json;
using FieldCure.Mcp.Essentials.Services;
using ModelContextProtocol.Protocol;

namespace FieldCure.Mcp.Essentials.Search;

/// <summary>
/// Represents an explicitly selected paid search engine whose API key is
/// resolved lazily on first use, with free-search fallback only after
/// elicitation or explicit fallback consent.
/// </summary>
internal sealed class LazyPaidSearchEngine : ISearchEngine, ICategorySearchEngine, IMcpAwareSearchEngine, IMcpAwareCategorySearchEngine
{
    readonly string _engineName;
    readonly string _engineDisplayName;
    readonly string _primaryEnvVar;
    readonly ApiKeyResolverRegistry _keys;
    readonly ISearchEngine _fallback;
    readonly Func<string, string, ISearchEngine> _engineFactory;
    readonly SemaphoreSlim _gate = new(1, 1);

    ISearchEngine? _resolved;
    bool _fallbackDeclined;

    /// <summary>
    /// Clears the cached engine resolution so the next access re-evaluates
    /// API key availability. Called when the host signals that credentials
    /// may have changed (e.g., after the user updated a paid-engine API key
    /// in the host UI and triggered an engine reconnect).
    /// </summary>
    public void InvalidateCache()
    {
        _gate.Wait();
        try
        {
            _resolved = null;
            _fallbackDeclined = false;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Initializes a new lazy paid-engine wrapper.
    /// </summary>
    /// <param name="engineName">Internal engine key such as <c>serper</c>.</param>
    /// <param name="engineDisplayName">Display name shown to users and in logs.</param>
    /// <param name="primaryEnvVar">Primary engine-specific environment variable name.</param>
    /// <param name="keys">API key resolver registry shared across the process.</param>
    /// <param name="fallback">Free search fallback engine used only after consent or when elicitation is unsupported.</param>
    /// <param name="engineFactory">Factory that creates the concrete paid engine once a key is available.</param>
    public LazyPaidSearchEngine(
        string engineName,
        string engineDisplayName,
        string primaryEnvVar,
        ApiKeyResolverRegistry keys,
        ISearchEngine fallback,
        Func<string, string, ISearchEngine> engineFactory)
    {
        _engineName = engineName;
        _engineDisplayName = engineDisplayName;
        _primaryEnvVar = primaryEnvVar;
        _keys = keys;
        _fallback = fallback;
        _engineFactory = engineFactory;
        SupportedCategories = BuildSupportedCategories(engineName);
    }

    /// <inheritdoc />
    public string EngineName => _engineDisplayName;

    /// <inheritdoc />
    public IReadOnlySet<SearchCategory> SupportedCategories { get; }

    /// <inheritdoc />
    public Task<SearchResult[]> SearchAsync(string query, int maxResults, string? region = null, CancellationToken ct = default) =>
        SearchAsync(gate: null, query, maxResults, region, ct);

    /// <inheritdoc />
    public async Task<SearchResult[]> SearchAsync(IElicitGate? gate, string query, int maxResults, string? region = null, CancellationToken ct = default)
    {
        var engine = await ResolveAsync(gate, ct);
        if (engine is null)
            throw new InvalidOperationException(_keys.BuildSoftFailMessage(_engineDisplayName, _primaryEnvVar));

        return await engine.SearchAsync(query, maxResults, region, ct);
    }

    /// <inheritdoc />
    public Task<CategorySearchResult> SearchAsync(CategorySearchRequest request, CancellationToken ct = default) =>
        SearchAsync(gate: null, request, ct);

    /// <inheritdoc />
    public async Task<CategorySearchResult> SearchAsync(IElicitGate? gate, CategorySearchRequest request, CancellationToken ct = default)
    {
        var engine = await ResolveAsync(gate, ct);
        if (engine is null)
            throw new InvalidOperationException(_keys.BuildSoftFailMessage(_engineDisplayName, _primaryEnvVar));

        if (engine is ICategorySearchEngine categoryEngine)
            return await categoryEngine.SearchAsync(request, ct);

        var results = await engine.SearchAsync(request.Query, request.MaxResults, request.Region, ct);
        return new CategorySearchResult
        {
            Category = request.Category,
            Engine = "Bing/DuckDuckGo (fallback)",
            Items = results.Select(r => new CategorySearchResultItem
            {
                Title = r.Title,
                Url = r.Url,
                Snippet = r.Snippet,
            }).ToArray(),
        };
    }

    /// <summary>
    /// Resolves the concrete search engine once and caches the result for
    /// subsequent calls.
    /// </summary>
    /// <param name="gate">The elicitation gate, or <see langword="null"/> for non-MCP calls.</param>
    /// <param name="ct">Cancellation token for the resolution flow.</param>
    /// <returns>The resolved paid or fallback engine, or <see langword="null"/> when the request should soft-fail.</returns>
    async Task<ISearchEngine?> ResolveAsync(IElicitGate? gate, CancellationToken ct)
    {
        if (_resolved is not null)
            return _resolved;

        await _gate.WaitAsync(ct);
        try
        {
            if (_resolved is not null)
                return _resolved;

            var key = await _keys.ResolveAsync(gate, _primaryEnvVar, _engineDisplayName, ct);
            if (!string.IsNullOrWhiteSpace(key))
            {
                _resolved = _engineFactory(_engineName, key);
                return _resolved;
            }

            if (gate?.IsSupported is not true)
            {
                Console.Error.WriteLine(
                    $"[Warning] {_engineDisplayName} API key not available and MCP Elicitation is unsupported. " +
                    "Falling back to Bing/DuckDuckGo.");
                _resolved = _fallback;
                return _resolved;
            }

            if (!_fallbackDeclined)
            {
                if (await ElicitFallbackConsentAsync(gate, ct))
                {
                    _resolved = _fallback;
                    return _resolved;
                }

                _fallbackDeclined = true;
            }

            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Asks the user whether free search engines may be used when the explicit
    /// paid engine could not be configured. Any client response other than an
    /// accepted <c>true</c> boolean is treated as a decline so the caller can
    /// soft-fail deterministically.
    /// </summary>
    /// <param name="gate">The elicitation gate for the active MCP request.</param>
    /// <param name="ct">Cancellation token for the elicitation flow.</param>
    /// <returns><see langword="true"/> only when fallback was explicitly accepted.</returns>
    async Task<bool> ElicitFallbackConsentAsync(IElicitGate gate, CancellationToken ct)
    {
        try
        {
            var result = await gate.ElicitAsync(BuildFallbackConsentPrompt(), ct);
            return result.IsAccepted
                && result.Content is not null
                && result.Content.TryGetValue("use_fallback", out var json)
                && json.ValueKind == JsonValueKind.True;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>
    /// Builds the standard fallback consent prompt for explicit paid-engine flows.
    /// </summary>
    /// <returns>An elicitation request asking whether free fallback search is acceptable.</returns>
    ElicitRequestParams BuildFallbackConsentPrompt() =>
        new()
        {
            Message = $"No {_engineDisplayName} API key available. Continue with free Bing/DuckDuckGo search instead?",
            RequestedSchema = new ElicitRequestParams.RequestSchema
            {
                Properties = new Dictionary<string, ElicitRequestParams.PrimitiveSchemaDefinition>
                {
                    ["use_fallback"] = new ElicitRequestParams.BooleanSchema
                    {
                        Title = "Use fallback search?",
                        Description = $"Accept to run this search with Bing/DuckDuckGo. Decline to keep the {_engineDisplayName} requirement (the request will soft-fail).",
                    },
                },
                Required = ["use_fallback"],
            },
        };

    /// <summary>
    /// Returns the category capabilities implied by the explicitly selected
    /// engine name before that engine is actually instantiated.
    /// </summary>
    /// <param name="engineName">Internal engine key such as <c>serper</c>.</param>
    /// <returns>The set of supported category tools.</returns>
    static IReadOnlySet<SearchCategory> BuildSupportedCategories(string engineName) => engineName switch
    {
        "serper" or "serpapi" => new HashSet<SearchCategory>
        {
            SearchCategory.News,
            SearchCategory.Images,
            SearchCategory.Scholar,
            SearchCategory.Patents,
        },
        "tavily" => new HashSet<SearchCategory>
        {
            SearchCategory.News,
        },
        _ => new HashSet<SearchCategory>(),
    };
}
