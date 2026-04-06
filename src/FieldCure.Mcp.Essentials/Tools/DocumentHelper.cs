using FieldCure.DocumentParsers;
using FieldCure.DocumentParsers.Pdf;

namespace FieldCure.Mcp.Essentials.Tools;

/// <summary>
/// Shared document parsing logic for web_fetch and read_file.
/// Routes binary content to the appropriate parser based on format.
/// </summary>
internal static class DocumentHelper
{
    /// <summary>
    /// Known binary document extensions that require specialized parsing.
    /// </summary>
    static readonly HashSet<string> BinaryExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx", ".hwpx", ".pptx", ".xlsx"
    };

    static DocumentHelper()
    {
        DocumentParserFactoryExtensions.AddPdfSupport();
    }

    /// <summary>
    /// Determines if a file extension requires binary document parsing.
    /// </summary>
    public static bool IsBinaryDocument(string extension)
        => BinaryExtensions.Contains(extension);

    /// <summary>
    /// Parses binary document bytes into Markdown text.
    /// </summary>
    public static string Parse(byte[] bytes, string extension)
    {
        var parser = DocumentParserFactory.GetParser(extension)
            ?? throw new NotSupportedException($"Unsupported document format: {extension}");
        return parser.ExtractText(bytes);
    }

    /// <summary>
    /// Maps Content-Type to file extension for web_fetch routing.
    /// Returns null if not a supported document type.
    /// </summary>
    public static string? ContentTypeToExtension(string? contentType) => contentType switch
    {
        "application/pdf" => ".pdf",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ".docx",
        "application/vnd.openxmlformats-officedocument.presentationml.presentation" => ".pptx",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => ".xlsx",
        _ => null,
    };

    /// <summary>
    /// Extracts file extension from URL path as fallback for unknown Content-Types (e.g. HWPX).
    /// Returns null if not a supported document type.
    /// </summary>
    public static string? UrlToExtension(string url)
    {
        try
        {
            var path = new Uri(url).AbsolutePath;
            var ext = Path.GetExtension(path);
            return IsBinaryDocument(ext) ? ext : null;
        }
        catch { return null; }
    }
}
