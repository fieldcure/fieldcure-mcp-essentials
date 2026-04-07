namespace FieldCure.Mcp.Essentials.Search;

/// <summary>
/// Represents the result of a category-specific search.
/// </summary>
public class CategorySearchResult
{
    /// <summary>Gets the category that was searched.</summary>
    public required SearchCategory Category { get; init; }

    /// <summary>Gets the engine that executed the search.</summary>
    public required string Engine { get; init; }

    /// <summary>Gets the list of result items.</summary>
    public required IReadOnlyList<CategorySearchResultItem> Items { get; init; }
}

/// <summary>
/// A single result item from a category search.
/// Fields are populated based on the category.
/// </summary>
public class CategorySearchResultItem
{
    // --- Common ---

    /// <summary>Gets the result title.</summary>
    public required string Title { get; init; }

    /// <summary>Gets the result URL.</summary>
    public string? Url { get; init; }

    /// <summary>Gets the result snippet/abstract.</summary>
    public string? Snippet { get; init; }

    // --- News ---

    /// <summary>Gets the publication date.</summary>
    public string? PublishedDate { get; init; }

    /// <summary>Gets the news source name.</summary>
    public string? Source { get; init; }

    // --- Scholar ---

    /// <summary>Gets the citation count.</summary>
    public int? CitationCount { get; init; }

    /// <summary>Gets the authors list.</summary>
    public IReadOnlyList<string>? Authors { get; init; }

    /// <summary>Gets the publication year.</summary>
    public int? Year { get; init; }

    /// <summary>Gets the journal/venue name.</summary>
    public string? Journal { get; init; }

    // --- Patents ---

    /// <summary>Gets the patent ID (e.g., "US10123456B2").</summary>
    public string? PatentId { get; init; }

    /// <summary>Gets the inventor name.</summary>
    public string? Inventor { get; init; }

    /// <summary>Gets the assignee/company.</summary>
    public string? Assignee { get; init; }

    /// <summary>Gets the priority date.</summary>
    public string? PriorityDate { get; init; }

    /// <summary>Gets the filing date.</summary>
    public string? FilingDate { get; init; }

    // --- Images ---

    /// <summary>Gets the full image URL.</summary>
    public string? ImageUrl { get; init; }

    /// <summary>Gets the thumbnail URL.</summary>
    public string? ThumbnailUrl { get; init; }

    /// <summary>Gets the image width in pixels.</summary>
    public int? Width { get; init; }

    /// <summary>Gets the image height in pixels.</summary>
    public int? Height { get; init; }
}
