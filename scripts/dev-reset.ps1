param(
    [switch]$NoRun,
    [switch]$Watch
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "dev-host.ps1")

Write-Host ""
Write-Host "ANNAP developer reset (full clean)"
Write-Host "----------------------------------"
Write-AnnapHostDiagnostics

& (Join-Path $PSScriptRoot "dev-stop.ps1") -Quiet
if ($LASTEXITCODE -ne 0) {
    Write-Host "dev-stop reported issues; continuing with clean anyway..."
}

Write-Host ""
Write-Host "Cleaning build outputs..."
dotnet clean $script:AnnapWebProject

$buildDirs = Get-ChildItem -Path $script:AnnapDevRoot -Directory -Recurse -Force |
    Where-Object { $_.Name -in @("bin", "obj") -and $_.FullName -notmatch "\\node_modules\\" }

foreach ($dir in $buildDirs) {
    Write-Host "Removing $($dir.FullName)"
    Remove-Item -LiteralPath $dir.FullName -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "Rebuilding web project..."
dotnet build $script:AnnapWebProject
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

if ($NoRun) {
    Write-Host ""
    Write-Host "Reset complete. Run when ready:"
    Write-Host "  .\scripts\dev-restart.ps1"
    Write-Host "  .\scripts\dev-restart.ps1 -Watch"
    exit 0
}

Initialize-AnnapDevEnvironment

Write-Host ""
if ($Watch) {
    Write-Host "Starting ANNAP with dotnet watch (single watcher - do not stack another dotnet run)."
    Write-Host "Press Ctrl+C to stop cleanly."
    Write-Host ""
    dotnet watch --project $script:AnnapWebProject
}
else {
    Write-Host "Starting ANNAP on the development port..."
    Write-Host "Press Ctrl+C to stop cleanly."
    Write-Host "Routine restart: .\scripts\dev-restart.ps1"
    Write-Host ""
    dotnet run --project $script:AnnapWebProject
}

exit $LASTEXITCODE
