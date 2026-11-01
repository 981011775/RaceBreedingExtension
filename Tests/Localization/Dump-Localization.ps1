#Requires -Version 5.1
<#
  Dump-Localization.ps1
  用途：把 RaceBreedingExtension 的三份 resx（EN / ZH / RU）导出成便于人工校对的三列表格。

  注意：resx 文件并非全部带 UTF-8 BOM（Strings.resx / Strings.ru.resx 无 BOM），
        因此这里一律用 [System.IO.File]::ReadAllText(..., UTF8) 显式读取，
        不能用 Get-Content（在中文系统上会按 GBK 解码，导致俄文变成乱码）。

  用法：
    powershell -NoProfile -ExecutionPolicy Bypass -File Tests\Localization\Dump-Localization.ps1
  输出：
    Tests\Localization\out\ResxTable.txt    全量三列对照表
    Tests\Localization\out\MissingRu.txt    仅 RU 缺失 / 占位符不一致等质检结果
#>
[CmdletBinding()]
param(
    [string]$Root
)

$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if ([string]::IsNullOrWhiteSpace($Root)) {
    $Root = Join-Path $scriptDir '..\..\ComplexBreedingRedux\Properties'
}

$outDir = Join-Path $scriptDir 'out'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$utf8 = New-Object System.Text.UTF8Encoding($false)

function Read-Resx([string]$path) {
    $doc = New-Object System.Xml.XmlDocument
    $doc.PreserveWhitespace = $true
    $doc.LoadXml([System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8))
    $map = [ordered]@{}
    foreach ($d in $doc.root.data) { $map[$d.name] = $d.value }
    return $map
}

$en = Read-Resx (Join-Path $Root 'Strings.resx')
$zh = Read-Resx (Join-Path $Root 'Strings.zh-cn.resx')
$ru = Read-Resx (Join-Path $Root 'Strings.ru.resx')

$keys = @($en.Keys) + @($zh.Keys) + @($ru.Keys) | Sort-Object -Unique

$table = New-Object System.Collections.Generic.List[string]
$issues = New-Object System.Collections.Generic.List[string]

function Get-Placeholders([string]$s) {
    $m = [regex]::Matches($s, '\{\d+\}') | ForEach-Object { $_.Value } | Sort-Object
    return ($m -join ',')
}

foreach ($k in $keys) {
    $e = if ($en.Contains($k)) { [string]$en[$k] } else { '<missing>' }
    $z = if ($zh.Contains($k)) { [string]$zh[$k] } else { '<missing>' }
    $r = if ($ru.Contains($k)) { [string]$ru[$k] } else { '<missing>' }
    $table.Add(("{0}`n    EN: {1}`n    ZH: {2}`n    RU: {3}" -f $k, $e, $z, $r))

    if ($e -eq '<missing>' -or $z -eq '<missing>' -or $r -eq '<missing>') {
        $miss = @()
        if ($e -eq '<missing>') { $miss += 'EN' }
        if ($z -eq '<missing>') { $miss += 'ZH' }
        if ($r -eq '<missing>') { $miss += 'RU' }
        $issues.Add(("MISSING [{0}]  {1}" -f ($miss -join ','), $k))
        continue
    }
    if ((Get-Placeholders $e) -ne (Get-Placeholders $z)) {
        $issues.Add(("PLACEHOLDER EN/ZH  {0}  EN=[{1}] ZH=[{2}]" -f $k, (Get-Placeholders $e), (Get-Placeholders $z)))
    }
    if ((Get-Placeholders $e) -ne (Get-Placeholders $r)) {
        $issues.Add(("PLACEHOLDER EN/RU  {0}  EN=[{1}] RU=[{2}]" -f $k, (Get-Placeholders $e), (Get-Placeholders $r)))
    }
    if ($e -match '[^\x00-\x7F]') {
        $issues.Add(("NON-ASCII-IN-EN    {0}  => {1}" -f $k, $e))
    }
    if (($null -ne $e) -and ($e -ne $e.Trim()) -and ($z -eq $z.Trim())) {
        $issues.Add(("SPACE-EN-ONLY      {0}  EN='{1}' ZH='{2}'" -f $k, $e, $z))
    }
    if (($null -ne $z) -and ($z -ne $z.Trim()) -and ($e -eq $e.Trim())) {
        $issues.Add(("SPACE-ZH-ONLY      {0}  EN='{1}' ZH='{2}'" -f $k, $e, $z))
    }
}

$tablePath = Join-Path $outDir 'ResxTable.txt'
$issuePath = Join-Path $outDir 'MissingRu.txt'
[System.IO.File]::WriteAllText($tablePath, ($table -join "`r`n"), $utf8)
[System.IO.File]::WriteAllText($issuePath, ($issues -join "`r`n"), $utf8)

Write-Host ("EN={0}  ZH={1}  RU={2}  UNION={3}" -f $en.Count, $zh.Count, $ru.Count, $keys.Count)
Write-Host ("issues={0}  -> {1}" -f $issues.Count, $issuePath)
Write-Host ("table      -> {0}" -f $tablePath)
