# Usage from repo root:
#   .\scripts\capture-visual.ps1 -CaptureReference     # do ONCE
#   .\scripts\capture-visual.ps1                       # local snapshots thereafter
param(
    [switch]$CaptureReference,
    [string]$AppUrl = "http://localhost:5299"
)
$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot
if (-not (Get-Command playwright-cli -ErrorAction SilentlyContinue)) {
    throw "playwright-cli is missing. Run: npm install -g @playwright/cli@latest"
}
New-Item -ItemType Directory -Force -Path "references", "artifacts/visual" | Out-Null
function RunBrowser([string[]] $arguments) {
    & playwright-cli @arguments
    if ($LASTEXITCODE -ne 0) { throw "Playwright failed: $($arguments -join ' ')" }
}
if ($CaptureReference) {
    RunBrowser @('-s=vnews-reference','open','https://vnexpress.net/')
    foreach ($mode in @(@{name='desktop';width=1440;height=900}, @{name='mobile';width=390;height=844})) {
        $target="references/vnexpress-$($mode.name).png"
        if (Test-Path $target) {
            Write-Host "Preserving existing reference: $target"
            continue
        }
        RunBrowser @('-s=vnews-reference','resize',[string]$mode.width,[string]$mode.height)
        RunBrowser @('-s=vnews-reference','screenshot',"--filename=$target")
    }
}
RunBrowser @('-s=vnews-local','open',$AppUrl)
foreach ($mode in @(@{name='desktop';width=1440;height=900}, @{name='mobile';width=390;height=844})) {
    $target="artifacts/visual/vnews-$($mode.name).png"
    RunBrowser @('-s=vnews-local','resize',[string]$mode.width,[string]$mode.height)
    RunBrowser @('-s=vnews-local','screenshot',"--filename=$target")
}
Write-Host "Screenshots saved. Reference (if captured) in references; local in artifacts/visual."
