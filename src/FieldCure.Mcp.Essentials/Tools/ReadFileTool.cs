using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;

namespace FieldCure.Mcp.Essentials.Tools;

[McpServerToolType]
public static class ReadFileTool
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    [McpServerTool(Name = "read_file")]
    [Description("Read text file content. Supports offset and max_lines for large files. Returns line count and truncation status.")]
    public static async Task<string> ReadFile(
        [Description("File path (absolute or relative to working directory)")]
        string path,
        [Description("File encoding (default: utf-8)")]
        string? encoding = "utf-8",
        [Description("Maximum lines to read (default: 1000)")]
        int max_lines = 1000,
        [Description("Start line number, 0-based (default: 0)")]
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);

            if (!File.Exists(fullPath))
                return JsonSerializer.Serialize(new { error = $"File not found: {fullPath}" }, JsonOptions);

            var enc = GetEncoding(encoding ?? "utf-8");

            // Check for binary file (first 8KB)
            var sample = new byte[Math.Min(8192, new FileInfo(fullPath).Length)];
            using (var fs = File.OpenRead(fullPath))
            {
                _ = await fs.ReadAsync(sample.AsMemory(0, sample.Length), cancellationToken);
            }
            if (Array.IndexOf(sample, (byte)0) >= 0)
                return JsonSerializer.Serialize(new { error = "Binary file detected. Use a specialized tool for binary files." }, JsonOptions);

            var allLines = await File.ReadAllLinesAsync(fullPath, enc, cancellationToken);
            var totalLines = allLines.Length;

            offset = Math.Clamp(offset, 0, Math.Max(0, totalLines - 1));
            max_lines = Math.Clamp(max_lines, 1, 10_000);

            var selectedLines = allLines.Skip(offset).Take(max_lines).ToArray();
            var truncated = offset + max_lines < totalLines;

            var result = new
            {
                Content = string.Join('\n', selectedLines),
                LinesRead = selectedLines.Length,
                TotalLines = totalLines,
                Truncated = truncated,
            };

            return JsonSerializer.Serialize(result, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    static Encoding GetEncoding(string name) => name.ToLowerInvariant() switch
    {
        "utf-8" or "utf8" => new UTF8Encoding(false),
        "utf-16" or "utf16" or "unicode" => Encoding.Unicode,
        "ascii" => Encoding.ASCII,
        "latin1" or "iso-8859-1" => Encoding.Latin1,
        _ => Encoding.GetEncoding(name),
    };
}
