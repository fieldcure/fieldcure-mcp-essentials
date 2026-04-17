# FieldCure.Mcp.Essentials

Install once, get the basics. An [MCP](https://modelcontextprotocol.io) server with 12–16 essential tools for any MCP client — web search, web/document fetching, shell, JavaScript sandbox, file I/O, and persistent memory. With SerpApi or Serper, category search tools (news, images, scholar, patents) are auto-registered.

## Tools

| Tool | Description |
|------|-------------|
| `http_request` | Full HTTP client (GET/POST/PUT/DELETE/PATCH/HEAD) with SSRF protection and `max_response_chars` for response size control |
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

### Category Search (dynamic — SerpApi / Serper)

| Tool | Description | SerpApi | Serper | Tavily |
|------|-------------|:-------:|:------:|:------:|
| `search_news` | Search recent news articles via Google News | Yes | Yes | Yes |
| `search_images` | Search images with size/type filtering | Yes | Yes | — |
| `search_scholar` | Search academic papers with citation counts | Yes | Yes | — |
| `search_patents` | Search patent documents with inventor/assignee filtering | Yes | Yes | — |

## Document Parsing

`web_fetch` and `read_file` can parse binary documents into Markdown:

| Format | Extension | Detection |
|--------|-----------|-----------|
| PDF | `.pdf` | Content-Type / URL extension (OCR fallback for scanned pages) |
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

Engine-specific environment variables are also auto-detected when `--search-engine` is omitted:

| Engine | Environment Variable |
|--------|---------------------|
| Serper | `SERPER_API_KEY` |
| SerpApi | `SERPAPI_API_KEY` |
| Tavily | `TAVILY_API_KEY` |

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

## Requirements

- [.NET 8.0 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)

## Optional

Works out of the box with no additional dependencies.
If available on your system, `run_command` can leverage tools like
Node.js, Python, Git, Docker, and any other CLI tools.

## See Also

Part of the [AssistStudio ecosystem](https://github.com/fieldcure/fieldcure-assiststudio#packages).

## Links

- [GitHub](https://github.com/fieldcure/fieldcure-mcp-essentials)
- [License: MIT](https://github.com/fieldcure/fieldcure-mcp-essentials/blob/main/LICENSE)
