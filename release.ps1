param(
    [Parameter(Mandatory = $true)]
    [string]$Tag,

    [string]$AssetPath = "publish/kwcmux-win-x64.zip",
    [string]$Repo = "scokeepa/kwcmux",
    [string]$TargetBranch = "master",

    [switch]$UpdateExisting,
    [switch]$Draft,
    [switch]$Prerelease,
    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not (Test-Path -Path $AssetPath -PathType Leaf)) {
    throw "Asset not found: $AssetPath"
}

$templatePath = ".github/release-notes-template.md"
if (-not (Test-Path -Path $templatePath -PathType Leaf)) {
    throw "Release template not found: $templatePath"
}

$assetName = Split-Path -Path $AssetPath -Leaf
$assetUrl = "https://github.com/$Repo/releases/download/$Tag/$assetName"
$sha256 = (Get-FileHash -Path $AssetPath -Algorithm SHA256).Hash.ToLowerInvariant()

$notes = Get-Content -Path $templatePath -Raw
$notes = $notes.Replace("{{TAG}}", $Tag)
$notes = $notes.Replace("{{ASSET_NAME}}", $assetName)
$notes = $notes.Replace("{{ASSET_URL}}", $assetUrl)
$notes = $notes.Replace("{{SHA256}}", $sha256)

if ($DryRun) {
    Write-Host "===== RELEASE NOTES PREVIEW ====="
    Write-Output $notes
    exit 0
}

if ($UpdateExisting) {
    gh release edit $Tag -R $Repo --notes "$notes"
    Write-Host "Updated release notes for $Tag"
    exit 0
}

$createArgs = @(
    "release", "create", $Tag, $AssetPath,
    "-R", $Repo,
    "--target", $TargetBranch,
    "--title", $Tag,
    "--notes", $notes
)

if ($Draft) {
    $createArgs += "--draft"
}

if ($Prerelease) {
    $createArgs += "--prerelease"
}

gh @createArgs
Write-Host "Created release $Tag with asset $assetName"
