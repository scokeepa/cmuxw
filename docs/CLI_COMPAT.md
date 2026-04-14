# CLI Compatibility (manaflow-ai/cmux)

This document tracks compatibility between upstream `manaflow-ai/cmux` command style and cmuxw (this Windows implementation).

## Fully supported aliases

| Upstream style | This project behavior |
|---|---|
| `cmux list-workspaces` | Supported |
| `cmux new-workspace` | Supported (`--name`, basic passthrough for `--cwd/--command`) |
| `cmux select-workspace --workspace <id/ref/index>` | Supported |
| `cmux close-workspace --workspace <id/ref/index>` | Supported |
| `cmux rename-workspace --workspace <id/ref/index> <title>` | Supported |
| `cmux current-workspace` | Supported |
| `cmux list-surfaces` | Supported |
| `cmux select-surface --workspace ... --surface ...` | Supported |
| `cmux close-surface --workspace ... --surface ...` | Supported |
| `cmux new-pane` | Supported (creates terminal surface) |
| `cmux new-split --direction right/down` | Supported |
| `cmux tree --all` | Supported (text output with stable `workspace:n`, `surface:n` tokens) |
| `cmux tree --all --json` | Supported |
| `cmux identify` | Supported (`caller.surface_ref`, `caller.workspace_ref`) |
| `cmux read-screen --workspace ... --surface ... --pane ...` | Supported (plain-text default output) |
| `cmux capture-pane --workspace ... --surface ... [--scrollback] [--lines N]` | Supported (plain-text default, read-screen fallback contract) |
| `cmux send --workspace ... --surface ... --pane ... <text>` | Supported |
| `cmux send-key --workspace ... --surface ... --pane ... <key>` | Supported (`enter/return`, `escape`, `tab`, arrows, `space`) |
| `cmux set-buffer --name <buf> -- <text>` | Supported |
| `cmux set-buffer --surface <surface> "<text>"` | Supported (hook-compatible form) |
| `cmux paste-buffer --name <buf> --workspace ... --surface ...` | Supported (default unnamed buffer included) |
| `cmux display-message "<text>"` | Supported |
| `cmux claude-hook <event>` | Supported (`{"ok":true}` no-op success contract) |
| `cmux log --level <...> --source <...> "<msg>"` | Supported (`{"ok":true}` no-op success contract) |
| `cmux browser screenshot --surface ... --out <path>` | Supported (PNG file save) |
| `cmux workspace-action --action next/previous/rename` | Supported |
| `cmux set-status <key> <value>` | Supported (workspace metadata) |
| `cmux trigger-flash --workspace ... --surface ... --pane ...` | Supported (BEL visual flash) |

## ID selector compatibility

Selectors accept:

- UUID IDs
- Numeric index (`1`, `2`, ...)
- Ref format (`workspace:1`, `surface:2`, `pane:3`)

## Output contracts

- `read-screen` / `capture-pane`: plain text on stdout by default (JSON only when `--json` provided).
- `identify`: minimum JSON payload:
  ```json
  {
    "caller": {
      "surface_ref": "surface:1",
      "workspace_ref": "workspace:1"
    }
  }
  ```
- `tree --all` text mode always includes regex-safe `workspace:<n>` and `surface:<n>` tokens.

## Notes

- Compatibility is implemented as alias commands on top of the existing Windows command model.
- Existing commands (`workspace`, `surface`, `pane`, `split`) continue to work.
- Selector forms keep UUID/ref/index compatibility (`id`, `workspace:n`, `surface:n`, numeric index).

## Verification snapshot (2026-04-14)

Validated in this repository (`dotnet run --project src/Cmux.Cli -- ...`):

- `tree --all` (text token contract: `workspace:<n>`, `surface:<n>`)
- `tree --all --json`
- `identify` (`caller.surface_ref`, `caller.workspace_ref`)
- `capture-pane --workspace workspace:1 --surface surface:1 --lines 20` (plain text default)
- `set-buffer --name t1 -- "hello"`
- `paste-buffer --name t1 --workspace workspace:1 --surface surface:1`
- `set-buffer --surface surface:1 "hello"`
- `display-message "test"`
- `claude-hook stop` (`{"ok":true}` contract)
- `log --level info --source test "hello"` (`{"ok":true}` contract)
- `send-key --workspace workspace:1 --surface surface:1 enter`
- `send-key --workspace workspace:1 --surface surface:1 escape`
- `browser screenshot --surface surface:1 --out C:\temp\shot.png` (file write confirmed)
