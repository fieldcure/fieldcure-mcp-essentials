using FieldCure.Mcp.Essentials.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace FieldCure.Mcp.Essentials.Search;

/// <summary>
/// Owns the currently active <see cref="ISearchEngine"/> and coordinates
/// runtime engine switches with MCP <c>notifications/tools/list_changed</c>
/// emissions. Registered as a singleton; tool handlers read
/// <see cref="Current"/> on each invocation so switches take effect without
/// restarting the stdio server.
/// </summary>
public sealed class SearchEngineManager
{
    readonly ApiKeyResolverRegistry _apiKeyResolvers;
    readonly ILogger<SearchEngineManager> _logger;
    readonly SemaphoreSlim _switchGate = new(1, 1);

    volatile ISearchEngine _current;

    /// <summary>
    /// Initializes the manager with the engine resolved from CLI arguments
    /// or environment variables at startup.
    /// </summary>
    /// <param name="initialEngine">The engine to activate initially.</param>
    /// <param name="apiKeyResolvers">Shared API-key resolver registry passed to newly created engines.</param>
    /// <param name="logger">Logger for switch diagnostics.</param>
    public SearchEngineManager(
        ISearchEngine initialEngine,
        ApiKeyResolverRegistry apiKeyResolvers,
        ILogger<SearchEngineManager> logger)
    {
        _current = initialEngine;
        _apiKeyResolvers = apiKeyResolvers;
        _logger = logger;
    }

    /// <summary>
    /// Gets the currently active search engine. Safe to read from any
    /// thread; swaps are atomic via <c>volatile</c>.
    /// </summary>
    public ISearchEngine Current => _current;

    /// <summary>
    /// Switches the active engine at runtime and emits
    /// <c>notifications/tools/list_changed</c> so supporting clients refresh
    /// their tool descriptions. No-op for clients that ignore the
    /// notification; the superset of category tools stays registered and
    /// unsupported invocations are rejected by per-tool runtime guards.
    /// </summary>
    /// <param name="engineName">Engine identifier (e.g. <c>bing</c>, <c>duckduckgo</c>, <c>serper</c>, <c>tavily</c>, <c>serpapi</c>).</param>
    /// <param name="server">MCP server instance used to notify the client; pass <see langword="null"/> to suppress the notification (e.g., tests).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly activated <see cref="ISearchEngine"/>.</returns>
    public async Task<ISearchEngine> SwitchAsync(
        string engineName,
        McpServer? server,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(engineName))
            throw new ArgumentException("Engine name must not be empty.", nameof(engineName));

        await _switchGate.WaitAsync(ct);
        try
        {
            var newEngine = SearchEngineFactory.Create(engineName, apiKey: null, _apiKeyResolvers);

            // Invalidate any lazy cache on the outgoing engine so that if the
            // user switches away from and back to the same paid engine, the
            // new instance won't carry stale resolution state. (New instances
            // start clean; this only matters if a future caller holds a
            // reference to the old wrapper.)
            if (_current is LazyPaidSearchEngine outgoingLazy)
                outgoingLazy.InvalidateCache();

            _current = newEngine;

            _logger.LogInformation(
                "Search engine switched to '{Engine}'.",
                engineName.ToLowerInvariant());

            if (server is not null)
            {
                try
                {
                    await server.SendNotificationAsync(
                        NotificationMethods.ToolListChangedNotification,
                        ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Notification is best-effort: clients that don't support
                    // list_changed can't be distinguished reliably, so we log
                    // and continue rather than failing the switch.
                    _logger.LogDebug(ex, "Failed to send tools/list_changed notification.");
                }
            }

            return newEngine;
        }
        finally
        {
            _switchGate.Release();
        }
    }
}
