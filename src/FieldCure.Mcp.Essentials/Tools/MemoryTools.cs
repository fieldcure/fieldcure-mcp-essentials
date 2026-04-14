using System.ComponentModel;
using System.Text.Json;
using FieldCure.Mcp.Essentials.Memory;
using ModelContextProtocol.Server;

namespace FieldCure.Mcp.Essentials.Tools;

/// <summary>
/// MCP tools for persistent key-value memory (remember, forget, list_memories).
/// </summary>
[McpServerToolType]
public static class MemoryTools
{
    /// <summary>
    /// Stores or updates a key-value memory entry.
    /// </summary>
    [McpServerTool(Name = "remember")]
    [Description("Store a memory. If the key already exists, the value is updated.")]
    public static string Remember(
        MemoryStore store,
        [Description("Short identifier for this memory (e.g., 'preferred_language')")]
        string? key = null,
        [Description("The content to remember")]
        string? value = null)
    {
        if (string.IsNullOrWhiteSpace(key))
            return JsonSerializer.Serialize(new { error = "Parameter 'key' is required." }, McpJson.Options);
        if (string.IsNullOrWhiteSpace(value))
            return JsonSerializer.Serialize(new { error = "Parameter 'value' is required." }, McpJson.Options);

        var (created, updated) = store.Upsert(key, value);

        return JsonSerializer.Serialize(new { key, created, updated }, McpJson.Options);
    }

    /// <summary>
    /// Deletes memories by exact key or keyword query.
    /// </summary>
    [McpServerTool(Name = "forget")]
    [Description("Delete a memory by key, or by query to forget multiple matches.")]
    public static string Forget(
        MemoryStore store,
        [Description("Exact key to delete")]
        string? key = null,
        [Description("Keyword query — deletes all matching memories")]
        string? query = null)
    {
        if (string.IsNullOrWhiteSpace(key) && string.IsNullOrWhiteSpace(query))
            return JsonSerializer.Serialize(new { error = "Either 'key' or 'query' is required." }, McpJson.Options);

        if (!string.IsNullOrWhiteSpace(key))
        {
            var deleted = store.DeleteByKey(key);
            return JsonSerializer.Serialize(new
            {
                deleted = deleted ? 1 : 0,
                keys = deleted ? new[] { key } : Array.Empty<string>(),
            }, McpJson.Options);
        }
        else
        {
            var deletedKeys = store.DeleteByQuery(query!);
            return JsonSerializer.Serialize(new
            {
                deleted = deletedKeys.Count,
                keys = deletedKeys,
            }, McpJson.Options);
        }
    }

    /// <summary>
    /// Searches and lists stored memories with optional FTS5 query.
    /// </summary>
    [McpServerTool(Name = "list_memories")]
    [Description("Search and list stored memories. Without a query, returns recent memories. With a query, performs keyword search across all memories.")]
    public static string ListMemories(
        MemoryStore store,
        [Description("Keyword search (FTS5). Null returns recent memories.")]
        string? query = null,
        [Description("Max results to return (default: 20, max: 100)")]
        int? limit = null,
        [Description("Offset for pagination (default: 0)")]
        int? offset = null)
    {
        var (entries, total) = store.List(query, limit ?? 20, offset ?? 0);

        return JsonSerializer.Serialize(new
        {
            memories = entries.Select(e => new
            {
                key = e.Key,
                value = e.Value,
                created_at = e.CreatedAt,
                updated_at = e.UpdatedAt,
            }),
            total,
            returned = entries.Count,
            has_more = (offset ?? 0) + entries.Count < total,
        }, McpJson.Options);
    }
}
