# kwcmux

A keyboard-first terminal multiplexer for Windows, inspired by tmux workflows and built with WPF + ConPTY.

## What you get

- Multiple **workspaces** for project context separation
- Surface tabs and **split panes** for parallel terminal work
- Agent-friendly **OSC notifications** and unread tracking
- **Command logs/history** with replay support
- **Session Vault** for transcript search and session restore
- Keyboard-first UX with dark theme and fast command palette

## Screenshots

### Main workspace
![Main workspace](assets/screenshots/1.jpg)

### Snippets panel
![Snippets panel](assets/screenshots/2.jpg)

### Command logs
![Command logs](assets/screenshots/3.jpg)

## Quick start (Windows)

### Requirements

- Windows 10/11
- [.NET 10 SDK](https://dotnet.microsoft.com/download)

### Run from source

```powershell
git clone https://github.com/scokeepa/kwcmux.git
cd kwcmux
dotnet build Cmux.sln -c Debug
dotnet run --project src/Cmux/Cmux.csproj -c Debug
```

### Use included publish artifact

```powershell
.\publish\cmux-win-x64\cmuxw.exe
```

## First 5 minutes

1. Open `cmuxw.exe`
2. `Ctrl+N` create workspace
3. `Ctrl+T` create surface(tab)
4. `Ctrl+D` / `Ctrl+Shift+D` split pane
5. `Ctrl+Shift+P` open command palette
6. `Ctrl+Shift+L` open command logs
7. `Ctrl+Shift+V` open Session Vault

## Key shortcuts

| Area | Shortcut | Action |
|---|---|---|
| Workspaces | `Ctrl+N` | New workspace |
| Workspaces | `Ctrl+1..9` | Jump workspace |
| Surfaces | `Ctrl+T` | New surface |
| Surfaces | `Ctrl+Shift+]` / `Ctrl+Shift+[` | Next / previous surface |
| Panes | `Ctrl+D` / `Ctrl+Shift+D` | Split right / down |
| Panes | `Ctrl+Alt+Arrow` | Focus adjacent pane |
| General | `Ctrl+Shift+P` | Command palette |
| General | `Ctrl+,` | Settings |

## CLI examples

```powershell
cmux notify --title "Build" --body "Done"
cmux workspace list
cmux workspace create --name "My Project"
cmux split right
cmux status
```

## Build publish outputs

### App executable

```powershell
dotnet publish src/Cmux/Cmux.csproj -c Release -r win-x64 --self-contained false -o publish/cmux-win-x64
```

### CLI executable

```powershell
dotnet publish src/Cmux.Cli/Cmux.Cli.csproj -c Release -r win-x64 --self-contained true -o publish/cmux-cli
```

## Collaboration

- Contribution guide: [`CONTRIBUTING.md`](CONTRIBUTING.md)
- Issue templates and PR template are enabled
- `master` is protected and merge is PR-based

## Project layout

```text
src/
  Cmux/         WPF desktop app
  Cmux.Core/    terminal engine, models, services, IPC
  Cmux.Cli/     automation CLI
tests/
  Cmux.Tests/   unit tests
```

## License

MIT
