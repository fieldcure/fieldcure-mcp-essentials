using FieldCure.Mcp.Essentials.Configuration;

namespace FieldCure.Mcp.Essentials.Tests;

[TestClass]
public class EssentialsSettingsTests
{
    string _tempDir = null!;
    string? _oldSettingsPath;
    string? _oldDownloadDirectory;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"essentials_settings_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _oldSettingsPath = Environment.GetEnvironmentVariable("ESSENTIALS_SETTINGS_PATH");
        _oldDownloadDirectory = Environment.GetEnvironmentVariable("ESSENTIALS_DOWNLOAD_DIRECTORY");
        Environment.SetEnvironmentVariable("ESSENTIALS_SETTINGS_PATH", null);
        Environment.SetEnvironmentVariable("ESSENTIALS_DOWNLOAD_DIRECTORY", null);
    }

    [TestCleanup]
    public void Cleanup()
    {
        Environment.SetEnvironmentVariable("ESSENTIALS_SETTINGS_PATH", _oldSettingsPath);
        Environment.SetEnvironmentVariable("ESSENTIALS_DOWNLOAD_DIRECTORY", _oldDownloadDirectory);

        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [TestMethod]
    public void LoadUsesDefaultDownloadDirectoryWhenUnset()
    {
        var settings = EssentialsSettings.Load(["--settings-path", Path.Combine(_tempDir, "missing.json")]);

        Assert.AreEqual(EssentialsSettings.DefaultDownloadDirectory, settings.DownloadDirectory);
        StringAssert.EndsWith(settings.GetResolvedDownloadDirectory(), Path.Combine("Downloads", "mcp"));
    }

    [TestMethod]
    public void LoadReadsDownloadDirectoryFromSettingsFile()
    {
        var settingsPath = Path.Combine(_tempDir, "settings.json");
        var configured = Path.Combine(_tempDir, "from-file");
        var configuredJson = EscapeJsonString(configured);
        File.WriteAllText(settingsPath, $$"""
            {
              "download_directory": "{{configuredJson}}",
            }
            """);

        var settings = EssentialsSettings.Load(["--settings-path", settingsPath]);

        Assert.AreEqual(configured, settings.DownloadDirectory);
        Assert.AreEqual(configured, settings.GetResolvedDownloadDirectory());
    }

    [TestMethod]
    public void EnvironmentDownloadDirectoryOverridesSettingsFile()
    {
        var settingsPath = Path.Combine(_tempDir, "settings.json");
        var fromFileJson = EscapeJsonString(Path.Combine(_tempDir, "from-file"));
        File.WriteAllText(settingsPath, $$"""
            {
              "download_directory": "{{fromFileJson}}"
            }
            """);

        var fromEnv = Path.Combine(_tempDir, "from-env");
        Environment.SetEnvironmentVariable("ESSENTIALS_DOWNLOAD_DIRECTORY", fromEnv);

        var settings = EssentialsSettings.Load(["--settings-path", settingsPath]);

        Assert.AreEqual(fromEnv, settings.DownloadDirectory);
    }

    [TestMethod]
    public void CliDownloadDirectoryOverridesEnvironment()
    {
        Environment.SetEnvironmentVariable("ESSENTIALS_DOWNLOAD_DIRECTORY", Path.Combine(_tempDir, "from-env"));
        var fromCli = Path.Combine(_tempDir, "from-cli");

        var settings = EssentialsSettings.Load([
            "--settings-path",
            Path.Combine(_tempDir, "missing.json"),
            "--download-directory",
            fromCli,
        ]);

        Assert.AreEqual(fromCli, settings.DownloadDirectory);
    }

    [TestMethod]
    public void ExpandsHomeDirectoryPrefix()
    {
        var expanded = EssentialsSettings.ExpandHomeDirectory("~/Downloads/mcp");

        Assert.IsTrue(Path.IsPathFullyQualified(expanded));
        StringAssert.EndsWith(expanded, Path.Combine("Downloads", "mcp"));
    }

    /// <summary>
    /// Escapes a string for direct insertion into the small JSON fixtures in this test class.
    /// </summary>
    static string EscapeJsonString(string value)
        => value.Replace(@"\", @"\\").Replace("\"", "\\\"");
}
