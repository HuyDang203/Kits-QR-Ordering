$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot + "\.."

$trayPath = "wwwroot\js\order-tray-dock.js"
$tray = Get-Content $trayPath -Raw

$dupPattern = '(?s)function trayCopyFallback\(key, vi, en\) \{.*?\n        \}\r?\n\r?\n        function trayCopyFallback\(key, vi, en\) \{.*?\n        \}'
$tray = [regex]::Replace($tray, $dupPattern, "function trayCopy(key) {`r`n            return tOrder(key) || `"`";`r`n        }", 1)

$tray = [regex]::Replace($tray, 'trayCopyFallback\(\s*"([^"]+)"[^)]*\)', 'trayCopy("$1")')
$tray = [regex]::Replace($tray, 'tOrder\("([^"]+)"\) \|\| trayCopy\("\1"\)', 'tOrder("$1")')

Set-Content $trayPath -Value $tray -NoNewline

$trackPath = "Pages\Track\Order.cshtml"
$track = Get-Content $trackPath -Raw
$track = $track -replace '(?m)^\s*const MILESTONE_FALLBACK = \[.*?\];\r?\n\r?\n', ''
$track = $track -replace 'return MILESTONE_FALLBACK\[step - 1\] \|\| "";', 'return "";'
$track = [regex]::Replace($track, 'trackT\("([^"]+)"\) \|\| "[^"]*"', 'trackT("$1")')
$track = [regex]::Replace($track, 'bill\.shopName \|\| trackT\("([^"]+)"\) \|\| "[^"]*"', 'bill.shopName || trackT("$1")')
$track = [regex]::Replace($track, 'data\.message \|\| trackT\("([^"]+)"\)', 'data.message || trackT("$1")')

Set-Content $trackPath -Value $track -NoNewline
Write-Host "Fixed tray and track fallbacks"
