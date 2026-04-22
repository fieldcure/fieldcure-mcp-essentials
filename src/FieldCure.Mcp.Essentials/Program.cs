using FieldCure.Mcp.Essentials.Memory;
using FieldCure.Mcp.Essentials.Search;
using FieldCure.Mcp.Essentials.Services;
using FieldCure.Mcp.Essentials.Services.WolframAlpha;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Reflection;

// Default CWD to user home when launched by a host app (e.g., AssistStudio)
// to avoid running in System32 or other system directories.
var cwd = Environment.GetEnvironmentVariable("ESSENTIALS_CWD")
    ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
if (Directory.Exists(cwd))
    Environment.CurrentDirectory = cwd;

// Resolve memory file path: CLI arg (--memory-path) > env var > default
var memoryPath = MemoryStore.ResolvePath(args);

var apiKeyResolvers = new ApiKeyResolverRegistry();

// Resolve the initial search engine: CLI arg (--search-engine) > env var > default (bing).
// Runtime switching via set_search_engine is handled by SearchEngineManager.
var initialEngine = ResolveSearchEngine(args, apiKeyResolvers);

var builder = Host.CreateApplicationBuilder(Array.Empty<string>());

builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services
    .AddSingleton(new MemoryStore(memoryPath));

builder.Services.AddSingleton(apiKeyResolvers);

// SearchEngineManager owns the active engine; all search tools read
// manager.Current on each invocation so switches take effect without
// restarting the stdio server.
builder.Services.AddSingleton(sp => new SearchEngineManager(
    initialEngine,
    sp.GetRequiredService<ApiKeyResolverRegistry>(),
    sp.GetRequiredService<ILogger<SearchEngineManager>>()));

// Transient ISearchEngine proxy for web_search — always resolves to the
// currently active engine.
builder.Services.AddTransient<ISearchEngine>(sp =>
    sp.GetRequiredService<SearchEngineManager>().Current);

// Wolfram|Alpha — always registered; AppID is resolved lazily via
// ApiKeyResolverRegistry on first tool invocation.
var wolframHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
builder.Services.AddSingleton(new WolframAlphaClient(wolframHttp));
builder.Services.AddSingleton(new ResultConverter(wolframHttp));

builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new()
        {
            Name = "fieldcure-mcp-essentials",
            Title = "FieldCure Essentials",
            Description = "HTTP, web search (+ news/images/scholar/patents with a category-capable engine), Wolfram|Alpha, shell, JavaScript, file I/O, persistent memory. Use set_search_engine to switch search engines at runtime.",
            Version = GetPublicVersion(),
        };
    })
    .WithStdioServerTransport()
    .WithToolsFromAssembly();  // Discovers all [McpServerToolType] classes,
                               // including the superset of category tools.

var app = builder.Build();
await app.RunAsync();
return 0;

/// <summary>
/// Resolves the initial search engine from CLI args, environment variable,
/// or default. Runtime switching is handled separately by
/// <see cref="SearchEngineManager"/>.
/// </summary>
static ISearchEngine ResolveSearchEngine(string[] args, ApiKeyResolverRegistry apiKeyResolvers)
{
    var engineName = ResolveArg(args, "--search-engine", "ESSENTIALS_SEARCH_ENGINE");

    if (engineName is null)
    {
        // Auto-detect: scan environment for paid engine API keys
        var detected = DetectEngineFromEnv();
        if (detected is not null)
            return detected;

        // Default: fallback engine (Bing primary, DDG secondary).
        // Automatically switches when one engine returns empty results (CAPTCHA).
        return new FallbackSearchEngine(new BingSearchEngine(), new DuckDuckGoSearchEngine());
    }

    // CLI arg > env var > engine-specific env var
    var apiKey = ResolveArg(args, "--search-api-key", "ESSENTIALS_SEARCH_API_KEY")
                 ?? ReadEngineApiKey(engineName);

    try
    {
        return SearchEngineFactory.Create(engineName, apiKey, apiKeyResolvers);
    }
    catch (ArgumentException ex)
    {
        Console.Error.WriteLine($"[Warning] {ex.Message} — falling back to Bing/DuckDuckGo.");
        return new FallbackSearchEngine(new BingSearchEngine(), new DuckDuckGoSearchEngine());
    }
}

/// <summary>
/// Resolves a value from CLI args or environment variable.
/// </summary>
static string? ResolveArg(string[] args, string cliFlag, string envVar)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i] == cliFlag)
            return args[i + 1];
    }

    var env = Environment.GetEnvironmentVariable(envVar);
    return string.IsNullOrWhiteSpace(env) ? null : env;
}

/// <summary>
/// Scans environment variables for paid search engine API keys and returns the first match.
/// Priority: Serper → SerpApi → Tavily.
/// </summary>
static ISearchEngine? DetectEngineFromEnv()
{
    (string Name, string EnvVar)[] engines =
    [
        ("serper", "SERPER_API_KEY"),
        ("serpapi", "SERPAPI_API_KEY"),
        ("tavily", "TAVILY_API_KEY"),
    ];

    foreach (var (name, envVar) in engines)
    {
        var apiKey = Environment.GetEnvironmentVariable(envVar);
        if (!string.IsNullOrEmpty(apiKey))
        {
            Console.Error.WriteLine($"[Info] Auto-detected search engine '{name}' from {envVar}.");
            return SearchEngineFactory.CreateConcrete(name, apiKey);
        }
    }

    return null;
}

/// <summary>
/// Reads an API key from engine-specific environment variables.
/// </summary>
static string? ReadEngineApiKey(string engineName)
{
    var envVar = engineName.ToLowerInvariant() switch
    {
        "serper" => "SERPER_API_KEY",
        "tavily" => "TAVILY_API_KEY",
        "serpapi" => "SERPAPI_API_KEY",
        _ => null,
    };

    if (envVar is null) return null;
    var value = Environment.GetEnvironmentVariable(envVar);
    return string.IsNullOrEmpty(value) ? null : value;
}

/// <summary>
/// Returns the user-facing server version. Strips the SemVer 2.0 build-metadata
/// suffix (<c>+&lt;commit-sha&gt;</c>) that the .NET SDK auto-appends to
/// <see cref="AssemblyInformationalVersionAttribute"/>; that hash is only useful
/// to developers and just adds noise in client UIs. The assembly attribute
/// itself still carries the full string for diagnostic logs and debuggers.
/// </summary>
static string GetPublicVersion()
{
    var info = typeof(Program).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
        ?.InformationalVersion;
    if (string.IsNullOrEmpty(info)) return "0.0.0";
    var plus = info.IndexOf('+');
    return plus > 0 ? info[..plus] : info;
}
