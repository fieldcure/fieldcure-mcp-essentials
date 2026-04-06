using System.Reflection;
using FieldCure.Mcp.Essentials;
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
            Title = "FieldCure Essentials",
            Description = "HTTP, web search, shell, JavaScript, file I/O, persistent memory",
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
    var engineName = ResolveArg(args, "--search-engine", "ESSENTIALS_SEARCH_ENGINE");

    if (engineName is null)
    {
        // Default: fallback engine (Bing primary, DDG secondary).
        // Automatically switches when one engine returns empty results (CAPTCHA).
        return new FallbackSearchEngine(new BingSearchEngine(), new DuckDuckGoSearchEngine());
    }

    // CLI arg > env var > engine-specific PasswordVault key
    var apiKey = ResolveArg(args, "--search-api-key", "ESSENTIALS_SEARCH_API_KEY")
                 ?? ReadEngineApiKey(engineName);

    return CreateEngine(engineName, apiKey);
}

/// <summary>
/// Resolves a value from CLI args or environment variable.
/// </summary>
static string? ResolveArg(string[] args, string cliFlag, string envVar)
{
    for (int i = 0; i < args.Length - 1; i++)
    {
        if (args[i] == cliFlag)
            return args[i + 1];
    }

    var env = Environment.GetEnvironmentVariable(envVar);
    return string.IsNullOrWhiteSpace(env) ? null : env;
}

/// <summary>
/// Reads an API key from PasswordVault using an engine-specific resource name.
/// </summary>
static string? ReadEngineApiKey(string engineName) => engineName.ToLowerInvariant() switch
{
    "serper" => ReadFromPasswordVault("FieldCure:Essentials:SerperApiKey"),
    "tavily" => ReadFromPasswordVault("FieldCure:Essentials:TavilyApiKey"),
    "serpapi" => ReadFromPasswordVault("FieldCure:Essentials:SerpApiApiKey"),
    _ => null,
};

/// <summary>
/// Reads a credential from Windows Credential Manager (PasswordVault-compatible).
/// </summary>
static string? ReadFromPasswordVault(string resourceName, string userName = "default") =>
    PasswordVault.Read(resourceName, userName);

/// <summary>
/// Creates a search engine instance by name, with optional API key for paid engines.
/// </summary>
static ISearchEngine CreateEngine(string name, string? apiKey) => name.ToLowerInvariant() switch
{
    "bing" => new BingSearchEngine(),
    "duckduckgo" or "ddg" => new DuckDuckGoSearchEngine(),
    "serper" => new SerperSearchEngine(apiKey ?? throw new ArgumentException(
        "Serper requires --search-api-key, ESSENTIALS_SEARCH_API_KEY, or PasswordVault 'FieldCure:Essentials:SerperApiKey'")),
    "tavily" => new TavilySearchEngine(apiKey ?? throw new ArgumentException(
        "Tavily requires --search-api-key, ESSENTIALS_SEARCH_API_KEY, or PasswordVault 'FieldCure:Essentials:TavilyApiKey'")),
    "serpapi" => new SerpApiSearchEngine(apiKey ?? throw new ArgumentException(
        "SerpApi requires --search-api-key, ESSENTIALS_SEARCH_API_KEY, or PasswordVault 'FieldCure:Essentials:SerpApiApiKey'")),
    _ => throw new ArgumentException($"Unknown search engine: '{name}'. Supported: bing, duckduckgo, serper, tavily, serpapi"),
};
