# FieldCure MCP Essentials

[![NuGet](https://img.shields.io/nuget/v/FieldCure.Mcp.Essentials)](https://www.nuget.org/packages/FieldCure.Mcp.Essentials)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/fieldcure/fieldcure-mcp-essentials/blob/main/LICENSE)

Install once, get the basics. A [Model Context Protocol (MCP)](https://modelcontextprotocol.io) server that provides 12–16 essential tools — HTTP requests, web search & fetch, shell commands, JavaScript execution, file I/O, environment info, and persistent memory — for any MCP client. With SerpApi or Serper, category search tools (news, images, scholar, patents) are auto-registered. Built with C# and the official [MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk).

## Features

- **12–16 essential tools** — HTTP, web search & fetch, shell, JavaScript sandbox, environment info, file read/write/search, persistent memory + dynamic category search (news, images, scholar, patents) with SerpApi or Serper
- **Zero configuration** — no API keys needed for default Bing search; optional API keys unlock Serper, Tavily, and SerpApi (+ category search tools)
- **Document parsing** — `web_fetch` and `read_file` extract text from PDF, DOCX, HWPX, PPTX, XLSX into Markdown
- **Sandboxed JavaScript** — Jint engine with strict limits (timeout, statement count, recursion depth)
- **SSRF protection** — HTTP requests and web fetch block private IP ranges and loopback addresses
- **Cross-client** — works with Claude Desktop, VS Code, AssistStudio, and any MCP-compatible client
- **Stdio transport** — standard MCP subprocess model via JSON-RPC over stdin/stdout

## Installation

```bash
dotnet tool install -g FieldCure.Mcp.Essentials
```

After installation, the `fieldcure-mcp-essentials` command is available globally.

### From source

```bash
git clone https://github.com/fieldcure/fieldcure-mcp-essentials.git
cd fieldcure-mcp-essentials
dotnet build
```

### Requirements

- [.NET 8.0 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) or later

## Tools

| Tool | Description | Destructive |
|------|-------------|:-----------:|
| `http_request` | Full HTTP client (GET/POST/PUT/DELETE/PATCH/HEAD) with custom headers and body | — |
| `web_search` | Search the web and return snippets (title, URL, description) | — |
| `web_fetch` | Fetch a URL and extract content as Markdown — HTML pages and documents (PDF, DOCX, HWPX, PPTX, XLSX) | — |
| `run_command` | Execute shell commands with working directory and environment variables | Yes |
| `run_javascript` | Sandboxed JavaScript execution (Jint) for math, data processing, JSON, regex | — |
| `get_environment` | System info — local time, timezone, OS, hostname, username, .NET version | — |
| `read_file` | Read files — text with offset/limit, documents (PDF, DOCX, HWPX, PPTX, XLSX) parsed to Markdown | — |
| `write_file` | Write or append text to files with auto directory creation | Yes |
| `search_files` | Search files by glob pattern and content (grep-like) | — |
| `remember` | Store a key-value memory (persisted in SQLite) | — |
| `forget` | Delete memories by key or keyword search | Yes |
| `list_memories` | Search and list stored memories with FTS5 and pagination | — |

### Category Search (dynamic — SerpApi / Serper)

These tools are auto-registered at startup when a category-capable engine is active:

| Tool | Description | SerpApi | Serper | Tavily |
|------|-------------|:-------:|:------:|:------:|
| `search_news` | Search recent news articles via Google News | Yes | Yes | Yes |
| `search_images` | Search images with size/type filtering | Yes | Yes | — |
| `search_scholar` | Search academic papers with citation counts | Yes | Yes | — |
| `search_patents` | Search patent documents with inventor/assignee filtering | Yes | Yes | — |

### `web_search` vs `web_fetch` vs `http_request`

| | `http_request` | `web_search` | `web_fetch` |
|---|---|---|---|
| Purpose | API calls, raw HTTP | Web search | Read web pages |
| Response | Raw (JSON, HTML, etc.) | `{title, url, snippet}[]` | Markdown (body only) |
| Conversion | None | None | SmartReader HTML → Markdown |
| Length limit | None | `max_results` (max 10) | `max_length` (max 20000) |

## Document Parsing

`web_fetch` and `read_file` can parse binary documents into Markdown:

| Format | Extension | Detection |
|--------|-----------|-----------|
| PDF | `.pdf` | Content-Type / URL extension |
| Word | `.docx` | Content-Type / URL extension |
| Hangul (HWPX) | `.hwpx` | URL extension (no standard Content-Type) |
| PowerPoint | `.pptx` | Content-Type / URL extension |
| Excel | `.xlsx` | Content-Type / URL extension |

Output includes headings, tables, math expressions (`[math: LaTeX]`), and slide/page separators.

## Web Search

Default engine is Bing (free, no API key needed). For more reliable results, use an API-based engine:

| Engine | Free Tier | Category Search | API Key |
|--------|-----------|:---------------:|---------|
| Bing (default) | Unlimited (scraping) | — | Not needed |
| Serper | 2,500 one-time | news, images, scholar, patents | [serper.dev](https://serper.dev) |
| SerpApi | 100/month | news, images, scholar, patents | [serpapi.com](https://serpapi.com) |
| Tavily | 1,000/month | news | [tavily.com](https://tavily.com) |

```bash
# Use Serper
fieldcure-mcp-essentials --search-engine serper --search-api-key YOUR_KEY

# Use Tavily
fieldcure-mcp-essentials --search-engine tavily --search-api-key YOUR_KEY

# Or via environment variables
ESSENTIALS_SEARCH_ENGINE=serper ESSENTIALS_SEARCH_API_KEY=xxx fieldcure-mcp-essentials
```

### PasswordVault Auto-Detection

API keys can be stored in Windows PasswordVault per engine. When `--search-engine` is omitted, the server scans PasswordVault and automatically selects the best available engine — no CLI args or environment variables needed:

| Engine | PasswordVault Resource |
|--------|----------------------|
| Serper | `FieldCure:Essentials:SerperApiKey` |
| SerpApi | `FieldCure:Essentials:SerpApiApiKey` |
| Tavily | `FieldCure:Essentials:TavilyApiKey` |

Detection priority: Serper → SerpApi → Tavily → Bing/DuckDuckGo fallback.

### API Key Security

| Engine | Auth Method | Key Exposure |
|--------|-------------|--------------|
| Serper | HTTP header (`X-API-KEY`) | Not in URL |
| Tavily | Authorization header (`Bearer` token) | Not in URL |
| SerpApi | URL query parameter (`api_key=xxx`) | Visible in server logs |

### Region

Use the `region` parameter for localized results:

```json
// Korean results
{ "query": "서울 맛집", "region": "ko-kr" }

// US English results
{ "query": "best restaurants NYC", "region": "en-us" }

// Global (default)
{ "query": "Python tutorial" }
```

Without `--search-engine`, a fallback engine (Bing → DuckDuckGo) auto-switches on CAPTCHA. Free engines rely on scraping and may be intermittent — **an API-based engine is strongly recommended for any non-trivial use.**
If a paid engine is selected but the API key is missing, the server falls back to Bing/DuckDuckGo with a warning on stderr.

## JavaScript Sandbox

`run_javascript` uses the [Jint](https://github.com/sebastienros/jint) engine with strict limits:

| Constraint | Value |
|-----------|-------|
| Timeout | 5s default, 30s max |
| Max statements | 100,000 |
| Recursion depth | 64 |
| Strict mode | Enforced |

**Allowed:** `Math.*`, `JSON`, `Date`, `RegExp`, `console.log`, string/array methods, `parseInt`, `encodeURIComponent`, `atob`/`btoa`

**Blocked:** `setTimeout`, `setInterval`, `require`, `import`, `.NET interop`, `eval()`

Variables can be injected into the script scope for data pipeline use:

```
1. http_request(url: "https://api.example.com/data") → {"items": [...]}
2. run_javascript(
     code: "data.items.filter(x => x.price > 100).map(x => x.name)",
     variables: {"data": {"items": [...]}}
   )
```

## Memory

Memories are stored in SQLite (`%LOCALAPPDATA%/FieldCure/Mcp.Essentials/memory.db`) and shared across all MCP clients on the same machine.

```bash
# Custom memory path
fieldcure-mcp-essentials --memory-path /path/to/memory.db

# Or via environment variable
ESSENTIALS_MEMORY_PATH=/path/to/memory.db fieldcure-mcp-essentials
```

## Configuration

### Claude Desktop

Add to `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "essentials": {
      "command": "fieldcure-mcp-essentials"
    }
  }
}
```

With a search engine:

```json
{
  "mcpServers": {
    "essentials": {
      "command": "fieldcure-mcp-essentials",
      "args": ["--search-engine", "serper", "--search-api-key", "YOUR_KEY"]
    }
  }
}
```

### VS Code (Copilot)

Add to `.vscode/mcp.json`:

```json
{
  "servers": {
    "essentials": {
      "command": "fieldcure-mcp-essentials"
    }
  }
}
```

### From source (without dotnet tool)

```json
{
  "mcpServers": {
    "essentials": {
      "command": "dotnet",
      "args": [
        "run",
        "--project", "C:\\path\\to\\fieldcure-mcp-essentials\\src\\FieldCure.Mcp.Essentials"
      ]
    }
  }
}
```

## Data Storage

| Data | Location |
|------|----------|
| Memory database | `%LOCALAPPDATA%/FieldCure/Mcp.Essentials/memory.db` |
| Search API keys | Windows PasswordVault (DPAPI) |

## Project Structure

```
src/FieldCure.Mcp.Essentials/
├── Program.cs                  # MCP server entry point (stdio)
├── Http/
│   └── SsrfGuard.cs            # SSRF protection (shared by http_request & web_fetch)
├── Memory/
│   └── MemoryStore.cs          # SQLite + FTS5 memory storage
├── Search/
│   ├── ISearchEngine.cs        # Search engine interface
│   ├── ICategorySearchEngine.cs # Category search interface (news, images, scholar, patents)
│   ├── SearchResult.cs         # Search result record
│   ├── BingSearchEngine.cs     # Bing scraping (default)
│   ├── DuckDuckGoSearchEngine.cs  # DuckDuckGo lite scraping
│   ├── FallbackSearchEngine.cs # Auto-rotate on CAPTCHA
│   ├── SerperSearchEngine.cs   # Serper.dev API (+ category search)
│   ├── TavilySearchEngine.cs   # Tavily API (+ news)
│   └── SerpApiSearchEngine.cs  # SerpApi API (+ category search)
└── Tools/
    ├── HttpRequestTool.cs      # http_request
    ├── WebSearchTool.cs        # web_search
    ├── WebFetchTool.cs         # web_fetch (SmartReader)
    ├── CategorySearchTools.cs  # search_news / search_images / search_scholar / search_patents
    ├── CategorySearchDescriptions.cs  # Per-engine tool descriptions
    ├── RunCommandTool.cs       # run_command
    ├── RunJavaScriptTool.cs    # run_javascript (Jint sandbox)
    ├── GetEnvironmentTool.cs   # get_environment
    ├── ReadFileTool.cs         # read_file
    ├── WriteFileTool.cs        # write_file
    ├── SearchFilesTool.cs      # search_files
    └── MemoryTools.cs          # remember / forget / list_memories
```

## Development

```bash
# Build
dotnet build

# Test
dotnet test

# Pack as dotnet tool
dotnet pack src/FieldCure.Mcp.Essentials -c Release
```

## See Also

Part of the [AssistStudio ecosystem](https://github.com/fieldcure/fieldcure-assiststudio#packages).

## License

[MIT](LICENSE)
