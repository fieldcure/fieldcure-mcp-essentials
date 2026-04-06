# FieldCure.Mcp.Essentials

Install once, get the basics. An [MCP](https://modelcontextprotocol.io) server with 12 essential tools for any MCP client — web search, web/document fetching, shell, JavaScript sandbox, file I/O, and persistent memory.

## Tools

| Tool | Description |
|------|-------------|
| `http_request` | Full HTTP client (GET/POST/PUT/DELETE/PATCH/HEAD) with SSRF protection |
| `web_search` | Search the web and return snippets (title, URL, description) |
| `web_fetch` | Fetch a URL and extract content as Markdown — HTML pages and documents (PDF, DOCX, HWPX, PPTX, XLSX) |
| `run_command` | Shell command execution with timeout, working directory, and env vars |
| `run_javascript` | Sandboxed JavaScript (Jint) — math, JSON, regex, data processing |
| `get_environment` | System info — time, timezone, OS, hostname, username |
| `read_file` | Read files — text with offset/limit, documents (PDF, DOCX, HWPX, PPTX, XLSX) parsed to Markdown |
| `write_file` | File writing (overwrite/append) with auto directory creation |
| `search_files` | File search by glob pattern and content (grep-like) |
| `remember` | Store a key-value memory (persisted in SQLite) |
| `forget` | Delete memories by key or keyword search |
| `list_memories` | Search and list stored memories with FTS5 and pagination |

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

| Engine | Free Tier | API Key |
|--------|-----------|---------|
| Bing (default) | Unlimited (scraping) | Not needed |
| Serper | 2,500 one-time | [serper.dev](https://serper.dev) |
| Tavily | 1,000/month | [tavily.com](https://tavily.com) |
| SerpApi | 100/month | [serpapi.com](https://serpapi.com) |

```bash
# Use Serper
fieldcure-mcp-essentials --search-engine serper --search-api-key YOUR_KEY

# Or via environment variables
ESSENTIALS_SEARCH_ENGINE=tavily ESSENTIALS_SEARCH_API_KEY=xxx fieldcure-mcp-essentials
```

API keys can also be stored in Windows PasswordVault per engine:

| Engine | PasswordVault Resource |
|--------|----------------------|
| Serper | `FieldCure:Essentials:SerperApiKey` |
| Tavily | `FieldCure:Essentials:TavilyApiKey` |
| SerpApi | `FieldCure:Essentials:SerpApiApiKey` |

Use the `region` parameter for localized results:

```json
{ "query": "서울 맛집", "region": "ko-kr" }
```

Without `--search-engine`, a fallback engine (Bing → DuckDuckGo) auto-switches on CAPTCHA.
If a paid engine is selected but the API key is missing, the server falls back to Bing/DuckDuckGo with a warning on stderr.

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

### AssistStudio

Settings > MCP Servers > **Add Server**:

| Field | Value |
|-------|-------|
| **Name** | `Essentials` |
| **Command** | `fieldcure-mcp-essentials` |
| **Arguments** | *(empty)* |
| **Environment** | *(none — search engine API keys are optional)* |
| **Description** | *(auto-filled on first connection)* |

## Requirements

- [.NET 8.0 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)

## Optional

Works out of the box with no additional dependencies.
If available on your system, `run_command` can leverage tools like
Node.js, Python, Git, Docker, and any other CLI tools.

## See Also — AssistStudio Ecosystem

| Package | Description |
|---------|-------------|
| **[FieldCure.Mcp.Essentials](https://www.nuget.org/packages/FieldCure.Mcp.Essentials)** | **Web search, web/document fetching (PDF, DOCX, HWPX, PPTX, XLSX), shell, JavaScript, file I/O, persistent memory** |
| [FieldCure.Mcp.Outbox](https://www.nuget.org/packages/FieldCure.Mcp.Outbox) | Multi-channel messaging — Slack, Telegram, Email (SMTP/Graph), KakaoTalk |
| [FieldCure.Mcp.Filesystem](https://www.nuget.org/packages/FieldCure.Mcp.Filesystem) | Sandboxed file/directory operations with built-in document parsing (DOCX, HWPX, XLSX, PDF) |
| [FieldCure.Mcp.Rag](https://www.nuget.org/packages/FieldCure.Mcp.Rag) | Document search — hybrid BM25 + vector retrieval, multi-KB, incremental indexing |
| [FieldCure.Mcp.PublicData.Kr](https://www.nuget.org/packages/FieldCure.Mcp.PublicData.Kr) | Korean public data gateway — data.go.kr (80,000+ APIs) |
| [FieldCure.AssistStudio.Runner](https://www.nuget.org/packages/FieldCure.AssistStudio.Runner) | Headless LLM task runner with scheduling via Windows Task Scheduler |
| [FieldCure.AssistStudio](https://github.com/fieldcure/fieldcure-assiststudio) | Multi-provider AI workspace for Windows (WinUI 3) |

## Links

- [GitHub](https://github.com/fieldcure/fieldcure-mcp-essentials)
- [License: MIT](https://github.com/fieldcure/fieldcure-mcp-essentials/blob/main/LICENSE)
