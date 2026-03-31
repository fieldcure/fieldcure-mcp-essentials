using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using FieldCure.Mcp.Essentials.Memory;
using ModelContextProtocol.Server;

namespace FieldCure.Mcp.Essentials.Tools;

[McpServerToolType]
public static class MemoryTools
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    [McpServerTool(Name = "remember")]
    [Description("Save information to persistent memory for use across conversations. Use when the user asks to remember preferences, facts, or context. Write content as a concise third-person statement.")]
    public static string Remember(
        MemoryStore store,
        [Description("Concise factual statement to remember, written in third person (e.g., 'User prefers dark theme.')")]
        string? content = null,
        [Description("Short key to identify this memory (e.g., 'preferred_theme'). Auto-generated if omitted.")]
        string? key = null)
    {
        if (string.IsNullOrWhiteSpace(content))
            return JsonSerializer.Serialize(new { success = false, error = "Parameter 'content' is required." }, JsonOptions);

        key ??= $"mem_{Guid.NewGuid():N}"[..16];

        var (success, warning) = store.Add(key, content);

        if (!success)
            return JsonSerializer.Serialize(new { success = false, error = "Failed to save memory." }, JsonOptions);

        return JsonSerializer.Serialize(new
        {
            success = true,
            key,
            message = $"Remembered: {content}",
            warning,
        }, JsonOptions);
    }

    [McpServerTool(Name = "forget")]
    [Description("Remove information from persistent memory. Use when the user asks to forget or delete previously remembered information.")]
    public static string Forget(
        MemoryStore store,
        [Description("Search term to find the memory to remove (matched by substring in key or value)")]
        string? query = null)
    {
        if (string.IsNullOrWhiteSpace(query))
            return JsonSerializer.Serialize(new { success = false, error = "Parameter 'query' is required." }, JsonOptions);

        var removed = store.Remove(query);

        return JsonSerializer.Serialize(new
        {
            success = removed,
            message = removed
                ? $"Forgot: {query}"
                : $"No matching memory found for: {query}",
        }, JsonOptions);
    }

    [McpServerTool(Name = "list_memories")]
    [Description("List all saved memory entries. Use to check what the user has asked to remember.")]
    public static string ListMemories(MemoryStore store)
    {
        var entries = store.GetAll();

        return JsonSerializer.Serialize(new
        {
            total_count = entries.Count,
            entries = entries.Select(e => new
            {
                key = e.Key,
                value = e.Value,
                created_at = e.CreatedAt,
                updated_at = e.UpdatedAt,
            }),
        }, JsonOptions);
    }
}
