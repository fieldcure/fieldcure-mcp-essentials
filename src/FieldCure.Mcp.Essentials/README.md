# FieldCure.Mcp.Essentials

Install once, get the basics. An [MCP](https://modelcontextprotocol.io) server with 18 essential tools for any MCP client — web search with runtime engine switching, Wolfram|Alpha, web/document fetching, shell, JavaScript sandbox, file I/O, and persistent memory. Category search (news, images, scholar, patents) is always available with per-tool runtime capability guards against the active engine.

<!-- mcp-name: io.github.fieldcure/essentials -->

## Tools

| Tool | Description |
|------|-------------|
| `http_request` | Full HTTP client (GET/POST/PUT/DELETE/PATCH/HEAD) with SSRF protection and `max_response_chars` for response size control |
| `web_search` | Search the web and return snippets (title, URL, description) |
| `web_fetch` | Fetch a URL and extract content as Markdown — HTML pages and documents (PDF, DOCX, HWPX, PPTX, XLSX) |
| `run_command` | Shell command execution with timeout, working directory, and env vars |
| `run_javascript` | Sandboxed JavaScript (Jint) — math, JSON, regex, data processing |
| `wolfram_alpha` | Wolfram&#124;Alpha computational knowledge — symbolic math, plots, unit conversions, constants. MathML passes through for native rendering |
| `get_environment` | System info — time, timezone, OS, hostname, username |
| `read_file` | Read files — text with offset/limit, documents (PDF, DOCX, HWPX, PPTX, XLSX) parsed to Markdown |
| `write_file` | File writing (overwrite/append) with auto directory creation |
| `search_files` | File search by glob pattern and content (grep-like) |
| `remember` | Store a key-value memory (persisted in SQLite) |
| `forget` | Delete memories by key or keyword search |
| `list_memories` | Search and list stored memories with FTS5 and pagination |
| `set_search_engine` | Switch the active search engine (`bing`, `duckduckgo`, `serper`, `tavily`, `serpapi`) at runtime. Paid-engine API keys resolve lazily via env var or MCP Elicitation on the next search. Emits `notifications/tools/list_changed` on success. |

### Category Search (SerpApi / Serper / Tavily)

Always registered. Each tool runtime-guards on the active engine's capabilities and returns a descriptive error pointing at `set_search_engine` when the current engine does not support the category.

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
| PDF | `.pdf` | Content-Type / URL extension (text-layer only; scanned pages yield empty text — use `fieldcure-mcp-rag` for OCR) |
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

If a paid engine is selected explicitly but no API key is configured, the server no longer silently falls back. On the first `web_search` call it asks the MCP client for the key via MCP Elicitation; if the client does not support Elicitation, or the user declines and then also declines the follow-up "use free fallback?" prompt, the call soft-fails with a clear message. Cached keys live for the process lifetime.

## Wolfram|Alpha

`wolfram_alpha` calls the Full Results API v2 and returns mixed text/image content. MathML is passed through verbatim — clients that render MathML (e.g. ChatPanel WebView2) display formulas natively, no client-side conversion needed. Plots and other visual pods are embedded as `ImageContent`. The API's `reinterpret=true` flag is always on, so the server auto-corrects most failed queries before returning.

Set `WOLFRAM_APPID` to the AppID obtained at [developer.wolframalpha.com](https://developer.wolframalpha.com) (select **Full Results API**, free tier: 2,000 calls/month, non-commercial). Clients that support MCP Elicitation are prompted for the key on first use if the env var is unset.

> ⚠️ Use `developer.wolframalpha.com`, not `developer.wolfram.com` — the latter is a separate paid portal.

The tool is always registered regardless of AppID status; without a key it returns a setup-guidance error so the model can inform the user.

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
