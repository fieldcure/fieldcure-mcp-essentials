using System.Text.Json;
using System.Text.Json.Serialization;

namespace FieldCure.Mcp.Essentials.Configuration;

/// <summary>
/// User-configurable settings for the Essentials MCP server.
/// </summary>
public sealed class EssentialsSettings
{
    /// <summary>Default download directory used by <c>download_file</c>.</summary>
    public const string DefaultDownloadDirectory = "~/Downloads/mcp";

    /// <summary>
    /// JSON options for the optional settings file. Settings files are meant to
    /// be hand-edited, so comments and trailing commas are accepted.
    /// </summary>
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// Directory used as the base for relative <c>download_file.save_path</c> values.
    /// </summary>
    [JsonPropertyName("download_directory")]
    public string DownloadDirectory { get; init; } = DefaultDownloadDirectory;

    /// <summary>
    /// Loads settings from the default settings file, environment variables, and CLI args.
    /// Precedence: CLI &gt; environment &gt; settings file &gt; defaults.
    /// </summary>
    /// <param name="args">Command-line arguments passed to the server process.</param>
    /// <returns>The resolved settings object.</returns>
    public static EssentialsSettings Load(string[] args)
    {
        var fileSettings = LoadFromSettingsFile(args) ?? new EssentialsSettings();
        var downloadDirectory = NormalizeConfiguredPath(fileSettings.DownloadDirectory);

        var envDownloadDirectory = Environment.GetEnvironmentVariable("ESSENTIALS_DOWNLOAD_DIRECTORY");
        if (!string.IsNullOrWhiteSpace(envDownloadDirectory))
            downloadDirectory = envDownloadDirectory;

        var cliDownloadDirectory = ResolveArg(args, "--download-directory", "-d");
        if (!string.IsNullOrWhiteSpace(cliDownloadDirectory))
            downloadDirectory = cliDownloadDirectory;

        return new EssentialsSettings
        {
            DownloadDirectory = NormalizeConfiguredPath(downloadDirectory),
        };
    }

    /// <summary>
    /// Returns the configured download directory as an absolute path.
    /// </summary>
    /// <returns>The absolute download directory path with home/environment variables expanded.</returns>
    public string GetResolvedDownloadDirectory()
        => Path.GetFullPath(ExpandHomeDirectory(NormalizeConfiguredPath(DownloadDirectory)));

    /// <summary>
    /// Returns the default settings path for the current platform.
    /// </summary>
    /// <returns>The default settings file path.</returns>
    public static string GetDefaultSettingsPath()
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(baseDir))
            baseDir = Path.Combine(GetHomeDirectory(), ".local", "share");

        return Path.Combine(baseDir, "FieldCure", "Mcp.Essentials", "settings.json");
    }

    /// <summary>
    /// Expands a leading <c>~</c> to the current user's home directory.
    /// </summary>
    /// <param name="path">Path that may start with <c>~</c> or include environment variables.</param>
    /// <returns>The expanded path.</returns>
    internal static string ExpandHomeDirectory(string path)
    {
        path = Environment.ExpandEnvironmentVariables(path);

        if (path == "~")
            return GetHomeDirectory();

        if (path.StartsWith("~/", StringComparison.Ordinal) ||
            path.StartsWith(@"~\", StringComparison.Ordinal))
        {
            return Path.Combine(GetHomeDirectory(), NormalizeSeparators(path[2..]));
        }

        return path;
    }

    /// <summary>
    /// Loads settings from the configured settings file path, if the file exists.
    /// </summary>
    /// <param name="args">Command-line arguments that may include <c>--settings-path</c>.</param>
    /// <returns>The deserialized settings, or <see langword="null"/> when no file exists.</returns>
    static EssentialsSettings? LoadFromSettingsFile(string[] args)
    {
        var settingsPath = ResolveArg(args, "--settings-path")
            ?? Environment.GetEnvironmentVariable("ESSENTIALS_SETTINGS_PATH")
            ?? GetDefaultSettingsPath();

        settingsPath = Path.GetFullPath(ExpandHomeDirectory(settingsPath));

        if (!File.Exists(settingsPath))
            return null;

        try
        {
            var json = File.ReadAllText(settingsPath);
            return JsonSerializer.Deserialize<EssentialsSettings>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            // A malformed or unreadable settings file must not prevent server startup.
            Console.Error.WriteLine($"[essentials] Failed to load settings from {settingsPath}: {ex.Message}. Using defaults.");
            return null;
        }
    }

    /// <summary>
    /// Resolves a command-line option value by looking for a long and optional short flag.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <param name="cliFlag">Long option name.</param>
    /// <param name="shortFlag">Optional short option name.</param>
    /// <returns>The option value, or <see langword="null"/> when the option is not present.</returns>
    static string? ResolveArg(string[] args, string cliFlag, string? shortFlag = null)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == cliFlag || (shortFlag is not null && args[i] == shortFlag))
                return args[i + 1];
        }

        return null;
    }

    /// <summary>
    /// Normalizes a configured path value, falling back to the sensible default when blank.
    /// </summary>
    /// <param name="path">Configured path value.</param>
    /// <returns>A non-empty path string.</returns>
    static string NormalizeConfiguredPath(string? path)
        => string.IsNullOrWhiteSpace(path) ? DefaultDownloadDirectory : path.Trim();

    /// <summary>
    /// Normalizes slash variants in a relative path fragment for the current platform.
    /// </summary>
    /// <param name="path">Relative path fragment.</param>
    /// <returns>The path fragment using the platform directory separator.</returns>
    static string NormalizeSeparators(string path)
        => path.Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);

    /// <summary>
    /// Finds the current user's home directory using platform APIs and common environment variables.
    /// </summary>
    /// <returns>The best available home directory path.</returns>
    static string GetHomeDirectory()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(home))
            return home;

        return Environment.GetEnvironmentVariable("HOME")
            ?? Environment.GetEnvironmentVariable("USERPROFILE")
            ?? Environment.CurrentDirectory;
    }
}
