using Microsoft.Data.Sqlite;

namespace FieldCure.Mcp.Essentials.Memory;

/// <summary>
/// Persistent key-value memory store backed by SQLite (WAL mode).
/// Default path: %LOCALAPPDATA%/FieldCure/Mcp.Essentials/memory.db.
/// Override via CLI argument (--memory-path) or ESSENTIALS_MEMORY_PATH env var.
/// </summary>
public sealed class MemoryStore : IDisposable
{
    private readonly string _connectionString;

    public const int MaxEntries = 200;
    private const int WarningThreshold = 180;

    public MemoryStore(string dbPath)
    {
        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        _connectionString = $"Data Source={dbPath}";
        Initialize();
    }

    /// <summary>
    /// Returns the default memory database path.
    /// </summary>
    public static string GetDefaultPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "FieldCure", "Mcp.Essentials", "memory.db");
    }

    /// <summary>
    /// Resolves the effective memory path: CLI arg > env var > default.
    /// </summary>
    public static string ResolvePath(string[] args)
    {
        // CLI: --memory-path <path>
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] is "--memory-path" or "-m")
                return Path.GetFullPath(args[i + 1]);
        }

        // Environment variable
        var envPath = Environment.GetEnvironmentVariable("ESSENTIALS_MEMORY_PATH");
        if (!string.IsNullOrWhiteSpace(envPath))
            return Path.GetFullPath(envPath);

        return GetDefaultPath();
    }

    public (bool Success, string? Warning) Add(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key))
            return (false, null);

        using var conn = Open();
        using var cmd = conn.CreateCommand();

        var now = DateTimeOffset.UtcNow.ToString("o");

        cmd.CommandText = """
            INSERT INTO Memories (Key, Value, CreatedAt, UpdatedAt)
            VALUES (@key, @value, @now, @now)
            ON CONFLICT(Key) DO UPDATE SET Value = @value, UpdatedAt = @now
            """;
        cmd.Parameters.AddWithValue("@key", key);
        cmd.Parameters.AddWithValue("@value", value);
        cmd.Parameters.AddWithValue("@now", now);
        cmd.ExecuteNonQuery();

        string? warning = null;
        var count = Count();
        if (count >= MaxEntries)
            warning = $"Memory has {count}/{MaxEntries} entries and has exceeded the soft limit. Consider removing old entries.";
        else if (count >= WarningThreshold)
            warning = $"Memory has {count}/{MaxEntries} entries. Consider removing old entries.";

        return (true, warning);
    }

    public bool Remove(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return false;

        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Memories WHERE Key LIKE @q OR Value LIKE @q";
        cmd.Parameters.AddWithValue("@q", $"%{query}%");
        return cmd.ExecuteNonQuery() > 0;
    }

    public List<MemoryEntry> GetAll()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Key, Value, CreatedAt, UpdatedAt FROM Memories ORDER BY CreatedAt";

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

    public int Count()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Memories";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public void Dispose() { }

    #region Private

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
    }

    private SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var pragma = conn.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();
        return conn;
    }

    #endregion
}

/// <summary>
/// A single memory entry.
/// </summary>
public sealed class MemoryEntry
{
    public required string Key { get; init; }
    public required string Value { get; init; }
    public required string CreatedAt { get; init; }
    public required string UpdatedAt { get; init; }
}
