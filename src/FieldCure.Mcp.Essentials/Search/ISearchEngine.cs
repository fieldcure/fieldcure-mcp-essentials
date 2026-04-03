namespace FieldCure.Mcp.Essentials.Search;

public interface ISearchEngine
{
    Task<SearchResult[]> SearchAsync(string query, int maxResults, CancellationToken ct = default);
}
