# FieldCure.Mcp.Essentials

Cross-platform .NET 8 MCP server. Tools live in `src/FieldCure.Mcp.Essentials/Tools/*.cs`, packaged as NuGet `FieldCure.Mcp.Essentials` and distributed via `dnx`.

## Tool authoring conventions

- Tools are static methods on a `[McpServerToolType]` class with `[McpServerTool(Name = "snake_case", Destructive = true|false)]` and a prescriptive `[Description]`.
- Parameter names use snake_case (`working_directory`, `max_output_chars`) — they reach the LLM verbatim through the schema.
- Always return `JsonSerializer.Serialize(result, McpJson.Options)`. `McpJson.Options` applies `JsonNamingPolicy.SnakeCaseLower`, so PascalCase anonymous types serialize as snake_case JSON automatically.
- Errors return `{ "error": "<message>" }` rather than throwing.
- Tool/parameter descriptions are LLM-steering surface area: be prescriptive about *when* to use which option, not just what it is. Mirror the `HttpRequestTool` `max_response_chars` and `RunCommandTool` `shell` descriptions.

## RunCommandTool subtleties

These are non-obvious and easy to break:

- `cmd.exe` (`shell: "auto"` on Windows, `shell: "cmd"`) uses raw `psi.Arguments`, **not** `ArgumentList`. cmd does not parse argv with CommandLineToArgvW conventions, and `ArgumentList` leaks backslashes around embedded quotes (`echo "a"` → `\"a\"`). All other shells use `ArgumentList`.
- PowerShell shells (`pwsh`, `powershell`) use `-EncodedCommand` with a UTF-8 prelude (`$OutputEncoding=[Console]::OutputEncoding=[System.Text.Encoding]::UTF8;`) prepended before base64 encoding. Without the prelude, Windows PowerShell 5.1 emits OEM-codepage bytes (CP949 on Korean Windows) and the parent's UTF-8 reader sees mojibake.
- Output truncation must **keep draining** the pipe after the visible cap; breaking the read loop deadlocks verbose children on a full stdout/stderr pipe.
- The shell-availability cache stores successes only. Failures are re-probed so a transient cold-start miss (AV scan, slow disk) does not poison the rest of the process lifetime. Probe timeout is 5 s.

## Tests

- Run: `dotnet test -c Release` from repo root. CI passes `--no-build` after a separate `dotnet build -c Release`.
- OS-conditional tests use `OperatingSystem.IsWindows()` + `Assert.Inconclusive("...")`. Do not use `Assert.Skip` (MSTest does not have it).
- For optional shells (e.g. `pwsh` may not be installed), parse the JSON, look for an `error` containing `"not available"`, and `Assert.Inconclusive` if so. See `TryReadUnavailable` helper in `RunCommandToolTests`.
- CI matrix: `windows-latest`, `ubuntu-latest`, `macos-latest`. All three must be green before a release commit.

## Release process

Two-stage. Do not combine.

1. **Code commit** — every change *except* version-bump files. Push, then wait for all 3 CI matrix jobs to pass.
2. **Release commit** — bumps these three files together:
   - `RELEASENOTES.md` — new dated section at top with `### Added` / `### Fixed` / `### Behaviour notes` as appropriate.
   - `src/FieldCure.Mcp.Essentials/.mcp/server.json` — both `version` and `packages[0].version`.
   - `src/FieldCure.Mcp.Essentials/FieldCure.Mcp.Essentials.csproj` — `<Version>` and `<PackageReleaseNotes>`.

   Commit subject follows existing pattern: `Release vX.Y.Z`.

## NuGet publish

Always use `scripts/publish-nuget.ps1`. **Never `dotnet nuget push` directly** — every published package must be EV-code-signed by the GlobalSign USB dongle (Subject: `Fieldcure Co., Ltd.`). The script handles clean → pack → sign → push. It reads `NUGET_API_KEY` from env, and the signing step opens a Windows GUI dialog for the dongle PIN.

```powershell
.\scripts\publish-nuget.ps1                      # full
.\scripts\publish-nuget.ps1 -SkipPush            # pack + sign, no upload
.\scripts\publish-nuget.ps1 -SkipSign -SkipPush  # pack only, no dongle needed
```

## Architecture decisions

ADRs live in the sibling `fieldcure-assiststudio` repo under `docs/ADR-NNN-*.md`. Reference them in commits and release notes when applicable. Active ADRs that constrain this repo:

- **ADR-001** — MCP credential management (visible defaults, fail-fast on misconfiguration).
- **ADR-002** — RunCommandTool cross-platform & observable behavior (shell selection, output truncation).
