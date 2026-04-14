using System.ComponentModel;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace FieldCure.Mcp.Essentials.Tools;

/// <summary>
/// MCP tool that reads file content with offset and line limit support.
/// Supports text files and binary documents (PDF, DOCX, HWPX, PPTX, XLSX).
/// </summary>
[McpServerToolType]
public static class ReadFileTool
{
    /// <summary>
    /// Default maximum character length for document content.
    /// </summary>
    const int DefaultDocMaxLength = 5000;

    /// <summary>
    /// Absolute upper bound for max_length parameter on documents.
    /// </summary>
    const int AbsoluteDocMaxLength = 20000;

    /// <summary>
    /// Reads a file and returns its content as JSON.
    /// Binary documents (PDF, DOCX, HWPX, PPTX, XLSX) are parsed into Markdown.
    /// Text files are read with offset/max_lines pagination.
    /// </summary>
    [McpServerTool(Name = "read_file")]
    [Description("Read a file. Text files support offset/max_lines pagination. Documents (PDF, DOCX, HWPX, PPTX, XLSX) are parsed into Markdown.")]
    public static async Task<string> ReadFile(
        [Description("File path (absolute or relative to working directory)")]
        string path,
        [Description("File encoding for text files (default: utf-8)")]
        string? encoding = "utf-8",
        [Description("Maximum lines to read for text files (default: 1000)")]
        int max_lines = 1000,
        [Description("Start line number for text files, 0-based (default: 0)")]
        int offset = 0,
        [Description("Maximum character length for document output (default: 5000, max: 20000)")]
        int max_length = DefaultDocMaxLength,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);

            if (!File.Exists(fullPath))
                return JsonSerializer.Serialize(new { error = $"File not found: {fullPath}" }, McpJson.Options);

            var extension = Path.GetExtension(fullPath);

            if (DocumentHelper.IsBinaryDocument(extension))
                return await HandleDocument(fullPath, extension, max_length, cancellationToken);

            return await HandleText(fullPath, encoding ?? "utf-8", max_lines, offset, cancellationToken);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, McpJson.Options);
        }
    }

    /// <summary>
    /// Reads a binary document file and parses it into Markdown via DocumentHelper.
    /// </summary>
    static async Task<string> HandleDocument(string fullPath, string extension, int maxLength, CancellationToken ct)
    {
        maxLength = Math.Clamp(maxLength, 100, AbsoluteDocMaxLength);
        var bytes = await File.ReadAllBytesAsync(fullPath, ct);
        var text = DocumentHelper.Parse(bytes, extension);

        var truncated = text.Length > maxLength;
        if (truncated)
            text = text[..maxLength];

        return JsonSerializer.Serialize(new
        {
            Content = text,
            Format = extension.TrimStart('.'),
            Length = text.Length,
            Truncated = truncated ? true : (bool?)null,
        }, McpJson.Options);
    }

    /// <summary>
    /// Reads a text file with encoding, offset, and line limit support.
    /// </summary>
    static async Task<string> HandleText(string fullPath, string encoding, int maxLines, int offset, CancellationToken ct)
    {
        var enc = GetEncoding(encoding);

        // Check for binary file (first 8KB)
        var fileLength = new FileInfo(fullPath).Length;
        var sample = new byte[Math.Min(8192, fileLength)];
        using (var fs = File.OpenRead(fullPath))
        {
            _ = await fs.ReadAsync(sample.AsMemory(0, sample.Length), ct);
        }
        if (Array.IndexOf(sample, (byte)0) >= 0)
            return JsonSerializer.Serialize(new { error = "Binary file detected. Use a specialized tool for binary files." }, McpJson.Options);

        var allLines = await File.ReadAllLinesAsync(fullPath, enc, ct);
        var totalLines = allLines.Length;

        offset = Math.Clamp(offset, 0, Math.Max(0, totalLines - 1));
        maxLines = Math.Clamp(maxLines, 1, 10_000);

        var selectedLines = allLines.Skip(offset).Take(maxLines).ToArray();
        var truncated = offset + maxLines < totalLines;

        return JsonSerializer.Serialize(new
        {
            Content = string.Join('\n', selectedLines),
            LinesRead = selectedLines.Length,
            TotalLines = totalLines,
            Truncated = truncated,
        }, McpJson.Options);
    }

    /// <summary>
    /// Resolves a named encoding to an <see cref="Encoding"/> instance.
    /// </summary>
    static Encoding GetEncoding(string name) => name.ToLowerInvariant() switch
    {
        "utf-8" or "utf8" => new UTF8Encoding(false),
        "utf-16" or "utf16" or "unicode" => Encoding.Unicode,
        "ascii" => Encoding.ASCII,
        "latin1" or "iso-8859-1" => Encoding.Latin1,
        _ => Encoding.GetEncoding(name),
    };
}
