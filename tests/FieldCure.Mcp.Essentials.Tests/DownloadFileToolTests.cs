using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using FieldCure.Mcp.Essentials.Configuration;
using FieldCure.Mcp.Essentials.Tools;

namespace FieldCure.Mcp.Essentials.Tests;

[TestClass]
public class DownloadFileToolTests
{
    string _tempDir = null!;
    string _downloadDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"essentials_download_test_{Guid.NewGuid():N}");
        _downloadDir = Path.Combine(_tempDir, "downloads");
        Directory.CreateDirectory(_downloadDir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [TestMethod]
    public async Task DownloadsFileToRelativePath()
    {
        var client = CreateClient([1, 2, 3], "application/pdf");
        var settings = CreateSettings();

        var json = await DownloadFileTool.DownloadFileCore(
            client,
            settings,
            "https://example.com/source.pdf",
            "papers/out.pdf",
            overwrite: false,
            skipSsrfCheck: true,
            maxDownloadBytes: 100,
            CancellationToken.None);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.AreEqual("saved", root.GetProperty("status").GetString());
        Assert.AreEqual("application/pdf", root.GetProperty("content_type").GetString());
        Assert.AreEqual(3, root.GetProperty("size_bytes").GetInt64());

        var path = root.GetProperty("path").GetString()!;
        Assert.AreEqual(Path.Combine(_downloadDir, "papers", "out.pdf"), path);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, await File.ReadAllBytesAsync(path));
    }

    [TestMethod]
    public async Task InfersFileNameFromContentDispositionWhenSavePathOmitted()
    {
        var client = CreateClient([10, 20], "image/png", fileName: "image.png");
        var settings = CreateSettings();

        var json = await DownloadFileTool.DownloadFileCore(
            client,
            settings,
            "https://example.com/download",
            savePath: null,
            overwrite: false,
            skipSsrfCheck: true,
            maxDownloadBytes: 100,
            CancellationToken.None);

        using var doc = JsonDocument.Parse(json);
        var path = doc.RootElement.GetProperty("path").GetString()!;
        Assert.AreEqual(Path.Combine(_downloadDir, "image.png"), path);
        Assert.IsTrue(File.Exists(path));
    }

    [TestMethod]
    public async Task InfersFileNameFromUrlWhenSavePathOmitted()
    {
        var client = CreateClient([42], "application/pdf");

        var json = await DownloadFileTool.DownloadFileCore(
            client,
            CreateSettings(),
            "https://example.com/files/report.pdf?token=abc",
            savePath: null,
            overwrite: false,
            skipSsrfCheck: true,
            maxDownloadBytes: 100,
            CancellationToken.None);

        using var doc = JsonDocument.Parse(json);
        var path = doc.RootElement.GetProperty("path").GetString()!;
        Assert.AreEqual(Path.Combine(_downloadDir, "report.pdf"), path);
        CollectionAssert.AreEqual(new byte[] { 42 }, await File.ReadAllBytesAsync(path));
    }

    [TestMethod]
    public async Task AppendsExtensionFromContentTypeWhenUrlFilenameHasNone()
    {
        var client = CreateClient([1, 2], "text/csv");

        var json = await DownloadFileTool.DownloadFileCore(
            client,
            CreateSettings(),
            "https://example.com/export",
            savePath: null,
            overwrite: false,
            skipSsrfCheck: true,
            maxDownloadBytes: 100,
            CancellationToken.None);

        using var doc = JsonDocument.Parse(json);
        var path = doc.RootElement.GetProperty("path").GetString()!;
        Assert.AreEqual(Path.Combine(_downloadDir, "export.csv"), path);
        Assert.IsTrue(File.Exists(path));
    }

    [TestMethod]
    public async Task AppendsHtmlExtensionForHtmlFallbackName()
    {
        var client = CreateClient([60, 33], "text/html");

        var json = await DownloadFileTool.DownloadFileCore(
            client,
            CreateSettings(),
            "https://example.com/",
            savePath: null,
            overwrite: false,
            skipSsrfCheck: true,
            maxDownloadBytes: 100,
            CancellationToken.None);

        using var doc = JsonDocument.Parse(json);
        var path = doc.RootElement.GetProperty("path").GetString()!;
        Assert.AreEqual(".html", Path.GetExtension(path));
        StringAssert.StartsWith(Path.GetFileName(path), "download-");
        Assert.IsTrue(File.Exists(path));
    }

    [TestMethod]
    [DataRow("application/pdf", ".pdf")]
    [DataRow("application/msword", ".doc")]
    [DataRow("application/vnd.openxmlformats-officedocument.wordprocessingml.document", ".docx")]
    [DataRow("application/vnd.openxmlformats-officedocument.presentationml.presentation", ".pptx")]
    [DataRow("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", ".xlsx")]
    [DataRow("application/vnd.ms-excel", ".xls")]
    [DataRow("application/vnd.ms-powerpoint", ".ppt")]
    [DataRow("application/vnd.hancom.hwpx", ".hwpx")]
    [DataRow("application/vnd.oasis.opendocument.text", ".odt")]
    [DataRow("application/zip", ".zip")]
    [DataRow("application/x-7z-compressed", ".7z")]
    [DataRow("application/octet-stream", ".bin")]
    [DataRow("application/x-ndjson", ".jsonl")]
    [DataRow("application/x-yaml", ".yaml")]
    [DataRow("audio/mpeg", ".mp3")]
    [DataRow("image/bmp", ".bmp")]
    [DataRow("image/jpeg", ".jpg")]
    [DataRow("image/png", ".png")]
    [DataRow("image/svg+xml", ".svg")]
    [DataRow("image/tiff", ".tiff")]
    [DataRow("image/vnd.microsoft.icon", ".ico")]
    [DataRow("image/webp", ".webp")]
    [DataRow("text/yaml", ".yaml")]
    [DataRow("video/mp4", ".mp4")]
    public async Task AppendsExpectedExtensionForCommonContentTypes(string contentType, string expectedExtension)
    {
        var client = CreateClient([1], contentType);

        var json = await DownloadFileTool.DownloadFileCore(
            client,
            CreateSettings(),
            "https://example.com/",
            savePath: null,
            overwrite: false,
            skipSsrfCheck: true,
            maxDownloadBytes: 100,
            CancellationToken.None);

        using var doc = JsonDocument.Parse(json);
        var path = doc.RootElement.GetProperty("path").GetString()!;
        Assert.AreEqual(expectedExtension, Path.GetExtension(path));
        Assert.IsTrue(File.Exists(path));
    }

    [TestMethod]
    public async Task RelativeDirectorySavePathUsesInferredFileName()
    {
        var client = CreateClient([8], "application/pdf");

        var json = await DownloadFileTool.DownloadFileCore(
            client,
            CreateSettings(),
            "https://example.com/manual.pdf",
            "docs/",
            overwrite: false,
            skipSsrfCheck: true,
            maxDownloadBytes: 100,
            CancellationToken.None);

        using var doc = JsonDocument.Parse(json);
        var path = doc.RootElement.GetProperty("path").GetString()!;
        Assert.AreEqual(Path.Combine(_downloadDir, "docs", "manual.pdf"), path);
        CollectionAssert.AreEqual(new byte[] { 8 }, await File.ReadAllBytesAsync(path));
    }

    [TestMethod]
    public async Task AbsoluteSavePathIsUsedDirectly()
    {
        var client = CreateClient([6, 7]);
        var absolutePath = Path.Combine(_tempDir, "absolute.bin");

        var json = await DownloadFileTool.DownloadFileCore(
            client,
            CreateSettings(),
            "https://example.com/absolute.bin",
            absolutePath,
            overwrite: false,
            skipSsrfCheck: true,
            maxDownloadBytes: 100,
            CancellationToken.None);

        using var doc = JsonDocument.Parse(json);
        var path = doc.RootElement.GetProperty("path").GetString()!;
        Assert.AreEqual(absolutePath, path);
        CollectionAssert.AreEqual(new byte[] { 6, 7 }, await File.ReadAllBytesAsync(path));
    }

    [TestMethod]
    public async Task BlocksSsrfPrivateAddress()
    {
        var json = await DownloadFileTool.DownloadFile(
            CreateSettings(),
            "http://127.0.0.1/file.pdf",
            "blocked.pdf",
            cancellationToken: CancellationToken.None);

        using var doc = JsonDocument.Parse(json);
        StringAssert.Contains(doc.RootElement.GetProperty("error").GetString()!, "SSRF");
    }

    [TestMethod]
    public async Task ExistingFileWithoutOverwriteReturnsError()
    {
        var path = Path.Combine(_downloadDir, "existing.bin");
        await File.WriteAllBytesAsync(path, [9, 9, 9]);

        var client = CreateClient([1, 2, 3]);
        var json = await DownloadFileTool.DownloadFileCore(
            client,
            CreateSettings(),
            "https://example.com/existing.bin",
            "existing.bin",
            overwrite: false,
            skipSsrfCheck: true,
            maxDownloadBytes: 100,
            CancellationToken.None);

        using var doc = JsonDocument.Parse(json);
        StringAssert.Contains(doc.RootElement.GetProperty("error").GetString()!, "File already exists");
        CollectionAssert.AreEqual(new byte[] { 9, 9, 9 }, await File.ReadAllBytesAsync(path));
    }

    [TestMethod]
    public async Task ExistingFileWithOverwriteReplacesAtomically()
    {
        var path = Path.Combine(_downloadDir, "replace.bin");
        await File.WriteAllBytesAsync(path, [9, 9, 9]);

        var client = CreateClient([1, 2, 3, 4]);
        var json = await DownloadFileTool.DownloadFileCore(
            client,
            CreateSettings(),
            "https://example.com/replace.bin",
            "replace.bin",
            overwrite: true,
            skipSsrfCheck: true,
            maxDownloadBytes: 100,
            CancellationToken.None);

        using var doc = JsonDocument.Parse(json);
        Assert.AreEqual("saved", doc.RootElement.GetProperty("status").GetString());
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, await File.ReadAllBytesAsync(path));
    }

    [TestMethod]
    public async Task BlocksProtectedPath()
    {
        var protectedPath = OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetEnvironmentVariable("WINDIR") ?? @"C:\Windows", "fieldcure-test-download.bin")
            : "/etc/fieldcure-test-download.bin";

        var client = CreateClient([1, 2, 3]);
        var json = await DownloadFileTool.DownloadFileCore(
            client,
            CreateSettings(),
            "https://example.com/file.bin",
            protectedPath,
            overwrite: true,
            skipSsrfCheck: true,
            maxDownloadBytes: 100,
            CancellationToken.None);

        using var doc = JsonDocument.Parse(json);
        StringAssert.Contains(doc.RootElement.GetProperty("error").GetString()!, "protected directory");
    }

    [TestMethod]
    public async Task ContentLengthOverLimitReturnsError()
    {
        var client = CreateClient([], contentLength: 101);
        var target = Path.Combine(_downloadDir, "too-large.bin");

        var json = await DownloadFileTool.DownloadFileCore(
            client,
            CreateSettings(),
            "https://example.com/too-large.bin",
            "too-large.bin",
            overwrite: false,
            skipSsrfCheck: true,
            maxDownloadBytes: 100,
            CancellationToken.None);

        using var doc = JsonDocument.Parse(json);
        StringAssert.Contains(doc.RootElement.GetProperty("error").GetString()!, "File too large");
        Assert.IsFalse(File.Exists(target));
    }

    [TestMethod]
    public async Task StreamOverLimitWithoutContentLengthDeletesTempFile()
    {
        var client = CreateClient(Enumerable.Range(0, 101).Select(i => (byte)i).ToArray());
        var target = Path.Combine(_downloadDir, "stream-too-large.bin");

        var json = await DownloadFileTool.DownloadFileCore(
            client,
            CreateSettings(),
            "https://example.com/stream-too-large.bin",
            "stream-too-large.bin",
            overwrite: false,
            skipSsrfCheck: true,
            maxDownloadBytes: 100,
            CancellationToken.None);

        using var doc = JsonDocument.Parse(json);
        StringAssert.Contains(doc.RootElement.GetProperty("error").GetString()!, "File too large");
        Assert.IsFalse(File.Exists(target));
        Assert.AreEqual(0, Directory.GetFiles(_downloadDir, "*.tmp").Length);
    }

    [TestMethod]
    public async Task AutoCreatesDirectory()
    {
        var client = CreateClient([1]);
        var settings = CreateSettings();

        var json = await DownloadFileTool.DownloadFileCore(
            client,
            settings,
            "https://example.com/data.csv",
            "new/deep/data.csv",
            overwrite: false,
            skipSsrfCheck: true,
            maxDownloadBytes: 100,
            CancellationToken.None);

        using var doc = JsonDocument.Parse(json);
        var path = doc.RootElement.GetProperty("path").GetString()!;
        Assert.AreEqual(Path.Combine(_downloadDir, "new", "deep", "data.csv"), path);
        Assert.IsTrue(File.Exists(path));
    }

    [TestMethod]
    public async Task RelativePathCannotEscapeDownloadDirectory()
    {
        var client = CreateClient([1]);
        var json = await DownloadFileTool.DownloadFileCore(
            client,
            CreateSettings(),
            "https://example.com/file.bin",
            "../outside.bin",
            overwrite: false,
            skipSsrfCheck: true,
            maxDownloadBytes: 100,
            CancellationToken.None);

        using var doc = JsonDocument.Parse(json);
        StringAssert.Contains(doc.RootElement.GetProperty("error").GetString()!, "download_directory");
        Assert.IsFalse(File.Exists(Path.Combine(_tempDir, "outside.bin")));
    }

    [TestMethod]
    public async Task ContentDispositionFileNameCannotEscapeDownloadDirectory()
    {
        var client = CreateClient([1], "application/pdf", fileName: "../evil.pdf");

        var json = await DownloadFileTool.DownloadFileCore(
            client,
            CreateSettings(),
            "https://example.com/download",
            savePath: null,
            overwrite: false,
            skipSsrfCheck: true,
            maxDownloadBytes: 100,
            CancellationToken.None);

        using var doc = JsonDocument.Parse(json);
        var path = doc.RootElement.GetProperty("path").GetString()!;
        Assert.AreEqual(Path.Combine(_downloadDir, "evil.pdf"), path);
        Assert.IsFalse(File.Exists(Path.Combine(_tempDir, "evil.pdf")));
    }

    /// <summary>
    /// Creates settings that point downloads at this test's temporary directory.
    /// </summary>
    EssentialsSettings CreateSettings() => new()
    {
        DownloadDirectory = _downloadDir,
    };

    /// <summary>
    /// Creates an HTTP client that returns a deterministic in-memory response.
    /// </summary>
    static HttpClient CreateClient(
        byte[] bytes,
        string contentType = "application/octet-stream",
        string? fileName = null,
        long? contentLength = null)
    {
        return new HttpClient(new FakeHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(bytes),
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            if (contentLength is not null)
                response.Content.Headers.ContentLength = contentLength;
            if (fileName is not null)
            {
                response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
                {
                    FileName = $"\"{fileName}\"",
                };
            }

            return response;
        }));
    }

    /// <summary>
    /// Minimal HTTP handler used to stub network responses in unit tests.
    /// </summary>
    sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        /// <inheritdoc />
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }
}
