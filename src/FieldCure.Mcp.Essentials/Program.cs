using System.Reflection;
using FieldCure.Mcp.Essentials;
using FieldCure.Mcp.Essentials.Memory;
using FieldCure.Mcp.Essentials.Search;
using FieldCure.Mcp.Essentials.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

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
    .AddSingleton(new MemoryStore(memoryPath));

builder.Services.AddSingleton<ISearchEngine>(searchEngine);

var mcpBuilder = builder.Services
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
    .WithToolsFromAssembly();  // 12 static tools (attribute-based)

// v1.1: Register category search tools dynamically based on engine capabilities.
// Tools and descriptions are determined at startup — no runtime changes needed
// since stdio MCP servers restart on engine changes.
if (searchEngine is ICategorySearchEngine categoryEngine)
{
    builder.Services.AddSingleton<ICategorySearchEngine>(categoryEngine);
    RegisterCategoryTools(mcpBuilder, categoryEngine);
}

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
/// Falls back to Bing/DuckDuckGo if a paid engine's API key is missing.
/// </summary>
static ISearchEngine CreateEngine(string name, string? apiKey)
{
    var fallback = new FallbackSearchEngine(new BingSearchEngine(), new DuckDuckGoSearchEngine());

    return name.ToLowerInvariant() switch
    {
        "bing" => new BingSearchEngine(),
        "duckduckgo" or "ddg" => new DuckDuckGoSearchEngine(),
        "serper" => apiKey is not null
            ? new SerperSearchEngine(apiKey)
            : LogAndFallback("Serper API key not found (--search-api-key, ESSENTIALS_SEARCH_API_KEY, or PasswordVault 'FieldCure:Essentials:SerperApiKey')", fallback),
        "tavily" => apiKey is not null
            ? new TavilySearchEngine(apiKey)
            : LogAndFallback("Tavily API key not found (--search-api-key, ESSENTIALS_SEARCH_API_KEY, or PasswordVault 'FieldCure:Essentials:TavilyApiKey')", fallback),
        "serpapi" => apiKey is not null
            ? new SerpApiSearchEngine(apiKey)
            : LogAndFallback("SerpApi API key not found (--search-api-key, ESSENTIALS_SEARCH_API_KEY, or PasswordVault 'FieldCure:Essentials:SerpApiApiKey')", fallback),
        _ => LogAndFallback($"Unknown search engine: '{name}'. Supported: bing, duckduckgo, serper, tavily, serpapi", fallback),
    };
}

/// <summary>
/// Logs a warning to stderr and returns the fallback search engine.
/// </summary>
static ISearchEngine LogAndFallback(string message, ISearchEngine fallback)
{
    Console.Error.WriteLine($"[Warning] {message} — falling back to Bing/DuckDuckGo.");
    return fallback;
}

/// <summary>
/// Registers category search tools (news, images, scholar, patents) based on engine capabilities.
/// Each tool gets an engine-specific description via McpServerToolCreateOptions.
/// </summary>
static void RegisterCategoryTools(IMcpServerBuilder mcpBuilder, ICategorySearchEngine engine)
{
    var cats = engine.SupportedCategories;
    var name = engine.EngineName;
    var tools = new List<McpServerTool>();

    if (cats.Contains(SearchCategory.News))
        tools.Add(McpServerTool.Create(
            CategorySearchTools.SearchNews,
            new McpServerToolCreateOptions
            {
                Name = "search_news",
                Description = CategorySearchDescriptions.News(name),
            }));

    if (cats.Contains(SearchCategory.Images))
        tools.Add(McpServerTool.Create(
            CategorySearchTools.SearchImages,
            new McpServerToolCreateOptions
            {
                Name = "search_images",
                Description = CategorySearchDescriptions.Images(name),
            }));

    if (cats.Contains(SearchCategory.Scholar))
        tools.Add(McpServerTool.Create(
            CategorySearchTools.SearchScholar,
            new McpServerToolCreateOptions
            {
                Name = "search_scholar",
                Description = CategorySearchDescriptions.Scholar(name),
            }));

    if (cats.Contains(SearchCategory.Patents))
        tools.Add(McpServerTool.Create(
            CategorySearchTools.SearchPatents,
            new McpServerToolCreateOptions
            {
                Name = "search_patents",
                Description = CategorySearchDescriptions.Patents(name),
            }));

    if (tools.Count > 0)
        mcpBuilder.WithTools(tools);
}
