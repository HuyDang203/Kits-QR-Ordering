param(
    [switch]$Quiet
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "dev-host.ps1")

if (-not $Quiet) {
    Write-Host ""
    Write-Host "ANNAP dev stop"
    Write-Host "--------------"
    Write-AnnapHostDiagnostics
}

$stopped = Stop-AnnapHostProcesses -Reason "Stopping ANNAP development hosts..."
Start-Sleep -Milliseconds 500

if (-not (Wait-ForAnnapExit -TimeoutSeconds 15)) {
    Write-Host ""
    Write-Host "WARNING: ANNAP processes are still running after graceful stop attempt."
    Write-Host "Close the terminal that started dotnet run / dotnet watch, then rerun:"
    Write-Host "  .\scripts\dev-stop.ps1"
    Write-Host ""
    exit 1
}

$locked = Wait-ForAnnapDllUnlock -TimeoutSeconds 12
if ($locked.Count -gt 0) {
    Write-Host "Second termination pass (DLLs still locked)..."
    Stop-AnnapHostProcesses -Reason "Retrying host termination..."
    Wait-ForAnnapExit -TimeoutSeconds 10 | Out-Null
    $locked = Wait-ForAnnapDllUnlock -TimeoutSeconds 10
}

if ($locked.Count -gt 0) {
    Write-Host ""
    Write-Host "ERROR: DLLs are still locked:"
    foreach ($dll in $locked) { Write-Host "  $dll" }
    Write-Host ""
    Write-Host "Manual check:"
    Write-Host "  Get-Process Annap.CoffeeQrOrdering.Web, dotnet"
    Write-Host "  .\scripts\dev-host.ps1   # dot-source and run Write-AnnapHostDiagnostics"
    Write-Host ""
    exit 1
}

if (-not $Quiet) {
    Write-Host "ANNAP dev host stopped. DLL locks released."
    Write-Host "Safe to run: dotnet build / dotnet run / .\scripts\dev-restart.ps1"
    Write-Host ""
}

exit 0
