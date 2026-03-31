# Release Notes

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
