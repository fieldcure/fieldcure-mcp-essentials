using System.Text.Json;
using FieldCure.Mcp.Essentials.Tools;

namespace FieldCure.Mcp.Essentials.Tests;

[TestClass]
public class ReadFileToolTests
{
    string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"essentials_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [TestMethod]
    public async Task ReadExistingFile()
    {
        var filePath = Path.Combine(_tempDir, "test.txt");
        await File.WriteAllTextAsync(filePath, "line1\nline2\nline3");

        var json = await ReadFileTool.ReadFile(filePath);
        using var doc = JsonDocument.Parse(json);
        Assert.AreEqual("line1\nline2\nline3", doc.RootElement.GetProperty("content").GetString());
        Assert.AreEqual(3, doc.RootElement.GetProperty("lines_read").GetInt32());
        Assert.AreEqual(3, doc.RootElement.GetProperty("total_lines").GetInt32());
        Assert.IsFalse(doc.RootElement.GetProperty("truncated").GetBoolean());
    }

    [TestMethod]
    public async Task FileNotFound()
    {
        var json = await ReadFileTool.ReadFile(Path.Combine(_tempDir, "nonexistent.txt"));
        using var doc = JsonDocument.Parse(json);
        Assert.IsTrue(doc.RootElement.GetProperty("error").GetString()!.Contains("not found"));
    }

    [TestMethod]
    public async Task MaxLinesTruncation()
    {
        var filePath = Path.Combine(_tempDir, "large.txt");
        var lines = Enumerable.Range(1, 50).Select(i => $"line {i}");
        await File.WriteAllLinesAsync(filePath, lines);

        var json = await ReadFileTool.ReadFile(filePath, max_lines: 10);
        using var doc = JsonDocument.Parse(json);
        Assert.AreEqual(10, doc.RootElement.GetProperty("lines_read").GetInt32());
        Assert.AreEqual(50, doc.RootElement.GetProperty("total_lines").GetInt32());
        Assert.IsTrue(doc.RootElement.GetProperty("truncated").GetBoolean());
    }

    [TestMethod]
    public async Task OffsetParameter()
    {
        var filePath = Path.Combine(_tempDir, "offset.txt");
        var lines = Enumerable.Range(0, 10).Select(i => $"line{i}");
        await File.WriteAllLinesAsync(filePath, lines);

        var json = await ReadFileTool.ReadFile(filePath, offset: 5, max_lines: 3);
        using var doc = JsonDocument.Parse(json);
        var content = doc.RootElement.GetProperty("content").GetString()!;
        Assert.IsTrue(content.StartsWith("line5"));
        Assert.AreEqual(3, doc.RootElement.GetProperty("lines_read").GetInt32());
    }

    [TestMethod]
    public async Task BinaryFileDetection()
    {
        var filePath = Path.Combine(_tempDir, "binary.dat");
        var bytes = new byte[] { 0x48, 0x65, 0x6C, 0x00, 0x6F }; // "Hel\0o"
        await File.WriteAllBytesAsync(filePath, bytes);

        var json = await ReadFileTool.ReadFile(filePath);
        using var doc = JsonDocument.Parse(json);
        Assert.IsTrue(doc.RootElement.GetProperty("error").GetString()!.Contains("Binary"));
    }

    [TestMethod]
    public async Task Utf8Encoding()
    {
        var filePath = Path.Combine(_tempDir, "korean.txt");
        await File.WriteAllTextAsync(filePath, "한글 테스트");

        var json = await ReadFileTool.ReadFile(filePath);
        using var doc = JsonDocument.Parse(json);
        Assert.AreEqual("한글 테스트", doc.RootElement.GetProperty("content").GetString());
    }
}
