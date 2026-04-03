# FieldCure MCP Essentials

[![NuGet](https://img.shields.io/nuget/v/FieldCure.Mcp.Essentials)](https://www.nuget.org/packages/FieldCure.Mcp.Essentials)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/fieldcure/fieldcure-mcp-essentials/blob/main/LICENSE)

Install once, get the basics. A [Model Context Protocol (MCP)](https://modelcontextprotocol.io) server that provides 13 essential tools — HTTP requests, web search & fetch, shell commands, JavaScript execution, file I/O, environment info, and persistent memory — for any MCP client. Built with C# and the official [MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk).

## Features

- **13 essential tools** — HTTP, web search & fetch, shell, JavaScript sandbox, environment info, file read/write/search, persistent memory
- **Zero configuration** — no API keys, no accounts, no setup
- **Web search & fetch** — DuckDuckGo (default) or Bing search, plus readable text extraction from any URL
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

## Tools

| Tool | Description | Destructive |
|------|-------------|:-----------:|
| `http_request` | Full HTTP client (GET/POST/PUT/DELETE/PATCH/HEAD) with custom headers and body | — |
| `web_search` | Search the web and return snippets (title, URL, description) | — |
| `web_fetch` | Fetch a URL and extract readable text with length limit | — |
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
| Response | Raw (JSON, HTML, etc.) | `{title, url, snippet}[]` | Readable text (body only) |
| Conversion | None | None | SmartReader HTML → text |
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

`web_search` uses DuckDuckGo by default. You can switch to Bing:

```bash
# CLI argument
fieldcure-mcp-essentials --search-engine bing

# Or environment variable
ESSENTIALS_SEARCH_ENGINE=bing fieldcure-mcp-essentials
```

Supported engines: `duckduckgo` (default), `bing`

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
│   ├── DuckDuckGoSearchEngine.cs  # DuckDuckGo lite scraping
│   └── BingSearchEngine.cs     # Bing scraping
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

## License

[MIT](LICENSE)
