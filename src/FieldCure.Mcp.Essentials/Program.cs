using System.Reflection;
using FieldCure.Mcp.Essentials.Memory;
using FieldCure.Mcp.Essentials.Search;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Default CWD to user home when launched by a host app (e.g., AssistStudio)
// to avoid running in System32 or other system directories.
var cwd = Environment.GetEnvironmentVariable("ESSENTIALS_CWD")
    ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
if (Directory.Exists(cwd))
    Environment.CurrentDirectory = cwd;

// Resolve memory file path: CLI arg (--memory-path) > env var > default
var memoryPath = MemoryStore.ResolvePath(args);

// Resolve search engine: CLI arg (--search-engine) > env var > default (bing)
var searchEngine = ResolveSearchEngine(args);

var builder = Host.CreateApplicationBuilder(Array.Empty<string>());

builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services
    .AddSingleton(new MemoryStore(memoryPath))
    .AddSingleton<ISearchEngine>(searchEngine)
    .AddMcpServer(options =>
    {
        options.ServerInfo = new()
        {
            Name = "fieldcure-mcp-essentials",
            Version = typeof(Program).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion ?? "0.0.0",
        };
    })
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

var app = builder.Build();
await app.RunAsync();
return 0;

/// <summary>
/// Resolves the search engine from CLI args, environment variable, or default.
/// </summary>
static ISearchEngine ResolveSearchEngine(string[] args)
{
    // 1. CLI arg: --search-engine <name>
    for (int i = 0; i < args.Length - 1; i++)
    {
        if (args[i] is "--search-engine")
            return CreateEngine(args[i + 1]);
    }

    // 2. Environment variable
    var env = Environment.GetEnvironmentVariable("ESSENTIALS_SEARCH_ENGINE");
    if (!string.IsNullOrWhiteSpace(env))
        return CreateEngine(env);

    // 3. Default: fallback engine (Bing primary, DDG secondary).
    // Automatically switches when one engine returns empty results (CAPTCHA).
    return new FallbackSearchEngine(new BingSearchEngine(), new DuckDuckGoSearchEngine());
}

/// <summary>
/// Creates a search engine instance by name.
/// </summary>
static ISearchEngine CreateEngine(string name) => name.ToLowerInvariant() switch
{
    "bing" => new BingSearchEngine(),
    "duckduckgo" or "ddg" => new DuckDuckGoSearchEngine(),
    _ => throw new ArgumentException($"Unknown search engine: '{name}'. Supported: duckduckgo, bing"),
};
