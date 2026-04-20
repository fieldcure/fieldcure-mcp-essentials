using FieldCure.Mcp.Essentials.Services;

namespace FieldCure.Mcp.Essentials.Search;

/// <summary>
/// Extends basic search execution with access to an elicitation gate so an
/// engine can perform lazy credential prompts without depending on <c>McpServer</c>.
/// </summary>
internal interface IMcpAwareSearchEngine
{
    /// <summary>
    /// Executes a search with optional access to the current elicitation gate.
    /// </summary>
    /// <param name="gate">The elicitation gate, or <see langword="null"/> for direct calls.</param>
    /// <param name="query">The search query.</param>
    /// <param name="maxResults">Maximum number of results to return.</param>
    /// <param name="region">Optional region code for localized results.</param>
    /// <param name="ct">Cancellation token for the search operation.</param>
    /// <returns>An array of search results.</returns>
    Task<SearchResult[]> SearchAsync(
        IElicitGate? gate,
        string query,
        int maxResults,
        string? region = null,
        CancellationToken ct = default);
}
