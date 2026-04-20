using System.Collections.Concurrent;
using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace FieldCure.Mcp.Essentials.Services;

/// <summary>
/// Caches one API key resolver per environment variable name so paid search
/// engines can resolve keys lazily at first use instead of failing at startup.
/// </summary>
public sealed class ApiKeyResolverRegistry
{
    readonly ConcurrentDictionary<string, ApiKeyResolver> _resolvers = new(StringComparer.Ordinal);

    /// <summary>
    /// Resolves an API key from environment variables first, then optionally
    /// via MCP Elicitation when the connected client supports it.
    /// </summary>
    /// <param name="gate">The elicitation gate, or <see langword="null"/> for non-MCP calls.</param>
    /// <param name="envVarName">The primary engine-specific environment variable name.</param>
    /// <param name="providerLabel">Human-readable provider name shown in elicitation prompts.</param>
    /// <param name="ct">Cancellation token for the resolution flow.</param>
    /// <returns>The resolved API key, or <see langword="null"/> when no key is available.</returns>
    internal Task<string?> ResolveAsync(IElicitGate? gate, string envVarName, string providerLabel, CancellationToken ct) =>
        _resolvers.GetOrAdd(envVarName, static key => new ApiKeyResolver(key)).ResolveAsync(gate, providerLabel, ct);

    /// <summary>
    /// Invalidates a cached key after an upstream rejection (e.g. HTTP 401/403)
    /// so the next <see cref="ResolveAsync"/> forces a new elicitation. The
    /// environment-variable source is marked exhausted to avoid re-reading the
    /// same bad key on the next call; subsequent resolutions go straight to
    /// Elicitation within the per-session re-elicit cap.
    /// </summary>
    /// <param name="envVarName">The environment variable name whose cached key should be discarded.</param>
    public void Invalidate(string envVarName)
    {
        if (_resolvers.TryGetValue(envVarName, out var resolver))
            resolver.Invalidate();
    }

    /// <summary>
    /// Builds a user-facing soft-fail message for explicit paid-engine requests
    /// where no API key was provided and free fallback was not accepted.
    /// </summary>
    /// <param name="engineDisplayName">Human-readable engine name such as Serper.</param>
    /// <param name="envVarName">The environment variable users can set to configure the engine.</param>
    /// <returns>A concise guidance message suitable for tool error output.</returns>
    public string BuildSoftFailMessage(string engineDisplayName, string envVarName) =>
        $"{engineDisplayName} requires an API key, but none was provided and no fallback was accepted. " +
        $"Set {envVarName} environment variable, or restart with --search-engine bing for free search.";
}

/// <summary>
/// Resolves and caches one concrete API key for a single environment variable.
/// </summary>
sealed class ApiKeyResolver(string envVarName)
{
    const int MaxReElicits = 2;

    readonly SemaphoreSlim _gate = new(1, 1);
    string? _cachedKey;
    bool _hasElicitedBefore;
    int _reElicitCount;
    bool _staticSourcesExhausted;

    /// <summary>
    /// Resolves the API key from static sources or, when possible, by prompting
    /// the MCP client through Elicitation.
    /// </summary>
    /// <param name="gate">The elicitation gate, or <see langword="null"/> for non-MCP calls.</param>
    /// <param name="providerLabel">Human-readable provider name shown in elicitation prompts.</param>
    /// <param name="ct">Cancellation token for the resolution flow.</param>
    /// <returns>The resolved API key, or <see langword="null"/> when none is available.</returns>
    public async Task<string?> ResolveAsync(IElicitGate? gate, string providerLabel, CancellationToken ct)
    {
        if (_cachedKey is not null)
            return _cachedKey;

        await _gate.WaitAsync(ct);
        try
        {
            if (_cachedKey is not null)
                return _cachedKey;

            if (!_staticSourcesExhausted)
            {
                var envKey = Environment.GetEnvironmentVariable(envVarName);
                if (!string.IsNullOrWhiteSpace(envKey))
                {
                    _cachedKey = envKey;
                    return _cachedKey;
                }
            }

            if (gate?.IsSupported is not true)
                return null;

            if (_hasElicitedBefore)
            {
                if (_reElicitCount >= MaxReElicits)
                    return null;
                _reElicitCount++;
            }

            _hasElicitedBefore = true;

            try
            {
                var result = await gate.ElicitAsync(BuildApiKeyPrompt(providerLabel), ct);
                if (!TryReadString(result, "api_key", out var key))
                    return null;

                _cachedKey = key;
                return _cachedKey;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return null;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Discards the cached key and marks the environment variable source as
    /// exhausted so the next resolution cannot return the same stale value
    /// without fresh user intervention via Elicitation.
    /// </summary>
    public void Invalidate()
    {
        _cachedKey = null;
        _staticSourcesExhausted = true;
    }

    /// <summary>
    /// Builds the standard API key prompt shown to the client.
    /// </summary>
    /// <param name="providerLabel">Human-readable provider name shown in the prompt.</param>
    /// <returns>An elicitation request for one API key string.</returns>
    static ElicitRequestParams BuildApiKeyPrompt(string providerLabel) =>
        new()
        {
            Message = $"Enter your {providerLabel} API key.",
            RequestedSchema = new ElicitRequestParams.RequestSchema
            {
                Properties = new Dictionary<string, ElicitRequestParams.PrimitiveSchemaDefinition>
                {
                    ["api_key"] = new ElicitRequestParams.StringSchema
                    {
                        Title = "API Key",
                        Description = $"{providerLabel} API Key",
                        MinLength = 1,
                    },
                },
                Required = ["api_key"],
            },
        };

    /// <summary>
    /// Attempts to read a required non-empty string field from an elicitation result.
    /// </summary>
    /// <param name="result">The elicitation result returned by the client.</param>
    /// <param name="fieldName">The field name to read from the result payload.</param>
    /// <param name="value">When this method returns, contains the parsed string value if successful.</param>
    /// <returns><see langword="true"/> when a non-empty string field was found; otherwise <see langword="false"/>.</returns>
    static bool TryReadString(ElicitGateResult result, string fieldName, out string? value)
    {
        value = null;

        if (!result.IsAccepted || result.Content is null || !result.Content.TryGetValue(fieldName, out var json))
            return false;

        value = json.ValueKind == JsonValueKind.String ? json.GetString() : null;
        return !string.IsNullOrWhiteSpace(value);
    }
}
