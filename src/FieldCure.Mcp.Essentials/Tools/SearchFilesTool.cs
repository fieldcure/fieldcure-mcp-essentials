using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;

namespace FieldCure.Mcp.Essentials.Tools;

/// <summary>
/// MCP tool that searches for files by name pattern and optionally by content.
/// </summary>
[McpServerToolType]
public static class SearchFilesTool
{
    /// <summary>
    /// JSON serialization options shared across all responses.
    /// </summary>
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Searches for files matching a glob pattern and optional content filter.
    /// </summary>
    [McpServerTool(Name = "search_files")]
    [Description("Search for files by name pattern (glob) and optionally by content. Returns file paths, sizes, and modification dates. Content matches include a preview line.")]
    public static async Task<string> SearchFiles(
        [Description("Directory to search in")]
        string directory,
        [Description("Glob pattern, e.g. \"*.json\", \"*.cs\" (default: *)")]
        string? pattern = "*",
        [Description("Search subdirectories (default: true)")]
        bool recursive = true,
        [Description("Maximum results (default: 100)")]
        int max_results = 100,
        [Description("Search file content (case-insensitive grep). Only text files are searched.")]
        string? content_pattern = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var fullDir = Path.GetFullPath(directory);
            if (!Directory.Exists(fullDir))
                return JsonSerializer.Serialize(new { error = $"Directory not found: {fullDir}" }, JsonOptions);

            max_results = Math.Clamp(max_results, 1, 10_000);
            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

            var results = new List<object>();
            var totalFound = 0;

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(fullDir, pattern ?? "*", new EnumerationOptions
                {
                    RecurseSubdirectories = recursive,
                    IgnoreInaccessible = true,
                    MatchCasing = MatchCasing.CaseInsensitive,
                });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { error = $"Search failed: {ex.Message}" }, JsonOptions);
            }

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string? matchPreview = null;

                if (content_pattern is not null)
                {
                    matchPreview = await FindContentMatch(file, content_pattern, cancellationToken);
                    if (matchPreview is null) continue; // no content match
                }

                totalFound++;
                if (results.Count < max_results)
                {
                    var fi = new FileInfo(file);
                    results.Add(new
                    {
                        Path = file,
                        Size = fi.Length,
                        Modified = fi.LastWriteTime.ToString("o"),
                        MatchPreview = matchPreview,
                    });
                }
            }

            var response = new
            {
                Results = results,
                TotalFound = totalFound,
                Truncated = totalFound > max_results,
            };

            return JsonSerializer.Serialize(response, JsonOptions);
        }
        catch (OperationCanceledException)
        {
            return JsonSerializer.Serialize(new { error = "Search cancelled." }, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    /// <summary>
    /// Searches a file for the first line matching the given pattern (case-insensitive).
    /// </summary>
    static async Task<string?> FindContentMatch(string filePath, string pattern, CancellationToken ct)
    {
        try
        {
            // Skip large files (> 10 MB) and binary check
            var fi = new FileInfo(filePath);
            if (fi.Length > 10_485_760) return null;

            // Quick binary check (first 512 bytes)
            var sample = new byte[Math.Min(512, (int)fi.Length)];
            using (var fs = File.OpenRead(filePath))
            {
                _ = await fs.ReadAsync(sample.AsMemory(0, sample.Length), ct);
            }
            if (Array.IndexOf(sample, (byte)0) >= 0) return null;

            using var reader = new StreamReader(filePath);
            string? line;
            while ((line = await reader.ReadLineAsync(ct)) is not null)
            {
                if (line.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    return line.Length > 200 ? line[..200] + "..." : line;
            }
        }
        catch { /* skip unreadable files */ }

        return null;
    }
}
