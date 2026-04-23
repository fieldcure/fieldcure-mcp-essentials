# Release Notes

## v2.4.0 (2026-04-23)

### Added

- **`get_search_engine` tool** — returns the currently active engine name, its supported category set, and a `supports_category_search` boolean. Read-only and side-effect-free. Fills the gap left by v2.3.0's mutation-only `set_search_engine`: host UIs that want to reflect the live engine after a runtime switch had no way to query it short of mutating, and models that wanted to confirm category support before calling `search_news` / `search_images` / `search_scholar` / `search_patents` had to rely on the per-tool runtime guard as a failure-only signal.

### Behaviour notes

- No state change from v2.3.0. Engine resolution order (CLI arg → env var → auto-detect → fallback) is unchanged, and `set_search_engine` remains the only mutator.

---

## v2.3.0 (2026-04-22)

### Added

- **`set_search_engine` tool** — switches the active search engine (`bing`, `duckduckgo`, `serper`, `tavily`, `serpapi`) at runtime without restarting the stdio server. Paid-engine API keys continue to resolve lazily via `ApiKeyResolverRegistry` (env var → MCP Elicitation) on the next search invocation; the switch itself takes only the engine name.
- **`SearchEngineManager`** — singleton that owns the active `ISearchEngine` and coordinates switches. Emits `notifications/tools/list_changed` after a successful swap so supporting clients refresh their tool descriptions; emission failures are logged and swallowed so clients that ignore the notification are unaffected.
- **`LazyPaidSearchEngine.InvalidateCache()`** — discards the cached engine resolution under the existing semaphore so the next access re-evaluates API-key availability (used by `SearchEngineManager` on the outgoing engine to keep any retained reference clean).
- **`SearchEngineFactory`** — extracts engine construction from `Program.cs` so both the startup resolution path and the runtime switch go through the same factory, with a shared `SupportedNames` list for tool descriptions.

### Changed

- **Category tools are now always registered** — `search_news`, `search_images`, `search_scholar`, and `search_patents` are always exposed (superset) instead of conditionally registered per initial engine. Each handler runtime-guards on `ICategorySearchEngine.SupportedCategories` and returns a descriptive error that points at `set_search_engine` when the active engine cannot service the request.
- **`CategorySearchTools`** migrated to attribute-based discovery (`[McpServerToolType]`) and consumes `SearchEngineManager` via DI so invocations always see the current engine.
- **Server `Description`** now mentions runtime engine switching.

### Removed

- **`CategorySearchDescriptions`** — per-engine tool descriptions became meaningless once engines can change at runtime; the replacement descriptions live inline on each `[McpServerTool]` with a hint about `set_search_engine`.

### Behaviour notes

- **Superset-only strategy (not hybrid)** — the design originally proposed a hybrid strategy (exact tools for `list_changed`-capable clients, superset otherwise), but the SDK surface for dynamically adding/removing `McpServerTool` instances is internal, and the per-tool runtime guard is required for the non-`list_changed` path anyway. Four always-on tools add negligible token overhead and keep the code path uniform.
- **Client notification is best-effort** — `list_changed` is sent after every successful switch but not retried; clients that do not advertise the capability silently ignore it.

---

## v2.2.0 (2026-04-22)

### Added

- **`wolfram_alpha` tool** — queries the Wolfram|Alpha Full Results API v2 and returns mixed MCP content: MathML is passed through verbatim (ChatPanel/WebView2 renders it natively, no client-side conversion), plot/visual pods are embedded as `ImageContent`, and disambiguation assumptions plus a source link are appended.
- **`reinterpret=true` by default** — the API auto-corrects most failed queries server-side; only real parse failures surface `success=false` with the tool returning `isError: true` and `assumptions > tips > didyoumeans` guidance.
- **AppID via `ApiKeyResolverRegistry`** — the tool is always registered; the AppID resolves lazily from `WOLFRAM_APPID` on first call, with MCP Elicitation fallback on capable clients and Credential-Manager-style re-elicit cap reuse.
- **401/403 → invalidate-and-retry once** — a rejected AppID is invalidated and re-resolved (which may re-elicit), bounded by the existing registry cap so the retry cannot loop.
- **Unit tests** — `WolframAlphaResultConverterTests` covers MathML pass-through, visual pod image fetching (JSON `img.contenttype` MIME), non-visual pods without plaintext, image-fetch failure fallback, disambiguation, error priority ordering, and a real-response fixture.

### Changed

- Server `Description` now lists `Wolfram|Alpha` alongside existing capabilities.

### Behaviour notes

- **Tool is always exposed** (ADR-001) even without `WOLFRAM_APPID`, so the model can surface setup guidance (portal link, `Full Results API` selection) instead of the tool being invisible.
- **No automatic fallback** — when a query is better served by `run_javascript` or `web_search`, the model is expected to pick the right tool via the `RECOMMENDED / AVOID` guidance in the tool description. No code-level delegation.

---

## v2.1.1 (2026-04-20)

- Update MCP package metadata to the latest `server.json` format for NuGet and VS Code integration.

## v2.1.0 (2026-04-20)

### Added

- **MCP Elicitation for explicit paid engines** — when the user selects Serper / Tavily / SerpApi explicitly via `--search-engine` or `ESSENTIALS_SEARCH_ENGINE` but no API key is configured, the server elicits the key from the MCP client on the first `web_search` call instead of silently falling back to Bing/DuckDuckGo. A second elicitation asks whether to run the search with the free engine anyway; declining both yields a structured soft-fail. Auto-detect mode (no explicit engine) keeps its pre-existing free-fallback behaviour.
- **`IElicitGate` abstraction** (`Services/IElicitGate.cs`, `McpServerElicitGate.cs`, `ApiKeyResolverRegistry.cs`) — wraps the subset of `McpServer` that credential resolution needs so resolvers can be unit tested without constructing a real server.
- **`ApiKeyResolverRegistry.Invalidate(envVarName)`** — discards a cached key after an upstream 401/403 and marks the environment-variable source as exhausted, forcing the next resolve to re-elicit instead of returning the same rejected key.
- **`LazyPaidSearchEngine`** — explicit paid engines without a key are wrapped in a proxy that defers the concrete engine creation to the first tool call; category tool registration still happens at startup via a static `SupportedCategories` mapping.
- **Unit tests** — `LazyPaidSearchEngineTests` covers the six new decision paths (supported / unsupported elicit, accept / decline, fallback consent, re-elicit after invalidate, category fallback shape).

### Changed

- **`FieldCure.DocumentParsers` 1.x → 2.x** — PDF text extraction is now part of the core DocumentParsers package; the dedicated `FieldCure.DocumentParsers.Pdf` reference is removed. `DocumentHelper` drops the lazy `TesseractOcrEngine` initialization entirely. Scanned PDFs without a text layer yield empty text. For OCR-backed indexing use [`fieldcure-mcp-rag`](https://github.com/fieldcure/fieldcure-mcp-rag), which bundles that path conditionally on Windows.
- **`WebSearchTool.WebSearch` and `CategorySearchTools` entry points accept a nullable `McpServer`** — existing unit tests and direct invocations keep working; when the server is available the tool wraps it in `McpServerElicitGate` and dispatches through `IMcpAwareSearchEngine` / `IMcpAwareCategorySearchEngine`.

### Behaviour notes

- **Non-breaking by default.** Clients without Elicitation support see the pre-2.1 free-fallback behaviour, just as before. The new prompts only appear on Elicitation-capable clients when the explicit-engine path is taken without a key.
- **Session cache, max 2 re-elicits.** Once an API key is obtained it is reused for the rest of the server process. Up to two re-elicitations per env-var slot are allowed after invalidation, matching the same cap used by `fieldcure-mcp-rag`.

---

## v2.0.0 (2026-04-17)

### Breaking

- **Remove Windows PasswordVault dependency** — search API keys are now resolved exclusively from environment variables (`SERPER_API_KEY`, `TAVILY_API_KEY`, `SERPAPI_API_KEY`) or CLI args (`--search-api-key`). The `PasswordVault.cs` file and advapi32.dll P/Invoke have been removed.

### Changed

- **Auto-detect from environment** — engine auto-detection scans environment variables instead of PasswordVault (priority: Serper → SerpApi → Tavily)
- **Cross-platform** — `net8.0` TFM is no longer misleading; runs on Windows, Linux, and macOS

### Removed

- `PasswordVault.cs` (Windows Credential Manager P/Invoke)

### Migration

If you previously stored API keys in Windows Credential Manager, set the corresponding environment variable instead:

```bash
# PowerShell
$env:SERPER_API_KEY = "your-key"

# Or in Claude Desktop config
"env": { "SERPER_API_KEY": "your-key" }
```

---

## v1.5.0 (2026-04-14)

### Changed

- **Centralize `JsonSerializerOptions`** — extract shared `McpJson.Options` to eliminate per-tool serializer configuration duplication
- **Jint 4.7.1 → 4.8.0** — JavaScript sandbox engine update

---

## v1.4.2 (2026-04-10)

- **`http_request` — refine `max_response_chars` description** — recommend for HTML/text responses, warn against JSON API truncation (truncated JSON cannot be parsed)

---

## v1.4.1 (2026-04-10)

- **`http_request` — stronger `max_response_chars` description** — reworded tool description to explicitly recommend 2000–5000 for most API calls, nudging models to use the parameter by default

---

## v1.4.0 (2026-04-10)

- **`http_request` — `max_response_chars` parameter** — limit response body size at the character level, independent of the 1MB byte ceiling. Truncated responses include an inline marker so the model knows content was omitted and can adjust its strategy.

---

## v1.3.1 (2026-04-08)

- **Fix Tesseract native DLL missing in dotnet tool** — upgraded `DocumentParsers.Pdf.Ocr` to 1.0.1 which includes `leptonica-1.82.0.dll` and `tesseract50.dll` via `buildTransitive/.targets`, fixing `DllNotFoundException` on server startup

---

## v1.3.0 (2026-04-08)

- **OCR fallback for scanned PDFs** — `read_file` and `web_fetch` now extract text from scanned PDFs via Tesseract OCR (English + Korean, tessdata_fast); lazily initialized on first PDF parse
- `FieldCure.DocumentParsers.Pdf.Ocr` 1.x dependency

---

## v1.2.0 (2026-04-07)

- **Auto-detect search engine from PasswordVault** — when `--search-engine` is not specified, scans PasswordVault for paid engine API keys (Serper → Tavily → SerpApi) and auto-selects the first match. Enables headless callers (Runner, CLI) to use paid search without explicit arguments
- **Upgrade ReverseMarkdown 4.* → 5.*** — resolves version conflict with FieldCure.DocumentParsers 1.1.0

## v1.1.0 (2026-04-07)

### New Features

- **Dynamic category search tools** — `search_news`, `search_images`, `search_scholar`, `search_patents` auto-registered when SerpApi or Serper is active
  - Tavily: `search_news` only (API limitation)
  - Tools and descriptions are determined at startup based on engine capabilities
  - `engine` parameter not exposed — injected via DI, transparent to the user
- **Dynamic server description** — `serverInfo.description` reflects active category tools (e.g. `+ news, images, scholar, patents`)

### Changed

- Server description now dynamically includes available category names
- `.mcp/server.json` and NuGet description updated to mention category search
- Tool count: 12 static + up to 4 dynamic (16 max with SerpApi/Serper)

---

## v1.0.0 (2026-04-06)

### New Features

- **Document parsing** — `web_fetch` and `read_file` now parse binary documents into Markdown
  - Supported formats: PDF, DOCX, HWPX, PPTX, XLSX
  - `web_fetch`: routes by Content-Type with URL extension fallback (for HWPX)
  - `read_file`: routes by file extension, adds `max_length` parameter for document output
  - Shared logic in `DocumentHelper` — no code duplication between tools
- Output preserves headings, tables, math expressions (`[math: LaTeX]`), and page/slide separators

### Changed

- `web_fetch` description updated to reflect document support
- `read_file` description updated to reflect document support
- README: added Document Parsing section, Requirements, and Optional sections
- Package tags updated with `document-parsing`, `pdf`, `docx`

### Dependencies

- Added `FieldCure.DocumentParsers` 1.x
- Added `FieldCure.DocumentParsers.Pdf` 1.x

---

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
