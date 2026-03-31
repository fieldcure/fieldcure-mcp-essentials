# FieldCure.Mcp.Essentials

Install once, get the basics. An [MCP](https://modelcontextprotocol.io) server with 7 essential tools for any MCP client.

## Tools

| Tool | Description |
|------|-------------|
| `http_request` | Full HTTP client (GET/POST/PUT/DELETE/PATCH/HEAD) with SSRF protection |
| `run_command` | Shell command execution with timeout, working directory, and env vars |
| `run_javascript` | Sandboxed JavaScript (Jint) — math, JSON, regex, data processing |
| `get_environment` | System info — time, timezone, OS, hostname, username |
| `read_file` | Text file reading with offset/limit for large files |
| `write_file` | File writing (overwrite/append) with auto directory creation |
| `search_files` | File search by glob pattern and content (grep-like) |

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
