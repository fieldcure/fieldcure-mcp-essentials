using FieldCure.DocumentParsers;
using FieldCure.DocumentParsers.Pdf.Ocr;

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

    /// <summary>
    /// Lazily initializes the OCR engine on first PDF parse.
    /// Avoids loading Tesseract native binaries until actually needed.
    /// </summary>
    static readonly Lazy<TesseractOcrEngine> OcrEngine = new(() =>
    {
        var engine = new TesseractOcrEngine();
        DocumentParsers.Pdf.DocumentParserFactoryExtensions.AddPdfSupport(engine);
        return engine;
    });

    /// <summary>
    /// Registers non-PDF parsers immediately (no native dependencies).
    /// PDF + OCR is registered lazily via <see cref="OcrEngine"/>.
    /// </summary>
    static DocumentHelper()
    {
        // Non-OCR PDF fallback for non-PDF document types that are already registered by Core
        // PDF registration happens lazily in EnsureOcrRegistered()
    }

    /// <summary>
    /// Ensures the OCR engine and PDF parser are registered.
    /// Called before PDF parsing to trigger lazy initialization.
    /// </summary>
    static void EnsureOcrRegistered() => _ = OcrEngine.Value;

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
        EnsureOcrRegistered();
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
