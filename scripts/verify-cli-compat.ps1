[CmdletBinding()]
param(
    [string]$AppExe = ".\src\Cmux\bin\Release\net10.0-windows10.0.17763.0\cmuxw.exe",
    [string]$CmuxExe = ".\src\Cmux.Cli\bin\Release\net10.0-windows\cmux.exe",
    [int]$TimeoutSec = 10
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Invoke-Cmux {
    param(
        [string[]]$CmdArgs,
        [int]$TimeoutSeconds = 10
    )

    $job = Start-Job -ScriptBlock {
        param($exePath, $argList)
        & $exePath @argList 2>&1 | Out-String
    } -ArgumentList $CmuxExe, $CmdArgs

    try {
        if (-not (Wait-Job -Job $job -Timeout $TimeoutSeconds)) {
            Stop-Job -Job $job | Out-Null
            return @{
                Ok = $false
                Timeout = $true
                Output = ""
            }
        }

        $output = (Receive-Job -Job $job) -join "`n"
        if ($null -eq $output) { $output = "" }
        return @{
            Ok = $true
            Timeout = $false
            Output = $output.Trim()
        }
    }
    finally {
        Remove-Job -Job $job -Force -ErrorAction SilentlyContinue | Out-Null
    }
}

function Add-TestResult {
    param(
        [string]$Name,
        [bool]$Pass,
        [string]$Output,
        [string]$Reason = ""
    )
    $script:Results += [pscustomobject]@{
        Name = $Name
        Pass = $Pass
        Reason = $Reason
        Output = $Output
    }
}

if (-not (Test-Path $AppExe)) { throw "App executable not found: $AppExe" }
if (-not (Test-Path $CmuxExe)) { throw "cmux executable not found: $CmuxExe" }
$AppExe = (Resolve-Path $AppExe).Path
$CmuxExe = (Resolve-Path $CmuxExe).Path

$script:Results = @()
$appProcess = $null

try {
    Get-Process cmuxw -ErrorAction SilentlyContinue | Stop-Process -Force
    $appProcess = Start-Process -FilePath $AppExe -PassThru
    Start-Sleep -Seconds 4

    # readiness probe (identify has fallback contract and should never block)
    $ready = $false
    for ($i = 0; $i -lt 8; $i++) {
        $probe = Invoke-Cmux -CmdArgs @("identify") -TimeoutSeconds $TimeoutSec
        if ($probe.Ok -and -not $probe.Timeout -and $probe.Output) {
            $ready = $true
            break
        }
        Start-Sleep -Seconds 1
    }
    if (-not $ready) {
        throw "cmux CLI readiness probe failed (identify command timeout)."
    }

    $treeText = Invoke-Cmux -CmdArgs @("tree", "--all") -TimeoutSeconds $TimeoutSec
    Add-TestResult -Name "tree --all" -Pass ($treeText.Ok -and -not $treeText.Timeout -and $treeText.Output -match "workspace:\d+" -and $treeText.Output -match "surface:\d+") -Output $treeText.Output -Reason "must include workspace/surface tokens"

    $treeJson = Invoke-Cmux -CmdArgs @("tree", "--all", "--json") -TimeoutSeconds $TimeoutSec
    $treeJsonPass = $false
    if ($treeJson.Ok -and -not $treeJson.Timeout) {
        try {
            $obj = $treeJson.Output | ConvertFrom-Json
            $treeJsonPass = $null -ne $obj.workspaces
        }
        catch { $treeJsonPass = $false }
    }
    Add-TestResult -Name "tree --all --json" -Pass $treeJsonPass -Output $treeJson.Output -Reason "must parse as JSON with workspaces"

    $identify = Invoke-Cmux -CmdArgs @("identify") -TimeoutSeconds $TimeoutSec
    $identifyPass = $false
    if ($identify.Ok -and -not $identify.Timeout) {
        try {
            $obj = $identify.Output | ConvertFrom-Json
            $identifyPass = ($null -ne $obj.caller.surface_ref -and $null -ne $obj.caller.workspace_ref)
        }
        catch { $identifyPass = $false }
    }
    Add-TestResult -Name "identify" -Pass $identifyPass -Output $identify.Output -Reason "must include caller.surface_ref/workspace_ref"

    $capture = Invoke-Cmux -CmdArgs @("capture-pane", "--workspace", "workspace:1", "--surface", "surface:1", "--lines", "20") -TimeoutSeconds $TimeoutSec
    $capturePass = $capture.Ok -and -not $capture.Timeout -and -not ($capture.Output.TrimStart().StartsWith("{"))
    Add-TestResult -Name "capture-pane" -Pass $capturePass -Output $capture.Output -Reason "default stdout should be plain text"

    $readScreen = Invoke-Cmux -CmdArgs @("read-screen", "--workspace", "workspace:1", "--surface", "surface:1", "--lines", "20") -TimeoutSeconds $TimeoutSec
    $readScreenPass = $readScreen.Ok -and -not $readScreen.Timeout -and -not ($readScreen.Output.TrimStart().StartsWith("{"))
    Add-TestResult -Name "read-screen" -Pass $readScreenPass -Output $readScreen.Output -Reason "default stdout should be plain text"

    $setBuffer = Invoke-Cmux -CmdArgs @("set-buffer", "--name", "t1", "--", "hello") -TimeoutSeconds $TimeoutSec
    Add-TestResult -Name "set-buffer --name" -Pass ($setBuffer.Ok -and -not $setBuffer.Timeout -and $setBuffer.Output -match '"ok"\s*:\s*true') -Output $setBuffer.Output

    $pasteBuffer = Invoke-Cmux -CmdArgs @("paste-buffer", "--name", "t1", "--workspace", "workspace:1", "--surface", "surface:1") -TimeoutSeconds $TimeoutSec
    Add-TestResult -Name "paste-buffer --name" -Pass ($pasteBuffer.Ok -and -not $pasteBuffer.Timeout -and $pasteBuffer.Output -match '"ok"\s*:\s*true') -Output $pasteBuffer.Output

    $setBufferSurface = Invoke-Cmux -CmdArgs @("set-buffer", "--surface", "surface:1", "hello") -TimeoutSeconds $TimeoutSec
    Add-TestResult -Name "set-buffer --surface" -Pass ($setBufferSurface.Ok -and -not $setBufferSurface.Timeout -and $setBufferSurface.Output -match '"ok"\s*:\s*true') -Output $setBufferSurface.Output

    $displayMessage = Invoke-Cmux -CmdArgs @("display-message", "test") -TimeoutSeconds $TimeoutSec
    Add-TestResult -Name "display-message" -Pass ($displayMessage.Ok -and -not $displayMessage.Timeout -and $displayMessage.Output -match '"ok"\s*:\s*true') -Output $displayMessage.Output

    $hook = Invoke-Cmux -CmdArgs @("claude-hook", "stop") -TimeoutSeconds $TimeoutSec
    Add-TestResult -Name "claude-hook stop" -Pass ($hook.Ok -and -not $hook.Timeout -and $hook.Output -match '"ok"\s*:\s*true') -Output $hook.Output

    $log = Invoke-Cmux -CmdArgs @("log", "--level", "info", "--source", "test", "hello") -TimeoutSeconds $TimeoutSec
    Add-TestResult -Name "log --level --source" -Pass ($log.Ok -and -not $log.Timeout -and $log.Output -match '"ok"\s*:\s*true') -Output $log.Output

    $sendEnter = Invoke-Cmux -CmdArgs @("send-key", "--workspace", "workspace:1", "--surface", "surface:1", "enter") -TimeoutSeconds $TimeoutSec
    Add-TestResult -Name "send-key enter" -Pass ($sendEnter.Ok -and -not $sendEnter.Timeout -and $sendEnter.Output -match '"ok"\s*:\s*true') -Output $sendEnter.Output

    $sendEscape = Invoke-Cmux -CmdArgs @("send-key", "--workspace", "workspace:1", "--surface", "surface:1", "escape") -TimeoutSeconds $TimeoutSec
    Add-TestResult -Name "send-key escape" -Pass ($sendEscape.Ok -and -not $sendEscape.Timeout -and $sendEscape.Output -match '"ok"\s*:\s*true') -Output $sendEscape.Output

    $sendTab = Invoke-Cmux -CmdArgs @("send-key", "--workspace", "workspace:1", "--surface", "surface:1", "tab") -TimeoutSeconds $TimeoutSec
    Add-TestResult -Name "send-key tab" -Pass ($sendTab.Ok -and -not $sendTab.Timeout -and $sendTab.Output -match '"ok"\s*:\s*true') -Output $sendTab.Output

    $sendUp = Invoke-Cmux -CmdArgs @("send-key", "--workspace", "workspace:1", "--surface", "surface:1", "up") -TimeoutSeconds $TimeoutSec
    Add-TestResult -Name "send-key up" -Pass ($sendUp.Ok -and -not $sendUp.Timeout -and $sendUp.Output -match '"ok"\s*:\s*true') -Output $sendUp.Output

    $sendDown = Invoke-Cmux -CmdArgs @("send-key", "--workspace", "workspace:1", "--surface", "surface:1", "down") -TimeoutSeconds $TimeoutSec
    Add-TestResult -Name "send-key down" -Pass ($sendDown.Ok -and -not $sendDown.Timeout -and $sendDown.Output -match '"ok"\s*:\s*true') -Output $sendDown.Output

    $sendLeft = Invoke-Cmux -CmdArgs @("send-key", "--workspace", "workspace:1", "--surface", "surface:1", "left") -TimeoutSeconds $TimeoutSec
    Add-TestResult -Name "send-key left" -Pass ($sendLeft.Ok -and -not $sendLeft.Timeout -and $sendLeft.Output -match '"ok"\s*:\s*true') -Output $sendLeft.Output

    $sendRight = Invoke-Cmux -CmdArgs @("send-key", "--workspace", "workspace:1", "--surface", "surface:1", "right") -TimeoutSeconds $TimeoutSec
    Add-TestResult -Name "send-key right" -Pass ($sendRight.Ok -and -not $sendRight.Timeout -and $sendRight.Output -match '"ok"\s*:\s*true') -Output $sendRight.Output

    $sendSpace = Invoke-Cmux -CmdArgs @("send-key", "--workspace", "workspace:1", "--surface", "surface:1", "space") -TimeoutSeconds $TimeoutSec
    Add-TestResult -Name "send-key space" -Pass ($sendSpace.Ok -and -not $sendSpace.Timeout -and $sendSpace.Output -match '"ok"\s*:\s*true') -Output $sendSpace.Output

    New-Item -ItemType Directory -Path "C:\temp" -Force | Out-Null
    $shotPath = "C:\temp\shot.png"
    if (Test-Path $shotPath) { Remove-Item $shotPath -Force }
    $screenshot = Invoke-Cmux -CmdArgs @("browser", "screenshot", "--surface", "surface:1", "--out", $shotPath) -TimeoutSeconds $TimeoutSec
    $shotPass = $screenshot.Ok -and -not $screenshot.Timeout -and (Test-Path $shotPath)
    Add-TestResult -Name "browser screenshot" -Pass $shotPass -Output $screenshot.Output -Reason "png file should exist"
}
finally {
    if ($null -ne $appProcess) {
        Stop-Process -Id $appProcess.Id -Force -ErrorAction SilentlyContinue
    }
    Get-Process cmuxw -ErrorAction SilentlyContinue | Stop-Process -Force
}

$passCount = @($script:Results | Where-Object { $_.Pass }).Count
$totalCount = @($script:Results).Count

Write-Host "=== cmux CLI Compatibility Verification ==="
Write-Host "Passed: $passCount / $totalCount"
foreach ($row in $script:Results) {
    $icon = if ($row.Pass) { "PASS" } else { "FAIL" }
    if ([string]::IsNullOrWhiteSpace($row.Reason)) {
        Write-Host ("[{0}] {1}" -f $icon, $row.Name)
    }
    else {
        Write-Host ("[{0}] {1} - {2}" -f $icon, $row.Name, $row.Reason)
    }
}

if ($passCount -ne $totalCount) {
    Write-Host ""
    Write-Host "=== Failure Details ==="
    foreach ($row in $script:Results | Where-Object { -not $_.Pass }) {
        Write-Host ("--- {0} ---" -f $row.Name)
        Write-Host $row.Output
    }
    exit 1
}

exit 0
