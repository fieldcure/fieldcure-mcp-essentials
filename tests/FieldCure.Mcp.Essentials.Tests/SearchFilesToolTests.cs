using System.Text.Json;
using FieldCure.Mcp.Essentials.Tools;

namespace FieldCure.Mcp.Essentials.Tests;

[TestClass]
public class SearchFilesToolTests
{
    string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"essentials_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        // Create test files
        File.WriteAllText(Path.Combine(_tempDir, "readme.md"), "# Hello World");
        File.WriteAllText(Path.Combine(_tempDir, "data.json"), "{\"key\": \"value\"}");
        File.WriteAllText(Path.Combine(_tempDir, "code.cs"), "var client = new HttpClient();");

        var subDir = Path.Combine(_tempDir, "sub");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "nested.md"), "# Nested");
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [TestMethod]
    public async Task GlobPattern()
    {
        var json = await SearchFilesTool.SearchFiles(_tempDir, pattern: "*.md");
        using var doc = JsonDocument.Parse(json);
        var results = doc.RootElement.GetProperty("results");
        Assert.AreEqual(2, results.GetArrayLength()); // readme.md + sub/nested.md
    }

    [TestMethod]
    public async Task NonRecursive()
    {
        var json = await SearchFilesTool.SearchFiles(_tempDir, pattern: "*.md", recursive: false);
        using var doc = JsonDocument.Parse(json);
        var results = doc.RootElement.GetProperty("results");
        Assert.AreEqual(1, results.GetArrayLength()); // only readme.md
    }

    [TestMethod]
    public async Task ContentPatternSearch()
    {
        var json = await SearchFilesTool.SearchFiles(_tempDir, content_pattern: "HttpClient");
        using var doc = JsonDocument.Parse(json);
        var results = doc.RootElement.GetProperty("results");
        Assert.AreEqual(1, results.GetArrayLength());

        var match = results[0];
        Assert.IsTrue(match.GetProperty("path").GetString()!.EndsWith("code.cs"));
        Assert.IsTrue(match.GetProperty("match_preview").GetString()!.Contains("HttpClient"));
    }

    [TestMethod]
    public async Task MaxResultsTruncation()
    {
        var json = await SearchFilesTool.SearchFiles(_tempDir, max_results: 2);
        using var doc = JsonDocument.Parse(json);
        var results = doc.RootElement.GetProperty("results");
        Assert.AreEqual(2, results.GetArrayLength());
        Assert.IsTrue(doc.RootElement.GetProperty("total_found").GetInt32() > 2);
        Assert.IsTrue(doc.RootElement.GetProperty("truncated").GetBoolean());
    }

    [TestMethod]
    public async Task DirectoryNotFound()
    {
        var json = await SearchFilesTool.SearchFiles(Path.Combine(_tempDir, "nonexistent"));
        using var doc = JsonDocument.Parse(json);
        Assert.IsTrue(doc.RootElement.GetProperty("error").GetString()!.Contains("not found"));
    }

    [TestMethod]
    public async Task ResultIncludesSizeAndModified()
    {
        var json = await SearchFilesTool.SearchFiles(_tempDir, pattern: "data.json", recursive: false);
        using var doc = JsonDocument.Parse(json);
        var item = doc.RootElement.GetProperty("results")[0];

        Assert.IsTrue(item.GetProperty("size").GetInt64() > 0);
        Assert.IsTrue(item.TryGetProperty("modified", out var mod));
        Assert.IsTrue(DateTimeOffset.TryParse(mod.GetString(), out _));
    }
}
