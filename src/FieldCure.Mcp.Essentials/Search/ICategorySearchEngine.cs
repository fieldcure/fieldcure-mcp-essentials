namespace FieldCure.Mcp.Essentials.Search;

/// <summary>
/// Extends search capability with category-specific searches.
/// Engines that support categories (SerpApi, Serper, Tavily) implement
/// both <see cref="ISearchEngine"/> and <see cref="ICategorySearchEngine"/>.
/// </summary>
public interface ICategorySearchEngine
{
    /// <summary>
    /// Gets the display name of the engine (e.g., "SerpApi", "Serper").
    /// Used in dynamic tool descriptions.
    /// </summary>
    string EngineName { get; }

    /// <summary>
    /// Gets the set of search categories this engine supports.
    /// Determines which category tools are registered at startup.
    /// </summary>
    IReadOnlySet<SearchCategory> SupportedCategories { get; }

    /// <summary>
    /// Executes a category-specific search.
    /// </summary>
    Task<CategorySearchResult> SearchAsync(
        CategorySearchRequest request,
        CancellationToken ct = default);
}
