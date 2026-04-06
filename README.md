# FieldCure MCP Essentials

[![NuGet](https://img.shields.io/nuget/v/FieldCure.Mcp.Essentials)](https://www.nuget.org/packages/FieldCure.Mcp.Essentials)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/fieldcure/fieldcure-mcp-essentials/blob/main/LICENSE)

Install once, get the basics. A [Model Context Protocol (MCP)](https://modelcontextprotocol.io) server that provides 12 essential tools — HTTP requests, web search & fetch, shell commands, JavaScript execution, file I/O, environment info, and persistent memory — for any MCP client. Built with C# and the official [MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk).

## Features

- **12 essential tools** — HTTP, web search & fetch, shell, JavaScript sandbox, environment info, file read/write/search, persistent memory
- **Zero configuration** — no API keys needed for default Bing search; optional API keys unlock Serper, Tavily, and SerpApi
- **Web search & fetch** — Bing (default, free) with optional API-based engines (Serper, Tavily, SerpApi), plus readable text extraction from any URL
- **Sandboxed JavaScript** — Jint engine with strict limits (timeout, statement count, recursion depth)
- **SSRF protection** — HTTP requests and web fetch block private IP ranges and loopback addresses
- **Cross-client** — works with Claude Desktop, VS Code, AssistStudio, and any MCP-compatible client
- **Stdio transport** — standard MCP subprocess model via JSON-RPC over stdin/stdout

## Installation

### dotnet tool (recommended)

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

## Requirements

- [.NET 8.0 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) or later

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

### AssistStudio

Settings > MCP Servers > **Add Server**:

| Field | Value |
|-------|-------|
| **Name** | `Essentials` |
| **Command** | `fieldcure-mcp-essentials` |
| **Arguments** | *(empty)* |
| **Environment** | *(none — search engine API keys are optional)* |
| **Description** | *(auto-filled on first connection)* |

## Tools

| Tool | Description | Destructive |
|------|-------------|:-----------:|
| `http_request` | Full HTTP client (GET/POST/PUT/DELETE/PATCH/HEAD) with custom headers and body | — |
| `web_search` | Search the web and return snippets (title, URL, description) | — |
| `web_fetch` | Fetch a URL and extract content as Markdown with length limit | — |
| `run_command` | Execute shell commands with working directory and environment variables | Yes |
| `run_javascript` | Sandboxed JavaScript execution (Jint) for math, data processing, JSON, regex | — |
| `get_environment` | System info — local time, timezone, OS, hostname, username, .NET version | — |
| `read_file` | Read text files with offset and line limit for large files | — |
| `write_file` | Write or append text to files with auto directory creation | Yes |
| `search_files` | Search files by glob pattern and content (grep-like) | — |
| `remember` | Store a key-value memory (persisted in SQLite) | — |
| `forget` | Delete memories by key or keyword search | Yes |
| `list_memories` | Search and list stored memories with FTS5 and pagination | — |

### `web_search` vs `web_fetch` vs `http_request`

| | `http_request` | `web_search` | `web_fetch` |
|---|---|---|---|
| Purpose | API calls, raw HTTP | Web search | Read web pages |
| Response | Raw (JSON, HTML, etc.) | `{title, url, snippet}[]` | Markdown (body only) |
| Conversion | None | None | SmartReader HTML → Markdown |
| Length limit | None | `max_results` (max 10) | `max_length` (max 20000) |

### Filesystem Overlap

`read_file`, `write_file`, and `search_files` overlap with the [FieldCure MCP Filesystem](https://github.com/fieldcure/fieldcure-mcp-filesystem) server. When both are connected, the MCP client routes to Filesystem (which has sandboxing, atomic writes, and document parsing). With Essentials alone, the lightweight built-in versions handle basic file I/O.

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

## Web Search

Default engine is Bing (free, no API key needed). For more reliable results, use an API-based engine:

| Engine | Free Tier | API Key |
|--------|-----------|---------|
| Bing (default) | Unlimited (scraping) | Not needed |
| Serper | 2,500 one-time | [serper.dev](https://serper.dev) |
| Tavily | 1,000/month | [tavily.com](https://tavily.com) |
| SerpApi | 100/month | [serpapi.com](https://serpapi.com) |

```bash
# Use Serper
fieldcure-mcp-essentials --search-engine serper --search-api-key YOUR_KEY

# Use Tavily
fieldcure-mcp-essentials --search-engine tavily --search-api-key YOUR_KEY

# Or via environment variables
ESSENTIALS_SEARCH_ENGINE=serper ESSENTIALS_SEARCH_API_KEY=xxx fieldcure-mcp-essentials
```

### API Key Security

| Engine | Auth Method | Key Exposure |
|--------|-------------|--------------|
| Serper | HTTP header (`X-API-KEY`) | Not in URL |
| Tavily | Authorization header (`Bearer` token) | Not in URL |
| SerpApi | URL query parameter (`api_key=xxx`) | Visible in server logs |

API keys can also be stored in Windows PasswordVault per engine — never exposed via environment variables or CLI args:

| Engine | PasswordVault Resource |
|--------|----------------------|
| Serper | `FieldCure:Essentials:SerperApiKey` |
| Tavily | `FieldCure:Essentials:TavilyApiKey` |
| SerpApi | `FieldCure:Essentials:SerpApiApiKey` |

### Claude Desktop

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

Without `--search-engine`, a fallback engine (Bing → DuckDuckGo) auto-switches on CAPTCHA.

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
│   ├── SearchResult.cs         # Search result record
│   ├── BingSearchEngine.cs     # Bing scraping (default)
│   ├── DuckDuckGoSearchEngine.cs  # DuckDuckGo lite scraping
│   ├── FallbackSearchEngine.cs # Auto-rotate on CAPTCHA
│   ├── SerperSearchEngine.cs   # Serper.dev API
│   ├── TavilySearchEngine.cs   # Tavily API
│   └── SerpApiSearchEngine.cs  # SerpApi API
└── Tools/
    ├── HttpRequestTool.cs      # http_request
    ├── WebSearchTool.cs        # web_search
    ├── WebFetchTool.cs         # web_fetch (SmartReader)
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

## See Also — AssistStudio Ecosystem

### MCP Servers

| Package | Description |
|---------|-------------|
| **[FieldCure.Mcp.Essentials](https://www.nuget.org/packages/FieldCure.Mcp.Essentials)** | **HTTP, web search (Bing/Serper/Tavily), shell, JavaScript, file I/O, persistent memory** |
| [FieldCure.Mcp.Outbox](https://www.nuget.org/packages/FieldCure.Mcp.Outbox) | Multi-channel messaging — Slack, Telegram, Email (SMTP/Graph), KakaoTalk |
| [FieldCure.Mcp.Filesystem](https://www.nuget.org/packages/FieldCure.Mcp.Filesystem) | Sandboxed file/directory operations with built-in document parsing (DOCX, HWPX, XLSX, PDF) |
| [FieldCure.Mcp.Rag](https://www.nuget.org/packages/FieldCure.Mcp.Rag) | Document search — hybrid BM25 + vector retrieval, multi-KB, incremental indexing |
| [FieldCure.Mcp.PublicData.Kr](https://www.nuget.org/packages/FieldCure.Mcp.PublicData.Kr) | Korean public data gateway — data.go.kr (80,000+ APIs) |
| [FieldCure.AssistStudio.Runner](https://www.nuget.org/packages/FieldCure.AssistStudio.Runner) | Headless LLM task runner with scheduling via Windows Task Scheduler |

### Libraries

| Package | Description |
|---------|-------------|
| [FieldCure.Ai.Providers](https://www.nuget.org/packages/FieldCure.Ai.Providers) | Multi-provider AI client — Claude, OpenAI, Gemini, Ollama, Groq with streaming and tool use |
| [FieldCure.Ai.Execution](https://www.nuget.org/packages/FieldCure.Ai.Execution) | Agent loop and sub-agent execution engine for autonomous tool-use workflows |
| [FieldCure.AssistStudio.Core](https://www.nuget.org/packages/FieldCure.AssistStudio.Core) | MCP server management, tool orchestration, and conversation persistence |
| [FieldCure.AssistStudio.Controls.WinUI](https://www.nuget.org/packages/FieldCure.AssistStudio.Controls.WinUI) | WinUI 3 chat UI controls — WebView2 rendering, streaming, conversation branching |
| [FieldCure.DocumentParsers](https://www.nuget.org/packages/FieldCure.DocumentParsers) | Document text extraction — DOCX, HWPX, XLSX, PPTX with math-to-LaTeX |
| [FieldCure.DocumentParsers.Pdf](https://www.nuget.org/packages/FieldCure.DocumentParsers.Pdf) | PDF text extraction add-on for DocumentParsers |

### App

| Package | Description |
|---------|-------------|
| [FieldCure.AssistStudio](https://github.com/fieldcure/fieldcure-assiststudio) | Multi-provider AI workspace for Windows (WinUI 3) |

## License

[MIT](LICENSE)
