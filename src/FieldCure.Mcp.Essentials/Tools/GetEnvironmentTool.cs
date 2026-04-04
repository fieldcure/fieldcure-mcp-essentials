using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;

namespace FieldCure.Mcp.Essentials.Tools;

/// <summary>
/// MCP tool that returns current system environment information.
/// </summary>
[McpServerToolType]
public static class GetEnvironmentTool
{
    /// <summary>
    /// JSON serialization options shared across all responses.
    /// </summary>
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Returns system environment info as JSON.
    /// </summary>
    [McpServerTool(Name = "get_environment")]
    [Description("Get current system environment info — local time, timezone, OS, hostname, username, working directory, .NET version. No parameters needed.")]
    public static string GetEnvironment()
    {
        var now = DateTimeOffset.Now;
        var result = new
        {
            CurrentTime = now.ToString("o"),
            CurrentTimeUtc = now.UtcDateTime.ToString("o"),
            Timezone = TimeZoneInfo.Local.Id,
            Os = $"{RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})",
            Hostname = Environment.MachineName,
            Username = Environment.UserName,
            WorkingDirectory = Environment.CurrentDirectory,
            DotnetVersion = Environment.Version.ToString(),
        };

        return JsonSerializer.Serialize(result, JsonOptions);
    }
}
