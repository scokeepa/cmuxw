# CLI Compatibility (manafow-ai/cmux)

This document tracks compatibility between upstream `manafow-ai/cmux` command style and this Windows implementation.

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
| `cmux read-screen --workspace ... --surface ... --pane ...` | Supported |
| `cmux send --workspace ... --surface ... --pane ... <text>` | Supported |
| `cmux send-key --workspace ... --surface ... --pane ... Return` | Supported |
| `cmux workspace-action --action next/previous/rename` | Supported |
| `cmux set-status <key> <value>` | Supported (workspace metadata) |
| `cmux trigger-flash --workspace ... --surface ... --pane ...` | Supported (BEL visual flash) |

## ID selector compatibility

Selectors accept:

- UUID IDs
- Numeric index (`1`, `2`, ...)
- Ref format (`workspace:1`, `surface:2`, `pane:3`)

## Not implemented yet

These upstream areas are not implemented in this Windows build yet:

- Browser command family (`cmux browser ...`)
- Socket-level auth/password flags
- Full upstream status/progress model parity

## Notes

- Compatibility is implemented as alias commands on top of the existing Windows command model.
- Existing commands (`workspace`, `surface`, `pane`, `split`) continue to work.
