# Release Notes

## v0.3.0

- **Persistent memory** — 3 new tools (`remember`, `forget`, `list_memories`) backed by SQLite with FTS5 full-text search
- **Cross-client memory sharing** — memories stored in `%LOCALAPPDATA%/FieldCure/Mcp.Essentials/memory.db`, shared across all MCP clients (AssistStudio, Claude Desktop, VS Code, etc.)
- **Memory path override** — `--memory-path` CLI arg or `ESSENTIALS_MEMORY_PATH` env var
- **FTS5 search** — `list_memories(query)` performs keyword search; without query returns recent entries
- **Pagination** — `list_memories(limit, offset)` with `has_more` indicator
- **Bulk forget** — `forget(query)` deletes all matching memories via FTS5

## v0.2.0

- **Working directory fix** — default CWD to user home instead of System32 when launched by host apps; supports `ESSENTIALS_CWD` env var override
- **run_javascript validation** — return clear error message when `code` parameter is missing instead of generic MCP SDK error

## v0.1.0

Initial release — 7 essential tools:

- `http_request` — Full HTTP client (GET/POST/PUT/DELETE/PATCH/HEAD)
- `run_command` — Shell command execution
- `run_javascript` — Sandboxed JavaScript execution (Jint)
- `get_environment` — System environment info
- `read_file` — Text file reading with offset/limit
- `write_file` — File writing with overwrite/append
- `search_files` — File search by glob pattern and content
