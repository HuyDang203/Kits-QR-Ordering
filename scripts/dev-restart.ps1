param(
    [switch]$Watch,
    [switch]$NoBuild,
    [switch]$StopOnly
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "dev-host.ps1")

Write-Host ""
Write-Host "ANNAP dev restart"
Write-Host "-----------------"

& (Join-Path $PSScriptRoot "dev-stop.ps1") -Quiet
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

if ($StopOnly) {
    Write-Host "Stop-only complete."
    exit 0
}

if (-not $NoBuild) {
    Write-Host ""
    Write-Host "Building web project..."
    dotnet build $script:AnnapWebProject
    if ($LASTEXITCODE -ne 0) {
        Write-Host ""
        Write-Host "Build failed. If MSB3021/MSB3027 appears, rerun:"
        Write-Host "  .\scripts\dev-stop.ps1"
        Write-Host "  .\scripts\dev-restart.ps1"
        exit $LASTEXITCODE
    }
}

Initialize-AnnapDevEnvironment

Write-Host ""
if ($Watch) {
    Write-Host "Starting dotnet watch (single session - do not also run dotnet run)."
    Write-Host "Press Ctrl+C for graceful shutdown."
    Write-Host ""
    dotnet watch --project $script:AnnapWebProject
}
else {
    Write-Host "Starting dotnet run."
    Write-Host "Press Ctrl+C for graceful shutdown."
    Write-Host "For Razor/C# rebuilds: .\scripts\dev-restart.ps1"
    Write-Host "For CSS/JS only: hard-refresh the browser (no rebuild required)."
    Write-Host ""
    dotnet run --project $script:AnnapWebProject
}

exit $LASTEXITCODE
