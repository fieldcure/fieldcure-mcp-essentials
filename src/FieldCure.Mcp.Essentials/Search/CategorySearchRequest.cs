namespace FieldCure.Mcp.Essentials.Search;

/// <summary>
/// Represents a category-specific search request.
/// Category-specific parameters are nullable; each tool handler
/// populates only the relevant fields.
/// </summary>
public class CategorySearchRequest
{
    /// <summary>Gets the target search category.</summary>
    public required SearchCategory Category { get; init; }

    /// <summary>Gets the search query string.</summary>
    public required string Query { get; init; }

    /// <summary>Gets the maximum number of results to return.</summary>
    public int MaxResults { get; init; } = 10;

    /// <summary>Gets the BCP47 region code (e.g., "ko-kr").</summary>
    public string? Region { get; init; }

    // --- News-specific ---

    /// <summary>
    /// Time range filter (e.g., "1d", "1w", "1m").
    /// Applicable only when Category is News.
    /// </summary>
    public string? TimeRange { get; init; }

    // --- Scholar-specific ---

    /// <summary>
    /// Find papers citing this paper ID.
    /// Applicable only when Category is Scholar.
    /// </summary>
    public string? CitedBy { get; init; }

    /// <summary>
    /// Filter by author name.
    /// Applicable only when Category is Scholar.
    /// </summary>
    public string? Author { get; init; }

    // --- Patents-specific ---

    /// <summary>
    /// Filter by inventor name.
    /// Applicable only when Category is Patents.
    /// </summary>
    public string? Inventor { get; init; }

    /// <summary>
    /// Filter by assignee/company name.
    /// Applicable only when Category is Patents.
    /// </summary>
    public string? Assignee { get; init; }

    /// <summary>
    /// Priority date range (e.g., "2020-01-01:2024-12-31").
    /// Applicable only when Category is Patents.
    /// </summary>
    public string? DateRange { get; init; }

    // --- Images-specific ---

    /// <summary>
    /// Image size filter (e.g., "large", "medium", "icon").
    /// Applicable only when Category is Images.
    /// </summary>
    public string? ImageSize { get; init; }

    /// <summary>
    /// Image type filter (e.g., "photo", "clipart", "lineart").
    /// Applicable only when Category is Images.
    /// </summary>
    public string? ImageType { get; init; }
}
