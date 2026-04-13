# cmuxw

Windows-native terminal multiplexer inspired by `cmux`, built with WPF + ConPTY.

## English

### Why cmuxw
- Dedicated Windows implementation (`cmux` + `windows`).
- Keyboard-first multiplexer with workspaces, surfaces, split panes.
- Session/log tooling for daily engineering workflows.

### Screenshots
![Main workspace](assets/screenshots/1.jpg)
![Snippets panel](assets/screenshots/2.jpg)
![Command logs](assets/screenshots/3.jpg)

### Quick start
```powershell
git clone https://github.com/scokeepa/cmuxw.git
cd cmuxw
dotnet build Cmux.sln -c Debug
dotnet run --project src/Cmux/Cmux.csproj -c Debug
```

### Command compatibility policy
- App/repository/product name: **cmuxw**
- CLI command namespace: **cmux** (kept intentionally for upstream compatibility)

```powershell
cmux workspace list
cmux workspace create --name "My Project"
cmux pane list
cmux split right
cmux status
```

### Browser surfaces (WebView2 + Playwright)
- **UI**: click the globe icon in the top toolbar, or use `File > New Browser`, or press `Ctrl+Shift+B`.
- **CLI**:

```powershell
cmux browser open https://github.com/manaflow-ai/cmux
cmux browser list
cmux browser snapshot
```

### Build
```powershell
dotnet publish src/Cmux/Cmux.csproj -c Release -r win-x64 --self-contained false -o publish/cmux-win-x64
dotnet publish src/Cmux.Cli/Cmux.Cli.csproj -c Release -r win-x64 --self-contained true -o publish/cmux-cli
```

### Releases
```powershell
.\release.ps1 -Tag v0.1.3 -AssetPath publish/cmuxw-win-x64.zip
.\release.ps1 -Tag v0.1.3 -AssetPath publish/cmuxw-win-x64.zip -UpdateExisting
```

Release notes history: [`CHANGELOG.md`](CHANGELOG.md)

---

## 한국어

### cmuxw 소개
- `cmux + windows`를 결합한 Windows 전용 터미널 멀티플렉서입니다.
- 워크스페이스/서피스/분할 패널 기반의 키보드 중심 UX를 제공합니다.
- 명령 로그, 세션 보관함 등 운영 기능을 포함합니다.

### 빠른 시작
```powershell
git clone https://github.com/scokeepa/cmuxw.git
cd cmuxw
dotnet build Cmux.sln -c Debug
dotnet run --project src/Cmux/Cmux.csproj -c Debug
```

### 명령어 호환성 정책
- 저장소/앱 이름은 **cmuxw**
- CLI 명령어는 상위 호환성을 위해 **cmux**를 유지

```powershell
cmux workspace list
cmux pane list
cmux split right
```

### 브라우저 서피스 (WebView2 + Playwright)
- **UI**: 상단 툴바의 지구본 아이콘 클릭, `파일 > 새 브라우저`, 또는 `Ctrl+Shift+B`.
- **CLI**:

```powershell
cmux browser open https://github.com/manaflow-ai/cmux
cmux browser list
cmux browser snapshot
```

### 협업/문서
- 기여 가이드: [`CONTRIBUTING.md`](CONTRIBUTING.md)
- CLI 호환성 문서: [`docs/CLI_COMPAT.md`](docs/CLI_COMPAT.md)
- 이슈/PR 템플릿 활성화

## License
MIT
