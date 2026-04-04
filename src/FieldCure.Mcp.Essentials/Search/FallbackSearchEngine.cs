namespace FieldCure.Mcp.Essentials.Search;

/// <summary>
/// Tries the primary engine first. If it returns empty results,
/// switches to the fallback engine for subsequent calls.
/// </summary>
public sealed class FallbackSearchEngine : ISearchEngine
{
    private readonly ISearchEngine[] _engines;
    private int _currentIndex;

    /// <summary>
    /// Creates a fallback engine with one or more engines in priority order.
    /// </summary>
    public FallbackSearchEngine(params ISearchEngine[] engines)
        => _engines = engines;

    /// <inheritdoc />
    public async Task<SearchResult[]> SearchAsync(
        string query, int maxResults, string? region = null, CancellationToken ct = default)
    {
        var results = await _engines[_currentIndex].SearchAsync(query, maxResults, region, ct);

        if (results.Length == 0)
        {
            // Switch to next engine for this call AND subsequent calls
            _currentIndex = (_currentIndex + 1) % _engines.Length;
            results = await _engines[_currentIndex].SearchAsync(query, maxResults, region, ct);
        }

        return results;
    }
}
