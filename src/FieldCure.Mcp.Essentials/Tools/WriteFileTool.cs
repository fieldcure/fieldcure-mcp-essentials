using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;

namespace FieldCure.Mcp.Essentials.Tools;

[McpServerToolType]
public static class WriteFileTool
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    [McpServerTool(Name = "write_file", Destructive = true)]
    [Description("Write or append text to a file. Auto-creates parent directories. Returns absolute path and byte count.")]
    public static async Task<string> WriteFile(
        [Description("File path (absolute or relative to working directory)")]
        string path,
        [Description("Content to write")]
        string content,
        [Description("Write mode: \"overwrite\" or \"append\" (default: overwrite)")]
        string? mode = "overwrite",
        [Description("File encoding (default: utf-8)")]
        string? encoding = "utf-8",
        [Description("Auto-create parent directories (default: true)")]
        bool create_directory = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            var created = !File.Exists(fullPath);

            if (create_directory)
            {
                var dir = Path.GetDirectoryName(fullPath);
                if (dir is not null)
                    Directory.CreateDirectory(dir);
            }

            var enc = GetEncoding(encoding ?? "utf-8");
            var isAppend = mode?.Equals("append", StringComparison.OrdinalIgnoreCase) == true;

            if (isAppend)
                await File.AppendAllTextAsync(fullPath, content, enc, cancellationToken);
            else
                await File.WriteAllTextAsync(fullPath, content, enc, cancellationToken);

            var fileInfo = new FileInfo(fullPath);

            var result = new
            {
                Path = fullPath,
                BytesWritten = fileInfo.Length,
                Created = created,
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
