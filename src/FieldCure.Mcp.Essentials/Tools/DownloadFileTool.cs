using FieldCure.Mcp.Essentials.Configuration;
using FieldCure.Mcp.Essentials.Http;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace FieldCure.Mcp.Essentials.Tools;

/// <summary>
/// Downloads a file from a URL and saves it to disk.
/// Binary counterpart of <see cref="WebFetchTool"/>: fetch reads content,
/// download_file saves the original file bytes.
/// </summary>
[McpServerToolType]
public static class DownloadFileTool
{
    /// <summary>Maximum allowed download size: 100 MB.</summary>
    internal const long MaxDownloadBytes = 100 * 1024 * 1024;

    /// <summary>
    /// Shared HTTP client used by the MCP entry point. Tests call
    /// <see cref="DownloadFileCore"/> with their own client.
    /// </summary>
    static readonly HttpClient SharedClient = new(new HttpClientHandler
    {
        AutomaticDecompression = DecompressionMethods.All,
    })
    {
        Timeout = TimeSpan.FromMinutes(5),
        DefaultRequestHeaders =
        {
            { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36" },
        },
    };

    /// <summary>
    /// Conservative Content-Type to extension mapping used only when the URL
    /// and Content-Disposition do not already provide an extension.
    /// </summary>
    static readonly Dictionary<string, string> ContentTypeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["application/acrobat"] = ".pdf",
        ["application/epub+zip"] = ".epub",
        ["application/gzip"] = ".gz",
        ["application/haansofthwp"] = ".hwp",
        ["application/haansofthwpx"] = ".hwpx",
        ["application/jsonl"] = ".jsonl",
        ["application/pdf"] = ".pdf",
        ["application/rtf"] = ".rtf",
        ["application/vnd.hancom.hwp"] = ".hwp",
        ["application/vnd.hancom.hwpx"] = ".hwpx",
        ["application/vnd.ms-excel"] = ".xls",
        ["application/vnd.ms-excel.sheet.macroenabled.12"] = ".xlsm",
        ["application/vnd.ms-powerpoint"] = ".ppt",
        ["application/vnd.ms-powerpoint.presentation.macroenabled.12"] = ".pptm",
        ["application/vnd.ms-word.document.macroenabled.12"] = ".docm",
        ["application/vnd.oasis.opendocument.presentation"] = ".odp",
        ["application/vnd.oasis.opendocument.spreadsheet"] = ".ods",
        ["application/vnd.oasis.opendocument.text"] = ".odt",
        ["application/vnd.openxmlformats-officedocument.presentationml.presentation"] = ".pptx",
        ["application/vnd.openxmlformats-officedocument.presentationml.template"] = ".potx",
        ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"] = ".xlsx",
        ["application/vnd.openxmlformats-officedocument.spreadsheetml.template"] = ".xltx",
        ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = ".docx",
        ["application/vnd.openxmlformats-officedocument.wordprocessingml.template"] = ".dotx",
        ["application/x-7z-compressed"] = ".7z",
        ["application/x-bzip2"] = ".bz2",
        ["application/x-gzip"] = ".gz",
        ["application/x-hwp"] = ".hwp",
        ["application/x-hwpx"] = ".hwpx",
        ["application/x-pdf"] = ".pdf",
        ["application/x-rar-compressed"] = ".rar",
        ["application/x-tar"] = ".tar",
        ["application/x-xz"] = ".xz",
        ["application/zip"] = ".zip",
        ["application/json"] = ".json",
        ["application/msword"] = ".doc",
        ["application/octet-stream"] = ".bin",
        ["application/xhtml+xml"] = ".xhtml",
        ["application/xml"] = ".xml",
        ["application/x-jsonlines"] = ".jsonl",
        ["application/x-ndjson"] = ".jsonl",
        ["application/x-yaml"] = ".yaml",
        ["application/yaml"] = ".yaml",
        ["application/yml"] = ".yml",
        ["audio/mpeg"] = ".mp3",
        ["audio/ogg"] = ".ogg",
        ["audio/wav"] = ".wav",
        ["image/bmp"] = ".bmp",
        ["image/gif"] = ".gif",
        ["image/icon"] = ".ico",
        ["image/jpeg"] = ".jpg",
        ["image/jpg"] = ".jpg",
        ["image/pjpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/svg+xml"] = ".svg",
        ["image/tiff"] = ".tiff",
        ["image/vnd.microsoft.icon"] = ".ico",
        ["image/webp"] = ".webp",
        ["image/x-bmp"] = ".bmp",
        ["image/x-icon"] = ".ico",
        ["image/x-ms-bmp"] = ".bmp",
        ["text/jsonl"] = ".jsonl",
        ["text/csv"] = ".csv",
        ["text/html"] = ".html",
        ["text/markdown"] = ".md",
        ["text/plain"] = ".txt",
        ["text/xml"] = ".xml",
        ["text/x-yaml"] = ".yaml",
        ["text/yaml"] = ".yaml",
        ["video/mp4"] = ".mp4",
        ["video/mpeg"] = ".mpeg",
        ["video/ogg"] = ".ogv",
        ["video/quicktime"] = ".mov",
        ["video/webm"] = ".webm",
    };

    /// <summary>
    /// Downloads a file from a URL and saves it to a local path.
    /// </summary>
    /// <param name="settings">Resolved Essentials settings supplied by DI.</param>
    /// <param name="url">HTTP(S) URL to download.</param>
    /// <param name="save_path">Optional destination path. Relative paths resolve under <c>download_directory</c>.</param>
    /// <param name="overwrite">Whether an existing destination file may be replaced.</param>
    /// <param name="cancellationToken">Cancellation token supplied by the MCP host.</param>
    /// <returns>A JSON string describing the saved file or an error.</returns>
    [McpServerTool(Name = "download_file", Title = "Download file", Destructive = true)]
    [Description("""
        Downloads a file from a URL and saves it to a local path.

        RECOMMENDED for:
        - Saving PDFs, images, datasets, archives, or any binary file from the web
        - Keeping the original file bytes for later use

        AVOID for:
        - Reading web page content or extracting text -- use web_fetch instead
        - Making API calls or sending POST requests -- use http_request instead
        - Fetching HTML to convert to markdown -- use web_fetch instead

        Parameters:
        - url (required): The URL to download from
        - save_path (optional): File path to save to. Relative paths resolve under the configured download_directory.
        - overwrite (optional, default false): Whether to overwrite if file already exists
        """)]
    public static Task<string> DownloadFile(
        EssentialsSettings settings,
        [Description("URL to download from (http or https)")]
        string url,
        [Description("File path to save to. Relative paths resolve under the configured download_directory. If omitted, a filename is inferred from the response or URL.")]
        string? save_path = null,
        [Description("Whether to overwrite if the destination file already exists (default: false)")]
        bool overwrite = false,
        CancellationToken cancellationToken = default)
        => DownloadFileCore(
            SharedClient,
            settings,
            url,
            save_path,
            overwrite,
            skipSsrfCheck: false,
            maxDownloadBytes: MaxDownloadBytes,
            cancellationToken);

    /// <summary>
    /// Implements the download flow with injectable dependencies for unit tests.
    /// </summary>
    /// <param name="httpClient">HTTP client used to fetch the URL.</param>
    /// <param name="settings">Resolved Essentials settings.</param>
    /// <param name="url">HTTP(S) URL to download.</param>
    /// <param name="savePath">Optional destination path.</param>
    /// <param name="overwrite">Whether an existing destination file may be replaced.</param>
    /// <param name="skipSsrfCheck">Skips DNS-based SSRF checks for deterministic unit tests.</param>
    /// <param name="maxDownloadBytes">Maximum allowed response body size.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A JSON string describing the saved file or an error.</returns>
    internal static async Task<string> DownloadFileCore(
        HttpClient httpClient,
        EssentialsSettings settings,
        string url,
        string? savePath,
        bool overwrite,
        bool skipSsrfCheck,
        long maxDownloadBytes,
        CancellationToken cancellationToken)
    {
        var tempPath = "";

        try
        {
            var (uri, urlError) = SsrfGuard.ValidateUrl(url);
            if (uri is null)
                return JsonSerializer.Serialize(new { error = urlError }, McpJson.Options);

            if (!skipSsrfCheck)
            {
                var ssrfError = await SsrfGuard.CheckAsync(uri, cancellationToken);
                if (ssrfError is not null)
                    return JsonSerializer.Serialize(new { error = ssrfError }, McpJson.Options);
            }

            using var response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var contentLength = response.Content.Headers.ContentLength;
            if (contentLength is > 0 && contentLength > maxDownloadBytes)
            {
                return JsonSerializer.Serialize(new
                {
                    error = $"File too large: {contentLength.Value} bytes (max {maxDownloadBytes} bytes).",
                }, McpJson.Options);
            }

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
            var suggestedFileName = InferFileName(uri, response.Content.Headers, contentType);
            var fullPath = ResolveTargetPath(settings, savePath, suggestedFileName);

            ValidateWritablePath(fullPath);

            if (!overwrite && File.Exists(fullPath))
            {
                return JsonSerializer.Serialize(new
                {
                    error = $"File already exists: {fullPath}. Set overwrite=true to replace.",
                }, McpJson.Options);
            }

            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            tempPath = CreateTempPath(fullPath);
            await using (var fileStream = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await CopyToFileWithLimitAsync(response.Content, fileStream, maxDownloadBytes, cancellationToken);
                await fileStream.FlushAsync(cancellationToken);
            }

            CommitDownload(tempPath, fullPath, overwrite);
            tempPath = "";

            var fileInfo = new FileInfo(fullPath);
            return JsonSerializer.Serialize(new
            {
                status = "saved",
                path = fullPath,
                size_bytes = fileInfo.Length,
                content_type = contentType,
            }, McpJson.Options);
        }
        catch (OperationCanceledException)
        {
            DeleteTempFile(tempPath);
            return JsonSerializer.Serialize(new { error = "Download cancelled or timed out." }, McpJson.Options);
        }
        catch (HttpRequestException ex)
        {
            DeleteTempFile(tempPath);
            return JsonSerializer.Serialize(new { error = ex.Message }, McpJson.Options);
        }
        catch (Exception ex)
        {
            DeleteTempFile(tempPath);
            return JsonSerializer.Serialize(new { error = ex.Message }, McpJson.Options);
        }
    }

    /// <summary>
    /// Resolves the final destination path from the optional user path and inferred file name.
    /// </summary>
    /// <param name="settings">Resolved Essentials settings.</param>
    /// <param name="savePath">Optional user-provided destination path.</param>
    /// <param name="suggestedFileName">Filename inferred from response metadata or URL.</param>
    /// <returns>The absolute destination path.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a relative path attempts to escape <c>download_directory</c>.
    /// </exception>
    internal static string ResolveTargetPath(EssentialsSettings settings, string? savePath, string suggestedFileName)
    {
        var downloadDirectory = settings.GetResolvedDownloadDirectory();

        if (string.IsNullOrWhiteSpace(savePath))
            return Path.GetFullPath(Path.Combine(downloadDirectory, suggestedFileName));

        var rawPath = EssentialsSettings.ExpandHomeDirectory(savePath.Trim());
        var isAbsolutePath = Path.IsPathFullyQualified(rawPath);
        var isDirectoryPath = LooksLikeDirectoryPath(rawPath)
            || Directory.Exists(isAbsolutePath ? rawPath : Path.Combine(downloadDirectory, rawPath));

        if (isAbsolutePath)
        {
            var absolutePath = isDirectoryPath
                ? Path.Combine(rawPath, suggestedFileName)
                : rawPath;
            return Path.GetFullPath(absolutePath);
        }

        var combined = Path.Combine(downloadDirectory, rawPath);
        if (isDirectoryPath)
            combined = Path.Combine(combined, suggestedFileName);

        var fullPath = Path.GetFullPath(combined);
        if (!IsUnderOrSameDirectory(fullPath, downloadDirectory))
            throw new InvalidOperationException("Relative save_path must stay within the configured download_directory.");

        return fullPath;
    }

    /// <summary>
    /// Streams response content to the destination while enforcing the byte limit.
    /// </summary>
    /// <param name="content">HTTP response content.</param>
    /// <param name="destination">Open writable destination stream.</param>
    /// <param name="maxDownloadBytes">Maximum number of bytes allowed.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the stream exceeds the byte limit.</exception>
    static async Task CopyToFileWithLimitAsync(
        HttpContent content,
        Stream destination,
        long maxDownloadBytes,
        CancellationToken ct)
    {
        await using var source = await content.ReadAsStreamAsync(ct);
        var buffer = new byte[81920];
        long total = 0;

        while (true)
        {
            var read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
            if (read == 0)
                break;

            if (total + read > maxDownloadBytes)
                throw new InvalidOperationException($"File too large: exceeded {maxDownloadBytes} bytes.");

            await destination.WriteAsync(buffer.AsMemory(0, read), ct);
            total += read;
        }
    }

    /// <summary>
    /// Infers a safe local file name from Content-Disposition, URL path, or Content-Type.
    /// </summary>
    /// <param name="uri">Downloaded URI.</param>
    /// <param name="headers">HTTP content headers.</param>
    /// <param name="contentType">Response media type.</param>
    /// <returns>A sanitized filename.</returns>
    static string InferFileName(Uri uri, HttpContentHeaders headers, string contentType)
    {
        var fromHeader = GetContentDispositionFileName(headers.ContentDisposition);
        var fromUrl = GetUrlFileName(uri);
        var extension = GetExtensionForContentType(contentType);

        var fileName = !string.IsNullOrWhiteSpace(fromHeader)
            ? fromHeader!
            : !string.IsNullOrWhiteSpace(fromUrl)
                ? fromUrl!
                : BuildFallbackFileName(extension);

        fileName = SanitizeFileName(fileName);

        if (string.IsNullOrWhiteSpace(Path.GetExtension(fileName)) && !string.IsNullOrEmpty(extension))
            fileName += extension;

        return fileName;
    }

    /// <summary>
    /// Extracts a filename from a Content-Disposition header.
    /// </summary>
    /// <param name="contentDisposition">Parsed Content-Disposition header.</param>
    /// <returns>The filename, or <see langword="null"/> when none is present.</returns>
    static string? GetContentDispositionFileName(ContentDispositionHeaderValue? contentDisposition)
    {
        if (contentDisposition is null)
            return null;

        var fileName = contentDisposition.FileNameStar;
        if (string.IsNullOrWhiteSpace(fileName))
            fileName = contentDisposition.FileName;

        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        fileName = fileName.Trim().Trim('"');
        return Path.GetFileName(fileName);
    }

    /// <summary>
    /// Extracts and URL-decodes the last path segment from a URI.
    /// </summary>
    /// <param name="uri">Downloaded URI.</param>
    /// <returns>The URL filename, or <see langword="null"/> when the URI has no file segment.</returns>
    static string? GetUrlFileName(Uri uri)
    {
        var fileName = Path.GetFileName(uri.LocalPath);
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        return Uri.UnescapeDataString(fileName);
    }

    /// <summary>
    /// Builds a generated filename for URLs that do not provide one.
    /// </summary>
    /// <param name="extension">Optional extension including the leading dot.</param>
    /// <returns>A unique fallback filename.</returns>
    static string BuildFallbackFileName(string extension)
    {
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var shortId = Guid.NewGuid().ToString("N")[..9];
        return $"download-{timestamp}-{shortId}{extension}";
    }

    /// <summary>
    /// Removes directory components and replaces invalid filename characters.
    /// </summary>
    /// <param name="fileName">Candidate filename.</param>
    /// <returns>A filename safe to combine with a destination directory.</returns>
    static string SanitizeFileName(string fileName)
    {
        fileName = Path.GetFileName(fileName);
        foreach (var c in Path.GetInvalidFileNameChars())
            fileName = fileName.Replace(c, '_');

        fileName = fileName.Trim().Trim('.');
        return string.IsNullOrWhiteSpace(fileName)
            ? BuildFallbackFileName("")
            : fileName;
    }

    /// <summary>
    /// Returns a conservative extension for a known Content-Type.
    /// </summary>
    /// <param name="contentType">HTTP media type.</param>
    /// <returns>An extension including the leading dot, or an empty string.</returns>
    static string GetExtensionForContentType(string contentType)
        => ContentTypeExtensions.TryGetValue(contentType, out var extension)
            ? extension
            : "";

    /// <summary>
    /// Rejects writes to protected system directories.
    /// </summary>
    /// <param name="fullPath">Absolute destination path.</param>
    /// <exception cref="InvalidOperationException">Thrown when the path is protected.</exception>
    static void ValidateWritablePath(string fullPath)
    {
        var protectedDirectories = GetProtectedDirectories();
        foreach (var directory in protectedDirectories)
        {
            if (IsUnderOrSameDirectory(fullPath, directory))
                throw new InvalidOperationException($"Cannot write to protected directory: {fullPath}");
        }
    }

    /// <summary>
    /// Enumerates platform-specific protected directory roots.
    /// </summary>
    /// <returns>Directory roots that should not be written by this tool.</returns>
    static IEnumerable<string> GetProtectedDirectories()
    {
        if (OperatingSystem.IsWindows())
        {
            var windir = Environment.GetEnvironmentVariable("WINDIR");
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

            foreach (var path in new[] { windir, programFiles, programFilesX86 })
            {
                if (!string.IsNullOrWhiteSpace(path))
                    yield return path;
            }
        }
        else
        {
            foreach (var path in new[] { "/bin", "/etc", "/sbin", "/usr" })
                yield return path;
        }
    }

    /// <summary>
    /// Determines whether a path string syntactically denotes a directory.
    /// </summary>
    /// <param name="path">Path string to inspect.</param>
    /// <returns><see langword="true"/> when the path ends with a directory separator.</returns>
    static bool LooksLikeDirectoryPath(string path)
        => path.EndsWith(Path.DirectorySeparatorChar)
           || path.EndsWith(Path.AltDirectorySeparatorChar);

    /// <summary>
    /// Checks whether a path is equal to or below a directory root.
    /// </summary>
    /// <param name="path">Candidate path.</param>
    /// <param name="directory">Directory root.</param>
    /// <returns><see langword="true"/> when <paramref name="path"/> is inside <paramref name="directory"/>.</returns>
    static bool IsUnderOrSameDirectory(string path, string directory)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        var fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullDirectory = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return fullPath.Equals(fullDirectory, comparison)
               || fullPath.StartsWith(fullDirectory + Path.DirectorySeparatorChar, comparison)
               || fullPath.StartsWith(fullDirectory + Path.AltDirectorySeparatorChar, comparison);
    }

    /// <summary>
    /// Creates a unique temporary filename in the same directory as the final destination.
    /// </summary>
    /// <param name="finalPath">Final destination path.</param>
    /// <returns>A non-existing temporary path.</returns>
    /// <exception cref="IOException">Thrown when a unique temporary path cannot be produced.</exception>
    static string CreateTempPath(string finalPath)
    {
        var directory = Path.GetDirectoryName(finalPath) ?? Environment.CurrentDirectory;
        var fileName = Path.GetFileName(finalPath);

        for (var i = 0; i < 10; i++)
        {
            var candidate = Path.Combine(directory, $".{fileName}.download-{Guid.NewGuid():N}.tmp");
            if (!File.Exists(candidate))
                return candidate;
        }

        throw new IOException("Could not create a unique temporary file name.");
    }

    /// <summary>
    /// Atomically commits a completed temporary file to the final destination.
    /// </summary>
    /// <param name="tempPath">Completed temporary file path.</param>
    /// <param name="fullPath">Final destination path.</param>
    /// <param name="overwrite">Whether an existing destination file may be replaced.</param>
    static void CommitDownload(string tempPath, string fullPath, bool overwrite)
        => File.Move(tempPath, fullPath, overwrite);

    /// <summary>
    /// Deletes a temporary file on a best-effort basis.
    /// </summary>
    /// <param name="tempPath">Temporary file path.</param>
    static void DeleteTempFile(string tempPath)
    {
        if (string.IsNullOrEmpty(tempPath))
            return;

        try
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
        catch
        {
            // Best-effort cleanup. The user-facing error should report the original failure.
        }
    }
}
