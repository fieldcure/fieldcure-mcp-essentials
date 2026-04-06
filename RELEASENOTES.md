# Release Notes

## v0.7.1 (2026-04-06)

### Fixed

- **Graceful fallback on missing API key** — paid engines (Serper, Tavily, SerpApi) now fall back to Bing/DuckDuckGo instead of crashing when the API key is not found
- Warning logged to stderr: `[Warning] ... — falling back to Bing/DuckDuckGo.`
- Unknown engine names also fall back gracefully instead of throwing

---

## v0.7.0 (2026-04-06)

### Changed

- **Per-engine PasswordVault API keys** — each search engine now has its own PasswordVault resource instead of a single shared key
  - Serper: `FieldCure:Essentials:SerperApiKey`
  - Tavily: `FieldCure:Essentials:TavilyApiKey`
  - SerpApi: `FieldCure:Essentials:SerpApiApiKey`
- CLI `--search-api-key` and `ESSENTIALS_SEARCH_API_KEY` still take priority (backwards compatible)
- Improved error messages now show the engine-specific vault resource name

---

## v0.6.0 (2026-04-04)

### New Features

- **Serper search engine** — Google results via Serper.dev API (`--search-engine serper`)
- **Tavily search engine** — AI-optimized search with content snippets (`--search-engine tavily`)
- **SerpApi search engine** — Google/Scholar/Patents via SerpApi (`--search-engine serpapi`)
- **Unified API key** — `--search-api-key`, `ESSENTIALS_SEARCH_API_KEY`, or Windows PasswordVault
- **PasswordVault integration** — API keys read from Windows Credential Manager (P/Invoke), never exposed via environment variables

### Notes

- API-based engines require an API key. Without a key, Bing (free) is used by default.
- Tavily returns content snippets with results, reducing the need for `web_fetch`.
- SerpApi passes the API key in the URL query string — consider this for sensitive environments.

---

## v0.5.2 (2026-04-04)

### New Features

- **FallbackSearchEngine** — auto-rotates between Bing and DuckDuckGo when one returns empty results (CAPTCHA detection)
  - Tries fallback engine immediately in the same call
  - Subsequent calls continue with the working engine
  - `params ISearchEngine[]` supports 2+ engines
- **Randomized request throttling** — delays between search requests use a random range (Bing 2-4s, DDG 3-5s) to mimic human behavior

### Changed

- Default engine is now `FallbackSearchEngine(Bing, DuckDuckGo)` instead of single Bing
- `--search-engine bing` or `--search-engine duckduckgo` still selects a single engine

---

## v0.5.1 (2026-04-04)

### Changed

- Default search engine changed to Bing (DuckDuckGo blocks bot traffic)
- DuckDuckGo still available via `--search-engine duckduckgo`

---

## v0.5.0 (2026-04-04)

### New Features

- **`web_search` region parameter** — localized search results via `region` code (e.g. `ko-kr`, `en-us`)
  - DuckDuckGo: `kl` parameter mapping
  - Bing: `setLang` + `cc` parameter mapping
  - Unrecognized region falls back to global search (no error)
- **`web_fetch` Markdown output** — page content now returned as Markdown (headings, links, tables, code blocks preserved) via ReverseMarkdown

### Changed

- `web_fetch` description updated to reflect Markdown output
- `ISearchEngine.SearchAsync` signature now includes optional `region` parameter

---

## v0.4.1 (2026-04-03)

### Changed

- `ModelContextProtocol` 1.1.0 → 1.2.0

---

## v0.4.0 (2026-03-31)

### Changes

- **FTS5 trigram tokenizer** — memory search now uses trigram tokenizer for substring matching
- **LIKE fallback** — queries shorter than 3 characters fall back to LIKE search

---

## v0.3.0 (2026-03-31)

### New Tools

- **`remember`** — Store a key-value memory (persisted in SQLite)
- **`forget`** — Delete memories by key or keyword search
- **`list_memories`** — Search and list stored memories with FTS5 and pagination

### Changes

- Tool count: 7 → 10
- Redesigned memory API with FTS5 full-text search and structured responses
- Removed entry limit — unlimited storage, prompt injection capped at 50

---

## v0.2.0 (2026-03-30)

### Fixes

- Default working directory changed to user home instead of System32
- Return validation error when `run_javascript` code parameter is missing

---

## v0.1.0 (2026-03-30)

Initial release.

### Features

- **7 MCP tools** for essential operations via stdio transport
  - **HTTP**: `http_request` (GET/POST/PUT/DELETE/PATCH/HEAD) with SSRF protection
  - **Shell**: `run_command` with timeout, working directory, and env vars
  - **JavaScript**: `run_javascript` — sandboxed Jint engine with strict limits
  - **Environment**: `get_environment` — local time, timezone, OS, hostname, username
  - **File I/O**: `read_file`, `write_file`, `search_files`
- **SSRF protection** — private IP range and loopback blocking for HTTP requests
- **JavaScript sandbox** — 5s timeout, 100K statement limit, 64-depth recursion
- **dotnet tool** packaging for global installation via NuGet

### Tech Stack

- .NET 8.0
- [MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk) v1.2.0
- Jint (JavaScript interpreter)
- Microsoft.Extensions.Hosting
