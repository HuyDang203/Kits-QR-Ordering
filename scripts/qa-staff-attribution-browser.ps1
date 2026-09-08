# Browser-equivalent QA for Staff Employee Account Attribution (HTTP + HTML checks)
$ErrorActionPreference = "Stop"
$Base = "http://localhost:8080"
$DisplayName = "Nguyễn Văn A"
$CheckoutFallback = "Nhân viên kiểm đơn"
$AdminFallback = "Quản lý"
$Results = [ordered]@{}

function Write-Qa($section, $pass, $detail) {
    $Results[$section] = @{ Pass = $pass; Detail = $detail }
    $mark = if ($pass) { "PASS" } else { "FAIL" }
    Write-Host "[$mark] $section - $detail"
}

function New-Session { return [Microsoft.PowerShell.Commands.WebRequestSession]::new() }

function Get-AntiforgeryToken($session, [string]$url) {
    $r = Invoke-WebRequest -Uri $url -WebSession $session -UseBasicParsing
    $m = [regex]::Match($r.Content, '<input name="__RequestVerificationToken" type="hidden" value="([^"]+)"')
    if ($m.Success) { return $m.Groups[1].Value }
    throw "Antiforgery token not found at $url"
}

function Login-Staff($session, [string]$user, [string]$password) {
    $token = Get-AntiforgeryToken $session "$Base/Staff/Login"
    $form = @{
        UserName                   = $user
        Password                   = $password
        __RequestVerificationToken = $token
    }
    Invoke-WebRequest -Uri "$Base/Staff/Login" -Method Post -WebSession $session -Body $form -UseBasicParsing -MaximumRedirection 5 | Out-Null
}

function Logout-Staff($session) {
    Invoke-WebRequest -Uri "$Base/Staff/Logout" -WebSession $session -UseBasicParsing -MaximumRedirection 5 | Out-Null
}

function Get-Json($session, [string]$url) {
    return Invoke-RestMethod -Uri $url -WebSession $session -Method Get
}

function Post-Json($session, [string]$url, $body = $null) {
    try {
        if ($null -eq $body) {
            return Invoke-WebRequest -Uri $url -WebSession $session -Method Post -UseBasicParsing
        }
        return Invoke-WebRequest -Uri $url -WebSession $session -Method Post -Body ($body | ConvertTo-Json) -ContentType "application/json" -UseBasicParsing
    } catch {
        if ($_.Exception.Response) {
            return [pscustomobject]@{ StatusCode = [int]$_.Exception.Response.StatusCode }
        }
        throw
    }
}

function Test-AdminDenied($session, [string]$path) {
    $r = Invoke-WebRequest -Uri "$Base$path" -WebSession $session -UseBasicParsing -MaximumRedirection 5
    $final = $r.BaseResponse.ResponseUri.AbsolutePath
    return ($final -notlike "/admin/*")
}

for ($i = 0; $i -lt 30; $i++) {
    try {
        Invoke-WebRequest -Uri "$Base/" -UseBasicParsing -TimeoutSec 3 | Out-Null
        break
    } catch {
        Start-Sleep -Seconds 2
    }
}

Write-Host "=== Part 2: Admin staff account creation ==="
$admin = New-Session
Login-Staff $admin "host" "ChangeMe"

$staffPage = Invoke-WebRequest -Uri "$Base/admin/staff-accounts" -WebSession $admin -UseBasicParsing
$titleOk = $staffPage.Content.Contains("admin-staff-accounts") -and $staffPage.Content.Contains("staff-accounts")
Write-Qa "Admin staff-accounts page" ($staffPage.StatusCode -eq 200 -and $titleOk) "HTTP $($staffPage.StatusCode)"

$createToken = Get-AntiforgeryToken $admin "$Base/admin/staff-accounts"
$createForm = @{
    CreateUsername             = "thu-ngan-1"
    CreateDisplayName          = $DisplayName
    CreatePassword             = "Test12345!"
    __RequestVerificationToken = $createToken
}
try {
    Invoke-WebRequest -Uri "$Base/admin/staff-accounts?handler=Create" -Method Post -WebSession $admin -Body $createForm -UseBasicParsing -MaximumRedirection 5 | Out-Null
} catch {
    # duplicate username acceptable on rerun
}

$listPage = Invoke-WebRequest -Uri "$Base/admin/staff-accounts" -WebSession $admin -UseBasicParsing
$hasAccount = $listPage.Content.Contains("thu-ngan-1")
$noPasswordShown = -not $listPage.Content.Contains("Test12345!")
Write-Qa "Account thu-ngan-1 in list" $hasAccount "Listed=$hasAccount"
Write-Qa "Password not displayed" $noPasswordShown "Plain password absent in HTML"
Write-Qa "Admin nav works" ($listPage.StatusCode -eq 200) "Admin page renders"

Write-Host ""
Write-Host "=== Part 3: Employee login and access ==="
Logout-Staff $admin
$emp = New-Session
Login-Staff $emp "thu-ngan-1" "Test12345!"

$ordersPage = Invoke-WebRequest -Uri "$Base/staff/orders" -WebSession $emp -UseBasicParsing
Write-Qa "Employee /staff/orders" ($ordersPage.StatusCode -eq 200) "HTTP $($ordersPage.StatusCode)"
$noAdminNav = -not $ordersPage.Content.Contains("/admin/staff-accounts") -and -not $ordersPage.Content.Contains("/admin/reports")
Write-Qa "No admin links on staff board" $noAdminNav "Admin URLs absent from staff orders HTML"

foreach ($path in @("/admin/reports", "/admin/payments", "/admin/staff-accounts")) {
    $ok = Test-AdminDenied $emp $path
    Write-Qa "Employee denied $path" $ok "Redirected away from admin"
}

$tableHome = Invoke-WebRequest -Uri "$Base/table/T01" -WebSession (New-Session) -UseBasicParsing
$venueTableId = $null
if ($tableHome.Content -match 'ANNAP_SERVER_VENUE_TABLE_ID = "([0-9a-fA-F-]{36})"') { $venueTableId = $Matches[1] }
elseif ($tableHome.Content -match 'id="venueTableIdHome" value="([0-9a-fA-F-]{36})"') { $venueTableId = $Matches[1] }
if (-not $venueTableId) { throw "Could not resolve venueTableId from /table/T01" }

$menu = Invoke-RestMethod -Uri "$Base/api/menu"
$menuItem = $null
foreach ($cat in $menu.categories) {
    if ($cat.items -and $cat.items.Count -gt 0) { $menuItem = $cat.items[0]; break }
}
if (-not $menuItem) { throw "No menu item in catalog" }

function Submit-GuestOrder($paymentMethod, [string]$idem) {
    $guest = New-Session
    Invoke-WebRequest -Uri "$Base/table/T01" -WebSession $guest -UseBasicParsing | Out-Null
    $payload = @{
        venueTableId   = $venueTableId
        idempotencyKey = $idem
        paymentMethod  = $paymentMethod
        items          = @(@{ menuItemId = $menuItem.id; quantity = 1; notes = $null })
    }
    $headers = @{ "Idempotency-Key" = $idem }
    return Invoke-RestMethod -Uri "$Base/api/orders" -Method Post -WebSession $guest -Body ($payload | ConvertTo-Json -Depth 5) -ContentType "application/json" -Headers $headers
}

Write-Host ""
Write-Host "=== Part 4: Cash/Card attribution ==="
$cash = Submit-GuestOrder "CashOrCardAtCounter" ("qa-cash-" + (Get-Date -Format "HHmmss"))
$boardBefore = Get-Json $emp "$Base/api/staff/orders"
$inSubmitted = @($boardBefore.submitted | Where-Object { $_.id -eq $cash.id }).Count -gt 0
Write-Qa "Cash order in submitted column" $inSubmitted "Order $($cash.id)"

$markPaid = Post-Json $emp "$Base/api/staff/orders/$($cash.id)/mark-paid" @{}
Write-Qa "Employee mark-paid cash" ($markPaid.StatusCode -eq 200) "HTTP $($markPaid.StatusCode)"

$boardAfter = Get-Json $emp "$Base/api/staff/orders"
$paidCard = @($boardAfter.paid | Where-Object { $_.id -eq $cash.id } | Select-Object -First 1)
$confirmer = $paidCard.paymentConfirmedBy
Write-Qa "Cash paid card confirmer (API = board UI source)" ($confirmer -eq $DisplayName) "paymentConfirmedBy=$confirmer"

$bill = Get-Json $emp "$Base/api/staff/orders/$($cash.id)/bill"
Write-Qa "Cash bill confirmer" ($bill.paymentConfirmedBy -eq $DisplayName) "bill.paymentConfirmedBy=$($bill.paymentConfirmedBy)"

$complete = Post-Json $emp "$Base/api/staff/orders/$($cash.id)/complete"
Write-Qa "Employee cannot complete" ($complete.StatusCode -eq 403) "HTTP $($complete.StatusCode)"

$itemId = $paidCard.items[0].id
$prep = Post-Json $emp "$Base/api/staff/orders/$($cash.id)/items/$itemId/prepared" @{ isPrepared = $true }
Write-Qa "Employee cannot prepare item" ($prep.StatusCode -eq 403) "HTTP $($prep.StatusCode)"

$ordersHtml = Invoke-WebRequest -Uri "$Base/staff/orders" -WebSession $emp -UseBasicParsing
$jsHasConfirmerTemplate = $ordersHtml.Content.Contains("staff-orders-board.js")
Write-Qa "Staff board loads staff-orders-board.js" $jsHasConfirmerTemplate "JS bundle present for confirmer render"

Write-Host ""
Write-Host "=== Part 5: BankTransfer manual attribution ==="
$bank = Submit-GuestOrder "BankTransfer" ("qa-bank-" + (Get-Date -Format "HHmmss"))
$qr = Invoke-RestMethod -Uri ("$Base/api/orders/$($bank.id)/transfer-qr?token=" + [Uri]::EscapeDataString($bank.guestSessionToken))
$hasQr = -not [string]::IsNullOrWhiteSpace($qr.memo)
Write-Qa "BankTransfer QR available" $hasQr "memo=$($qr.memo)"

$boardBank = Get-Json $emp "$Base/api/staff/orders"
$bankSubmitted = @($boardBank.submitted | Where-Object { $_.id -eq $bank.id } | Select-Object -First 1)
Write-Qa "Bank order in submitted" ($null -ne $bankSubmitted) "paymentMethod=$($bankSubmitted.paymentMethod)"

Post-Json $emp "$Base/api/staff/orders/$($bank.id)/mark-paid" @{} | Out-Null
$boardBankPaid = Get-Json $emp "$Base/api/staff/orders"
$bankPaid = @($boardBankPaid.paid | Where-Object { $_.id -eq $bank.id } | Select-Object -First 1)
Write-Qa "Bank paid card confirmer" ($bankPaid.paymentConfirmedBy -eq $DisplayName) "paymentConfirmedBy=$($bankPaid.paymentConfirmedBy)"

$bankBill = Get-Json $emp "$Base/api/staff/orders/$($bank.id)/bill"
Write-Qa "Bank bill confirmer" ($bankBill.paymentConfirmedBy -eq $DisplayName) "bill.paymentConfirmedBy=$($bankBill.paymentConfirmedBy)"

Write-Host ""
Write-Host "=== Part 6: Shared checkout fallback ==="
Logout-Staff $emp
$checkout = New-Session
Login-Staff $checkout "host" "ChangeMe"
$sharedOrder = Submit-GuestOrder "CashOrCardAtCounter" ("qa-shared-" + (Get-Date -Format "HHmmss"))
Post-Json $checkout "$Base/api/staff/orders/$($sharedOrder.id)/mark-paid" @{} | Out-Null
$sharedBoard = Get-Json $checkout "$Base/api/staff/orders"
$sharedPaid = @($sharedBoard.paid | Where-Object { $_.id -eq $sharedOrder.id } | Select-Object -First 1)
$sharedName = $sharedPaid.paymentConfirmedBy
if ($sharedName -eq $CheckoutFallback) {
    Write-Qa "Shared checkout fallback" $true "paymentConfirmedBy=$sharedName"
} elseif ($sharedName -eq $AdminFallback) {
    Write-Qa "Shared checkout fallback (blocked)" $false "Admin shared login used - StaffAuth CheckoutPassword not configured in dev appsettings"
} else {
    Write-Qa "Shared checkout fallback" $false "Unexpected paymentConfirmedBy=$sharedName"
}

Write-Host ""
Write-Host "=== Summary ==="
$failed = @($Results.Values | Where-Object { -not $_.Pass }).Count
$passed = @($Results.Values | Where-Object { $_.Pass }).Count
Write-Host "Passed: $passed / $($Results.Count)  Failed: $failed"
if ($failed -gt 0) { exit 1 }
