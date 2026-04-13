# Changelog

## v0.1.3 - 2026-04-14

- Hardened named-pipe CLI transport so connect/write/read cannot hang indefinitely on Windows.
- Added orchestrator-oriented CLI commands and output contracts (`tree`, `identify`, `capture-pane`, buffer paste/set, `display-message`, `claude-hook`, `log`, `browser screenshot`) with safe fallbacks when the GUI is unreachable.
- Extended `send-key` compatibility mapping and aligned `read-screen` / `capture-pane` plain-text defaults with watcher-pack expectations.
- Added `scripts/verify-cli-compat.ps1` to automate DoD checks against a running `cmuxw` instance.
- Updated `docs/CLI_COMPAT.md` for the expanded command surface.
- Bumped version metadata to `0.1.3` across app, CLI, and agent runtime reporting.

## v0.1.2 - 2026-04-13

- Added browser surface MVP and `cmux browser` command set.
- Added Playwright-backed browser automation adapter and pane registry plumbing.
- Improved UI focus/i18n behavior with tri-language consistency updates.
- Fixed runtime localization traversal crash and normalized menu access-key artifacts.
- Updated browser open behavior to split-right with immediate navigation (no URL prompt).
- Added bottom-left theme toggle button and expanded theme handling (`Dark`, `Light`, `System`).
- Added browser startup warm-up plus queued navigation to reduce open latency.
- Added configurable browser default URL in `Settings > Behavior`.
- Added default usage guide document at `docs/USER_GUIDE.md`.
- Aligned app/runtime/CLI version metadata to `0.1.2`.

## v0.1.1 - 2026-04-13

- Rebrand to `cmuxw`.
- Tri-language localization baseline and settings integration.

## v0.1.0 - 2026-04-13

- First public release with Windows artifact packaging.
