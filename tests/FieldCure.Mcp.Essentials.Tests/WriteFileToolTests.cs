using System.Text.Json;
using FieldCure.Mcp.Essentials.Tools;

namespace FieldCure.Mcp.Essentials.Tests;

[TestClass]
public class WriteFileToolTests
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
    public async Task OverwriteMode()
    {
        var filePath = Path.Combine(_tempDir, "out.txt");

        var json = await WriteFileTool.WriteFile(filePath, "hello world");
        using var doc = JsonDocument.Parse(json);
        Assert.IsTrue(doc.RootElement.GetProperty("created").GetBoolean());
        Assert.IsTrue(doc.RootElement.GetProperty("bytes_written").GetInt64() > 0);

        var content = await File.ReadAllTextAsync(filePath);
        Assert.AreEqual("hello world", content);
    }

    [TestMethod]
    public async Task AppendMode()
    {
        var filePath = Path.Combine(_tempDir, "append.txt");
        await File.WriteAllTextAsync(filePath, "first ");

        var json = await WriteFileTool.WriteFile(filePath, "second", mode: "append");
        using var doc = JsonDocument.Parse(json);
        Assert.IsFalse(doc.RootElement.GetProperty("created").GetBoolean());

        var content = await File.ReadAllTextAsync(filePath);
        Assert.AreEqual("first second", content);
    }

    [TestMethod]
    public async Task AutoCreateDirectory()
    {
        var filePath = Path.Combine(_tempDir, "sub", "deep", "file.txt");

        var json = await WriteFileTool.WriteFile(filePath, "nested content");
        using var doc = JsonDocument.Parse(json);
        Assert.IsTrue(File.Exists(filePath));
        Assert.AreEqual("nested content", await File.ReadAllTextAsync(filePath));
    }

    [TestMethod]
    public async Task ReturnsAbsolutePath()
    {
        var filePath = Path.Combine(_tempDir, "abs.txt");

        var json = await WriteFileTool.WriteFile(filePath, "test");
        using var doc = JsonDocument.Parse(json);
        var returnedPath = doc.RootElement.GetProperty("path").GetString()!;
        Assert.IsTrue(Path.IsPathFullyQualified(returnedPath));
    }

    [TestMethod]
    public async Task OverwriteExistingFile()
    {
        var filePath = Path.Combine(_tempDir, "replace.txt");
        await File.WriteAllTextAsync(filePath, "old content");

        var json = await WriteFileTool.WriteFile(filePath, "new content");
        using var doc = JsonDocument.Parse(json);
        Assert.IsFalse(doc.RootElement.GetProperty("created").GetBoolean());
        Assert.AreEqual("new content", await File.ReadAllTextAsync(filePath));
    }
}
