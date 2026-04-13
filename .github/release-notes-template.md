# {{TAG}}

## Download
- Asset: `{{ASSET_NAME}}`
- URL: {{ASSET_URL}}

## Installation (Windows)
1. Download `{{ASSET_NAME}}`
2. Extract to a folder, for example `C:\tools\cmuxw`
3. Run `cmuxw.exe`

```powershell
# Example
Expand-Archive -Path .\{{ASSET_NAME}} -DestinationPath C:\tools\cmuxw -Force
C:\tools\cmuxw\cmux-win-x64\cmuxw.exe
```

## Checksum verification (SHA256)
Expected SHA256:
`{{SHA256}}`

### PowerShell
```powershell
Get-FileHash .\{{ASSET_NAME}} -Algorithm SHA256
```

### Command Prompt (certutil)
```cmd
certutil -hashfile {{ASSET_NAME}} SHA256
```

If the printed hash matches the expected SHA256 exactly, the file is verified.
