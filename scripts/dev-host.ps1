# Shared ANNAP local development host utilities.
# Dot-source from dev-stop.ps1, dev-restart.ps1, and dev-restart.ps1.

$script:AnnapDevRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$script:AnnapWebProject = Join-Path $AnnapDevRoot "Annap.CoffeeQrOrdering.Web\Annap.CoffeeQrOrdering.Web.csproj"
$script:AnnapDllProbes = @(
    (Join-Path $AnnapDevRoot "Annap.CoffeeQrOrdering.Domain\bin\Debug\net8.0\Annap.CoffeeQrOrdering.Domain.dll"),
    (Join-Path $AnnapDevRoot "Annap.CoffeeQrOrdering.Application\bin\Debug\net8.0\Annap.CoffeeQrOrdering.Application.dll"),
    (Join-Path $AnnapDevRoot "Annap.CoffeeQrOrdering.Infrastructure\bin\Debug\net8.0\Annap.CoffeeQrOrdering.Infrastructure.dll"),
    (Join-Path $AnnapDevRoot "Annap.CoffeeQrOrdering.Web\bin\Debug\net8.0\Annap.CoffeeQrOrdering.Domain.dll"),
    (Join-Path $AnnapDevRoot "Annap.CoffeeQrOrdering.Web\bin\Debug\net8.0\Annap.CoffeeQrOrdering.Application.dll"),
    (Join-Path $AnnapDevRoot "Annap.CoffeeQrOrdering.Web\bin\Debug\net8.0\Annap.CoffeeQrOrdering.Infrastructure.dll")
)

function Get-AnnapHostProcesses {
    $hosts = @()

    $exe = Get-Process -Name "Annap.CoffeeQrOrdering.Web" -ErrorAction SilentlyContinue
    foreach ($p in $exe) {
        $hosts += [pscustomobject]@{
            ProcessId   = $p.Id
            Kind        = "Annap.CoffeeQrOrdering.Web"
            IsWatch     = $false
            CommandLine = $null
        }
    }

    $dotnet = Get-CimInstance Win32_Process -Filter "name = 'dotnet.exe'" -ErrorAction SilentlyContinue |
        Where-Object { $_.CommandLine -like "*Annap.CoffeeQrOrdering.Web*" }

    foreach ($p in $dotnet) {
        $isWatch = $p.CommandLine -like "*watch*"
        $kind = if ($isWatch) { "dotnet-watch" } else { "dotnet-run" }
        $hosts += [pscustomobject]@{
            ProcessId   = $p.ProcessId
            Kind        = $kind
            IsWatch     = [bool]$isWatch
            CommandLine = $p.CommandLine
        }
    }

    return $hosts | Sort-Object ProcessId -Unique
}

function Stop-AnnapHostProcesses {
    param(
        [string]$Reason = "Stopping ANNAP development hosts..."
    )

    $hosts = Get-AnnapHostProcesses
    if ($hosts.Count -eq 0) {
        Write-Host "No ANNAP host processes found."
        return 0
    }

    Write-Host $Reason
    foreach ($p in $hosts) {
        Write-Host "  taskkill /PID $($p.ProcessId) /T /F  ($($p.Kind))"
        if ($p.CommandLine) {
            Write-Host "    $($p.CommandLine)"
        }
        taskkill /PID $p.ProcessId /T /F | Out-Null
    }

    return $hosts.Count
}

function Wait-ForAnnapExit {
    param([int]$TimeoutSeconds = 15)

    for ($i = 0; $i -lt $TimeoutSeconds; $i++) {
        if ((Get-AnnapHostProcesses).Count -eq 0) {
            return $true
        }
        Start-Sleep -Seconds 1
    }

    return $false
}

function Test-DllUnlocked {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return $true
    }

    try {
        $stream = [System.IO.File]::Open(
            $Path,
            [System.IO.FileMode]::Open,
            [System.IO.FileAccess]::ReadWrite,
            [System.IO.FileShare]::None)
        $stream.Close()
        return $true
    }
    catch {
        return $false
    }
}

function Get-AnnapLockedDlls {
    $locked = @()
    foreach ($path in $script:AnnapDllProbes) {
        if (-not (Test-DllUnlocked -Path $path)) {
            $locked += $path
        }
    }
    return $locked
}

function Wait-ForAnnapDllUnlock {
    param([int]$TimeoutSeconds = 15)

    for ($i = 0; $i -lt $TimeoutSeconds; $i++) {
        $locked = Get-AnnapLockedDlls
        if ($locked.Count -eq 0) {
            return @()
        }
        Start-Sleep -Seconds 1
    }

    return (Get-AnnapLockedDlls)
}

function Write-AnnapHostDiagnostics {
    $hosts = Get-AnnapHostProcesses
    $watchCount = @($hosts | Where-Object { $_.IsWatch }).Count
    $runCount = @($hosts | Where-Object { -not $_.IsWatch }).Count

    Write-Host ""
    Write-Host "ANNAP dev host diagnostics"
    Write-Host "--------------------------"
    Write-Host "  Web project: $script:AnnapWebProject"
    Write-Host "  Host processes: $($hosts.Count) (dotnet-run=$runCount, dotnet-watch=$watchCount)"
    if ($watchCount -gt 1) {
        Write-Host "  WARNING: Multiple dotnet watch sessions detected. Use a single watcher only."
    }
    if ($hosts.Count -gt 1) {
        Write-Host "  WARNING: Multiple ANNAP hosts cause MSB3021/MSB3027 DLL lock failures on rebuild."
    }

    foreach ($p in $hosts) {
        Write-Host "  PID $($p.ProcessId) [$($p.Kind)]"
        if ($p.CommandLine) { Write-Host "    $($p.CommandLine)" }
    }

    $locked = Get-AnnapLockedDlls
    if ($locked.Count -gt 0) {
        Write-Host ""
        Write-Host "  Locked DLLs:"
        foreach ($dll in $locked) {
            Write-Host "    $dll"
        }
    }
    else {
        Write-Host "  Locked DLLs: none detected"
    }
    Write-Host ""
}

function Initialize-AnnapDevEnvironment {
    $env:ASPNETCORE_ENVIRONMENT = "Development"
    Remove-Item Env:ASPNETCORE_URLS -ErrorAction SilentlyContinue
}
