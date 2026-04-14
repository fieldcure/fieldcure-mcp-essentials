using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FieldCure.Mcp.Essentials;

/// <summary>
/// Shared JSON serialization options for all MCP tool responses.
/// Uses relaxed encoding so non-ASCII characters (Korean, CJK, emoji, etc.)
/// are emitted as-is instead of \uXXXX escape sequences.
/// </summary>
internal static class McpJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}
