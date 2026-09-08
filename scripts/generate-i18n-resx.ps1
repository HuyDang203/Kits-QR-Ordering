param(
    [string]$WebRoot = "$PSScriptRoot\..\Annap.CoffeeQrOrdering.Web"
)

function Flatten-JsonObject {
    param([object]$Obj, [string]$Prefix = "")
    $result = @{}
    if ($Obj -is [System.Collections.IDictionary] -or $Obj.PSObject.Properties) {
        foreach ($prop in $Obj.PSObject.Properties) {
            if ($prop.Name -eq 'lang') { continue }
            $key = if ($Prefix) { "$Prefix.$($prop.Name)" } else { $prop.Name }
            if ($prop.Value -is [PSCustomObject]) {
                $nested = Flatten-JsonObject -Obj $prop.Value -Prefix $key
                foreach ($k in $nested.Keys) { $result[$k] = $nested[$k] }
            } else {
                $result[$key] = [string]$prop.Value
            }
        }
    }
    return $result
}

function Write-Resx {
    param([string]$Path, [hashtable]$Entries)
    $sb = New-Object System.Text.StringBuilder
    [void]$sb.AppendLine('<?xml version="1.0" encoding="utf-8"?>')
    [void]$sb.AppendLine('<root>')
    [void]$sb.AppendLine('  <resheader name="resmimetype"><value>text/microsoft-resx</value></resheader>')
    [void]$sb.AppendLine('  <resheader name="version"><value>2.0</value></resheader>')
    [void]$sb.AppendLine('  <resheader name="reader"><value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>')
    [void]$sb.AppendLine('  <resheader name="writer"><value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>')
    foreach ($key in ($Entries.Keys | Sort-Object)) {
        $val = [System.Security.SecurityElement]::Escape($Entries[$key])
        [void]$sb.AppendLine("  <data name=""$key"" xml:space=""preserve""><value>$val</value></data>")
    }
    [void]$sb.AppendLine('</root>')
    $dir = Split-Path $Path -Parent
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    [System.IO.File]::WriteAllText($Path, $sb.ToString(), [System.Text.UTF8Encoding]::new($false))
}

$enJson = Get-Content (Join-Path $WebRoot 'wwwroot\i18n\guest-en.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$viJson = Get-Content (Join-Path $WebRoot 'wwwroot\i18n\guest-vi.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$en = Flatten-JsonObject $enJson
$vi = Flatten-JsonObject $viJson
$allKeys = @($en.Keys + $vi.Keys | Sort-Object -Unique)
$enFull = @{}
$viFull = @{}
foreach ($k in $allKeys) {
    $enFull[$k] = if ($en.ContainsKey($k)) { $en[$k] } else { $vi[$k] }
    $viFull[$k] = if ($vi.ContainsKey($k)) { $vi[$k] } else { $en[$k] }
}
$resDir = Join-Path $WebRoot 'Resources'
Write-Resx (Join-Path $resDir 'SharedResources.resx') $enFull
Write-Resx (Join-Path $resDir 'SharedResources.vi.resx') $viFull
Write-Output "Generated $($allKeys.Count) keys"
