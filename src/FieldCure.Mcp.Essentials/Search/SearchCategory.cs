namespace FieldCure.Mcp.Essentials.Search;

/// <summary>
/// Represents search categories that engines may support.
/// Each category maps to a dedicated MCP tool
/// that is conditionally registered at startup.
/// </summary>
public enum SearchCategory
{
    /// <summary>News article search with recency sorting.</summary>
    News,

    /// <summary>Image search returning structured metadata.</summary>
    Images,

    /// <summary>Academic/scholarly paper search.</summary>
    Scholar,

    /// <summary>Patent document search.</summary>
    Patents,
}
