# Release Notes

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
