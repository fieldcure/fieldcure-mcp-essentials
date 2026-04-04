using Microsoft.Data.Sqlite;

namespace FieldCure.Mcp.Essentials.Memory;

/// <summary>
/// Persistent key-value memory store backed by SQLite with FTS5 full-text search.
/// Default path: %LOCALAPPDATA%/FieldCure/Mcp.Essentials/memory.db.
/// Override via --memory-path CLI arg or ESSENTIALS_MEMORY_PATH env var.
/// </summary>
public sealed class MemoryStore : IDisposable
{
    private readonly string _connectionString;

    /// <summary>Recommended limit for system prompt injection (most recent N entries).</summary>
    public const int PromptInjectionLimit = 50;

    /// <summary>
    /// Initializes the memory store with the given SQLite database path.
    /// </summary>
    public MemoryStore(string dbPath)
    {
        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        _connectionString = $"Data Source={dbPath}";
        Initialize();
    }

    /// <summary>
    /// Returns the default database path under %LOCALAPPDATA%.
    /// </summary>
    public static string GetDefaultPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "FieldCure", "Mcp.Essentials", "memory.db");
    }

    /// <summary>
    /// Resolves the database path from CLI args, environment variable, or default.
    /// </summary>
    public static string ResolvePath(string[] args)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] is "--memory-path" or "-m")
                return Path.GetFullPath(args[i + 1]);
        }

        var envPath = Environment.GetEnvironmentVariable("ESSENTIALS_MEMORY_PATH");
        if (!string.IsNullOrWhiteSpace(envPath))
            return Path.GetFullPath(envPath);

        return GetDefaultPath();
    }

    /// <summary>
    /// Upserts a memory entry. Returns (created, updated).
    /// </summary>
    public (bool Created, bool Updated) Upsert(string key, string value)
    {
        using var conn = Open();
        var now = DateTimeOffset.UtcNow.ToString("o");

        // Check if exists
        using var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = "SELECT COUNT(*) FROM Memories WHERE Key = @key";
        checkCmd.Parameters.AddWithValue("@key", key);
        var exists = Convert.ToInt32(checkCmd.ExecuteScalar()) > 0;

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Memories (Key, Value, CreatedAt, UpdatedAt)
            VALUES (@key, @value, @now, @now)
            ON CONFLICT(Key) DO UPDATE SET Value = @value, UpdatedAt = @now
            """;
        cmd.Parameters.AddWithValue("@key", key);
        cmd.Parameters.AddWithValue("@value", value);
        cmd.Parameters.AddWithValue("@now", now);
        cmd.ExecuteNonQuery();

        // Sync FTS index
        if (exists)
        {
            using var ftsCmd = conn.CreateCommand();
            ftsCmd.CommandText = """
                DELETE FROM MemoriesFts WHERE rowid = (SELECT rowid FROM Memories WHERE Key = @key);
                INSERT INTO MemoriesFts(rowid, Key, Value)
                    SELECT rowid, Key, Value FROM Memories WHERE Key = @key;
                """;
            ftsCmd.Parameters.AddWithValue("@key", key);
            ftsCmd.ExecuteNonQuery();
        }
        else
        {
            using var ftsCmd = conn.CreateCommand();
            ftsCmd.CommandText = """
                INSERT INTO MemoriesFts(rowid, Key, Value)
                    SELECT rowid, Key, Value FROM Memories WHERE Key = @key;
                """;
            ftsCmd.Parameters.AddWithValue("@key", key);
            ftsCmd.ExecuteNonQuery();
        }

        return (Created: !exists, Updated: exists);
    }

    /// <summary>
    /// Deletes a memory by exact key. Returns true if deleted.
    /// </summary>
    public bool DeleteByKey(string key)
    {
        using var conn = Open();

        // Delete from FTS first
        using var ftsCmd = conn.CreateCommand();
        ftsCmd.CommandText = "DELETE FROM MemoriesFts WHERE rowid = (SELECT rowid FROM Memories WHERE Key = @key)";
        ftsCmd.Parameters.AddWithValue("@key", key);
        ftsCmd.ExecuteNonQuery();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Memories WHERE Key = @key";
        cmd.Parameters.AddWithValue("@key", key);
        return cmd.ExecuteNonQuery() > 0;
    }

    /// <summary>
    /// Deletes memories matching a FTS5 query. Returns deleted keys.
    /// </summary>
    public List<string> DeleteByQuery(string query)
    {
        using var conn = Open();

        // Find matching keys via FTS, fall back to LIKE
        var keys = new List<string>();
        try
        {
            using var findCmd = conn.CreateCommand();
            findCmd.CommandText = """
                SELECT m.Key FROM Memories m
                JOIN MemoriesFts f ON m.rowid = f.rowid
                WHERE MemoriesFts MATCH @query
                """;
            findCmd.Parameters.AddWithValue("@query", EscapeFtsQuery(query));
            using var reader = findCmd.ExecuteReader();
            while (reader.Read())
                keys.Add(reader.GetString(0));
        }
        catch
        {
            using var likeCmd = conn.CreateCommand();
            likeCmd.CommandText = "SELECT Key FROM Memories WHERE Key LIKE @q OR Value LIKE @q";
            likeCmd.Parameters.AddWithValue("@q", $"%{query}%");
            using var reader = likeCmd.ExecuteReader();
            while (reader.Read())
                keys.Add(reader.GetString(0));
        }

        if (keys.Count == 0)
            return keys;

        // Delete from FTS
        foreach (var key in keys)
        {
            using var ftsCmd = conn.CreateCommand();
            ftsCmd.CommandText = "DELETE FROM MemoriesFts WHERE rowid = (SELECT rowid FROM Memories WHERE Key = @key)";
            ftsCmd.Parameters.AddWithValue("@key", key);
            ftsCmd.ExecuteNonQuery();
        }

        // Delete from main table
        foreach (var key in keys)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM Memories WHERE Key = @key";
            cmd.Parameters.AddWithValue("@key", key);
            cmd.ExecuteNonQuery();
        }

        return keys;
    }

    /// <summary>
    /// Lists memories. If query is null, returns recent entries. If query is set, performs FTS5 search.
    /// </summary>
    public (List<MemoryEntry> Entries, int Total) List(string? query = null, int limit = 20, int offset = 0)
    {
        limit = Math.Clamp(limit, 1, 100);

        using var conn = Open();

        if (string.IsNullOrWhiteSpace(query))
        {
            // Recent entries
            using var countCmd = conn.CreateCommand();
            countCmd.CommandText = "SELECT COUNT(*) FROM Memories";
            var total = Convert.ToInt32(countCmd.ExecuteScalar());

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Key, Value, CreatedAt, UpdatedAt FROM Memories ORDER BY UpdatedAt DESC LIMIT @limit OFFSET @offset";
            cmd.Parameters.AddWithValue("@limit", limit);
            cmd.Parameters.AddWithValue("@offset", offset);

            return (ReadEntries(cmd), total);
        }
        else
        {
            // Try FTS5 search first, fall back to LIKE if FTS fails or query too short
            try
            {
                var ftsQuery = EscapeFtsQuery(query);
                if (string.IsNullOrEmpty(ftsQuery))
                    throw new InvalidOperationException("All tokens too short for trigram");


                using var countCmd = conn.CreateCommand();
                countCmd.CommandText = """
                    SELECT COUNT(*) FROM Memories m
                    JOIN MemoriesFts f ON m.rowid = f.rowid
                    WHERE MemoriesFts MATCH @query
                    """;
                countCmd.Parameters.AddWithValue("@query", ftsQuery);
                var total = Convert.ToInt32(countCmd.ExecuteScalar());

                using var cmd = conn.CreateCommand();
                cmd.CommandText = """
                    SELECT m.Key, m.Value, m.CreatedAt, m.UpdatedAt
                    FROM Memories m
                    JOIN MemoriesFts f ON m.rowid = f.rowid
                    WHERE MemoriesFts MATCH @query
                    ORDER BY rank
                    LIMIT @limit OFFSET @offset
                    """;
                cmd.Parameters.AddWithValue("@query", ftsQuery);
                cmd.Parameters.AddWithValue("@limit", limit);
                cmd.Parameters.AddWithValue("@offset", offset);

                return (ReadEntries(cmd), total);
            }
            catch
            {
                // FTS5 failed — fall back to LIKE
                var likePattern = $"%{query}%";

                using var countCmd = conn.CreateCommand();
                countCmd.CommandText = "SELECT COUNT(*) FROM Memories WHERE Key LIKE @q OR Value LIKE @q";
                countCmd.Parameters.AddWithValue("@q", likePattern);
                var total = Convert.ToInt32(countCmd.ExecuteScalar());

                using var cmd = conn.CreateCommand();
                cmd.CommandText = """
                    SELECT Key, Value, CreatedAt, UpdatedAt FROM Memories
                    WHERE Key LIKE @q OR Value LIKE @q
                    ORDER BY UpdatedAt DESC
                    LIMIT @limit OFFSET @offset
                    """;
                cmd.Parameters.AddWithValue("@q", likePattern);
                cmd.Parameters.AddWithValue("@limit", limit);
                cmd.Parameters.AddWithValue("@offset", offset);

                return (ReadEntries(cmd), total);
            }
        }
    }

    public void Dispose() { }

    #region Private

    /// <summary>
    /// Creates the database schema and FTS5 index if needed.
    /// </summary>
    private void Initialize()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            PRAGMA journal_mode=WAL;
            PRAGMA busy_timeout=5000;

            CREATE TABLE IF NOT EXISTS Memories (
                Key       TEXT PRIMARY KEY,
                Value     TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );

            """;
        cmd.ExecuteNonQuery();

        // Migration: ensure FTS5 uses trigram tokenizer
        // If MemoriesFts exists without trigram, drop and recreate
        var needsRecreate = false;
        try
        {
            using var checkFts = conn.CreateCommand();
            checkFts.CommandText = "SELECT sql FROM sqlite_master WHERE name = 'MemoriesFts'";
            var ftsSchema = checkFts.ExecuteScalar() as string;
            needsRecreate = ftsSchema is not null && !ftsSchema.Contains("trigram", StringComparison.OrdinalIgnoreCase);
        }
        catch { needsRecreate = true; }

        if (needsRecreate)
        {
            using var dropCmd = conn.CreateCommand();
            dropCmd.CommandText = "DROP TABLE IF EXISTS MemoriesFts";
            dropCmd.ExecuteNonQuery();
        }

        using var createFts = conn.CreateCommand();
        createFts.CommandText = """
            CREATE VIRTUAL TABLE IF NOT EXISTS MemoriesFts USING fts5(
                Key, Value, content=Memories, content_rowid=rowid,
                tokenize = 'trigram'
            );
            """;
        createFts.ExecuteNonQuery();

        // Rebuild FTS index if out of sync with Memories
        using var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = """
            SELECT (SELECT COUNT(*) FROM Memories) - (SELECT COUNT(*) FROM MemoriesFts)
            """;
        var diff = Convert.ToInt32(checkCmd.ExecuteScalar());
        if (diff != 0)
        {
            using var rebuildCmd = conn.CreateCommand();
            rebuildCmd.CommandText = "INSERT INTO MemoriesFts(MemoriesFts) VALUES('rebuild')";
            rebuildCmd.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Opens a new SQLite connection.
    /// </summary>
    private SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }

    /// <summary>
    /// Reads memory entries from a command's result set.
    /// </summary>
    private static List<MemoryEntry> ReadEntries(SqliteCommand cmd)
    {
        var entries = new List<MemoryEntry>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            entries.Add(new MemoryEntry
            {
                Key = reader.GetString(0),
                Value = reader.GetString(1),
                CreatedAt = reader.GetString(2),
                UpdatedAt = reader.GetString(3),
            });
        }
        return entries;
    }

    /// <summary>
    /// Builds an FTS5 MATCH query from user input, dropping tokens shorter than 3 characters.
    /// </summary>
    private static string EscapeFtsQuery(string query)
    {
        var tokens = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length >= 3)
            .Select(t => $"\"{t.Replace("\"", "\"\"")}\"")
            .ToList();
        return tokens.Count == 0 ? "" : string.Join(" OR ", tokens);
    }

    #endregion
}

/// <summary>
/// Represents a single memory entry.
/// </summary>
public sealed class MemoryEntry
{
    public required string Key { get; init; }
    public required string Value { get; init; }
    public required string CreatedAt { get; init; }
    public required string UpdatedAt { get; init; }
}
