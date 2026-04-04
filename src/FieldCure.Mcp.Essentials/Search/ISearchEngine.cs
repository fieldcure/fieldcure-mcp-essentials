namespace FieldCure.Mcp.Essentials.Search;

/// <summary>
/// Searches the web and returns snippet results.
/// </summary>
public interface ISearchEngine
{
    /// <summary>
    /// Executes a web search and returns an array of results.
    /// </summary>
    Task<SearchResult[]> SearchAsync(
        string query,
        int maxResults,
        string? region = null,
        CancellationToken ct = default);
}
