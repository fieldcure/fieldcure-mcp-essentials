namespace FieldCure.Mcp.Essentials.Search;

/// <summary>
/// Represents a single web search result.
/// </summary>
/// <param name="Title">Page title.</param>
/// <param name="Url">Page URL.</param>
/// <param name="Snippet">Short description or excerpt.</param>
public sealed record SearchResult(string Title, string Url, string Snippet);
