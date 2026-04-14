# Changelog

## v0.1.4 - 2026-04-14

### Theme engine rewrite — Cursor-style light/dark mode
- **Root cause fix**: DarkTheme.xaml brushes used `{StaticResource}` Color bindings which WPF froze at load time, making runtime theme switching silently fail. All brushes now use inline hex values so `.Color` mutation works.
- **Root cause fix**: `ApplyBrushColor` now writes directly to `Application.Resources[key]` (highest priority in WPF lookup), guaranteeing `DynamicResource` bindings update everywhere.
- **Root cause fix**: Removed `Application.ThemeMode` usage — the .NET 10 Fluent theme injection was force-overriding every custom brush, breaking both dark and light modes completely.
- Light mode colors now match Cursor: chrome/sidebar `#F3F3F3`, terminal pane `#F8F8F8`, input fields `#FFFFFF`, text `#383A42`.
- Dark mode colors match Cursor: chrome `#1E1E1E`, sidebar `#181818`, text `#CCCCCC`.
- `SystemColors` (Window, Control, Menu, Highlight) updated for both modes so ComboBox dropdowns, context menus, and ListBox items render correct foreground/background.
- DWM title bar frame (`DwmUseImmersiveDarkMode`) now re-applied on every theme switch via `WindowAppearance.RefreshDarkFrameForOpenWindows()`.
- Terminal right-click context menu: replaced hardcoded dark colors with theme-aware brush lookups.
- All sub-windows (`HistoryWindow`, `LogsWindow`, `ColorPickerWindow`, `TextPromptWindow`, `SessionVaultWindow`) converted from `StaticResource` to `DynamicResource` for `BackgroundBrush`.
- ComboBox dropdown popup: added `TextElement.Foreground` on `DropDownBorder`, `ScrollViewer`, and `ContentSite` to fix invisible text in light mode.
- Terminal `Default Light` palette: background `#F8F8F8`, yellow ANSI slots darkened to prevent invisible text on light backgrounds.

### Layout & pane stability
- Fixed toolbar layout change destroying all terminal sessions by replacing `NormalizeToSinglePane` with selective `ClosePane` + `SplitNode.BuildDenseGridRowMajor`.
- Added `SurfaceViewModel.ReplaceRootNode` for safe tree reconstruction.
- Added pane reset buttons and session clear logic per pane/sidebar/agent panel.
- Settings > Behavior: added "Reset All Sessions" with confirmation dialog.

### Other
- `SplitPaneContainer.RefreshChromeForTheme()` — forces pane chrome rebuild on theme change so code-created borders pick up new brushes.
- `BrowserControl.xaml`: `StaticResource` → `DynamicResource` for background.
- Bumped version to `0.1.4`.

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
