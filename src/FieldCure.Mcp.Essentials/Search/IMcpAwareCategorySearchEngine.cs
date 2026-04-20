using FieldCure.Mcp.Essentials.Services;

namespace FieldCure.Mcp.Essentials.Search;

/// <summary>
/// Extends category search execution with access to an elicitation gate so an
/// engine can perform lazy credential prompts without depending on <c>McpServer</c>.
/// </summary>
internal interface IMcpAwareCategorySearchEngine
{
    /// <summary>
    /// Executes a category search with optional access to the current elicitation gate.
    /// </summary>
    /// <param name="gate">The elicitation gate, or <see langword="null"/> for direct calls.</param>
    /// <param name="request">The category search request.</param>
    /// <param name="ct">Cancellation token for the search operation.</param>
    /// <returns>A structured category search result.</returns>
    Task<CategorySearchResult> SearchAsync(
        IElicitGate? gate,
        CategorySearchRequest request,
        CancellationToken ct = default);
}
