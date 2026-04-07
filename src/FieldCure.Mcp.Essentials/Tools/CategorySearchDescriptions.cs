using FieldCure.Mcp.Essentials.Search;

namespace FieldCure.Mcp.Essentials.Tools;

/// <summary>
/// Generates engine-specific descriptions for category search tools.
/// Used at startup when dynamically registering tools via McpServerTool.Create().
/// </summary>
static class CategorySearchDescriptions
{
    public static string News(string engine) => engine switch
    {
        "SerpApi" => "Search recent news articles via Google News (SerpApi). Returns results sorted by publication date with source and topic clustering.",
        "Serper" => "Search recent news articles via Google News (Serper). Returns results sorted by publication date with structured metadata.",
        "Tavily" => "Search recent news articles via Tavily. Returns real-time news updates sorted by relevance and publication date.",
        _ => $"Search recent news articles ({engine}). Returns results with title, URL, snippet, source, and publication date.",
    };

    public static string Images(string engine) => engine switch
    {
        "SerpApi" => "Search images via Google Images (SerpApi). Returns image URLs, thumbnails, dimensions, and source information with size, type, and color filtering.",
        "Serper" => "Search images via Google Images (Serper). Returns image URLs, thumbnails, dimensions, and source information.",
        _ => $"Search images ({engine}). Returns image URLs, thumbnails, dimensions, and source information.",
    };

    public static string Scholar(string engine) => engine switch
    {
        "SerpApi" => "Search academic papers via Google Scholar (SerpApi). Returns titles, authors, citation counts, publication details, and citing paper links.",
        "Serper" => "Search academic papers via Google Scholar (Serper). Returns titles, authors, citation counts, and publication details.",
        _ => $"Search academic papers ({engine}). Returns titles, citation counts, and publication details.",
    };

    public static string Patents(string engine) => engine switch
    {
        "SerpApi" => "Search patent documents via Google Patents (SerpApi). Find prior art, patent filings, and IP information with inventor, assignee, and priority date filtering.",
        "Serper" => "Search patent documents via Google Patents (Serper). Find prior art, patent filings, and IP information.",
        _ => $"Search patent documents ({engine}). Find prior art, patent filings, and IP information.",
    };
}
