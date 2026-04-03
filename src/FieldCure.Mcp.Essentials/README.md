# FieldCure.Mcp.Essentials

Install once, get the basics. An [MCP](https://modelcontextprotocol.io) server with 13 essential tools for any MCP client.

## Tools

| Tool | Description |
|------|-------------|
| `http_request` | Full HTTP client (GET/POST/PUT/DELETE/PATCH/HEAD) with SSRF protection |
| `web_search` | Search the web and return snippets (title, URL, description) |
| `web_fetch` | Fetch a URL and extract readable text with length limit |
| `run_command` | Shell command execution with timeout, working directory, and env vars |
| `run_javascript` | Sandboxed JavaScript (Jint) — math, JSON, regex, data processing |
| `get_environment` | System info — time, timezone, OS, hostname, username |
| `read_file` | Text file reading with offset/limit for large files |
| `write_file` | File writing (overwrite/append) with auto directory creation |
| `search_files` | File search by glob pattern and content (grep-like) |
| `remember` | Store a key-value memory (persisted in SQLite) |
| `forget` | Delete memories by key or keyword search |
| `list_memories` | Search and list stored memories with FTS5 and pagination |

## Web Search

`web_search` uses DuckDuckGo by default. Switch to Bing with `--search-engine bing` or `ESSENTIALS_SEARCH_ENGINE=bing`.

## Memory

Memories are stored in SQLite (`%LOCALAPPDATA%/FieldCure/Mcp.Essentials/memory.db`) and shared across all MCP clients on the same machine.

```
# Custom memory path
fieldcure-mcp-essentials --memory-path /path/to/memory.db

# Or via environment variable
ESSENTIALS_MEMORY_PATH=/path/to/memory.db fieldcure-mcp-essentials
```

## Quick Start

```bash
dotnet tool install -g FieldCure.Mcp.Essentials
```

### Claude Desktop

```json
{
  "mcpServers": {
    "essentials": {
      "command": "fieldcure-mcp-essentials"
    }
  }
}
```

### VS Code

```json
{
  "servers": {
    "essentials": {
      "command": "fieldcure-mcp-essentials"
    }
  }
}
```

## Requirements

- [.NET 8.0 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- No API keys or accounts needed

## Links

- [GitHub](https://github.com/fieldcure/fieldcure-mcp-essentials)
- [License: MIT](https://github.com/fieldcure/fieldcure-mcp-essentials/blob/main/LICENSE)
