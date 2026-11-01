#Requires -Version 5.1
<#
  Apply-Localization.ps1
  用途：把 Tests\Localization\*.json 里的本地化修订写入 RaceBreedingExtension 的标准本地化文件（*.resx）。

  设计要点
    * 本脚本只包含 ASCII 字符，所有非 ASCII 文本都放在同目录的 UTF-8 JSON 数据文件里，
      并且一律用 [System.IO.File]::ReadAllText(..., UTF8) 显式读取，
      避免 Windows PowerShell 5.1 在无 BOM 的 .ps1/.resx 上按 ANSI（GBK）解码而毁掉中文/俄文。
    * 写入 resx 时保留原有换行符与 BOM 状态，只做「按键改值」与「在 </root> 前追加新键」两种操作。

  数据文件
    localization-fix.json   enFixes / zhFixes / ruFixes
    add-keys.json           需要在 EN+ZH+RU 三个文件里同时新增的键 { key: { en, zh, ru } }
    ru-add-1.json           Strings.ru.resx 缺失键（血脉/派系/战斗）
    ru-add-2.json           Strings.ru.resx 缺失键（物品/工作/偏好/职业/状态）
    new-files.json          需要新建的子系统 ru 资源文件

  用法：
    powershell -NoProfile -ExecutionPolicy Bypass -File Tests\Localization\Apply-Localization.ps1
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$Root
)

$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if ([string]::IsNullOrWhiteSpace($Root)) {
    $Root = Join-Path $scriptDir '..\..'
}
$Root = (Resolve-Path $Root).Path
$resxDir = Join-Path $Root 'ComplexBreedingRedux\Properties'

# ---------------------------------------------------------------- helpers ----

function Read-JsonFile([string]$path) {
    if (-not (Test-Path -LiteralPath $path)) { return $null }
    $raw = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
    return ($raw | ConvertFrom-Json)
}

function Test-ResxBom([string]$path) {
    $b = [System.IO.File]::ReadAllBytes($path)
    return ($b.Length -ge 3 -and $b[0] -eq 0xEF -and $b[1] -eq 0xBB -and $b[2] -eq 0xBF)
}

function Save-Resx([string]$path, [string]$text, [bool]$bom) {
    $enc = if ($bom) { New-Object System.Text.UTF8Encoding($true) } else { New-Object System.Text.UTF8Encoding($false) }
    [System.IO.File]::WriteAllText($path, $text, $enc)
}

function Get-NewLine([string]$text) {
    if ($text.IndexOf("`r`n") -ge 0) { return "`r`n" }
    return "`n"
}

function ConvertTo-ResxEscaped([string]$value) {
    return $value.Replace('&', '&amp;').Replace('<', '&lt;').Replace('>', '&gt;')
}

function Get-ResxValue([string]$text, [string]$key) {
    $pattern = '<data name="' + [regex]::Escape($key) + '" xml:space="preserve">.*?<value>(.*?)</value>'
    $m = [regex]::Match($text, $pattern, [System.Text.RegularExpressions.RegexOptions]::Singleline)
    if (-not $m.Success) { return $null }
    return $m.Groups[1].Value
}

function Test-ResxKey([string]$text, [string]$key) {
    return [regex]::IsMatch($text, '<data name="' + [regex]::Escape($key) + '" xml:space="preserve">')
}

function Set-ResxValue {
    param([string]$Text, [string]$Key, [string]$Value, [string]$NewLine)
    $pattern = '<data name="' + [regex]::Escape($Key) + '" xml:space="preserve">\s*<value>.*?</value>'
    $m = [regex]::Match($Text, $pattern, [System.Text.RegularExpressions.RegexOptions]::Singleline)
    if (-not $m.Success) { return $null }
    $block = '<data name="' + $Key + '" xml:space="preserve">' + $NewLine + '    <value>' +
             (ConvertTo-ResxEscaped $Value) + '</value>'
    return $Text.Substring(0, $m.Index) + $block + $Text.Substring($m.Index + $m.Length)
}

function Add-ResxEntry {
    param([string]$Text, [string]$Key, [string]$Value, [string]$NewLine)
    if (Test-ResxKey $Text $Key) { return $null }
    $idx = $Text.LastIndexOf('</root>')
    if ($idx -lt 0) { throw "resx has no </root>: cannot append $Key" }
    $block = '  <data name="' + $Key + '" xml:space="preserve">' + $NewLine +
             '    <value>' + (ConvertTo-ResxEscaped $Value) + '</value>' + $NewLine +
             '  </data>' + $NewLine
    return $Text.Substring(0, $idx) + $block + $Text.Substring($idx)
}

# ------------------------------------------------------------------ data -----

$fix = Read-JsonFile (Join-Path $scriptDir 'localization-fix.json')
$addKeys = Read-JsonFile (Join-Path $scriptDir 'add-keys.json')
$ruAdd = @{}
foreach ($file in @('ru-add-1.json', 'ru-add-2.json')) {
    $part = Read-JsonFile (Join-Path $scriptDir $file)
    if ($null -eq $part) { continue }
    foreach ($p in $part.PSObject.Properties) {
        if ($p.Name.StartsWith('_')) { continue }
        $ruAdd[$p.Name] = [string]$p.Value
    }
}
$newFiles = Read-JsonFile (Join-Path $scriptDir 'new-files.json')

# ------------------------------------------------------------- main resx -----

$targets = @(
    @{ Tag = 'EN'; Path = (Join-Path $resxDir 'Strings.resx'); Lang = 'en'; Fixes = $fix.enFixes },
    @{ Tag = 'ZH'; Path = (Join-Path $resxDir 'Strings.zh-cn.resx'); Lang = 'zh'; Fixes = $fix.zhFixes },
    @{ Tag = 'RU'; Path = (Join-Path $resxDir 'Strings.ru.resx'); Lang = 'ru'; Fixes = $fix.ruFixes }
)

$changed = @{}
foreach ($t in $targets) {
    $changed[$t.Tag] = [System.IO.File]::ReadAllText($t.Path, [System.Text.Encoding]::UTF8)
}

# 1) change values of existing keys
if ($fix) {
    foreach ($t in $targets) {
        if ($null -eq $t.Fixes) { continue }
        foreach ($p in $t.Fixes.PSObject.Properties) {
            if ($p.Name.StartsWith('_')) { continue }
            $key = $p.Name
            $newValue = [string]$p.Value
            $text = $changed[$t.Tag]
            $old = Get-ResxValue $text $key
            if ($null -eq $old) {
                Write-Warning ("[{0}] key not found, skipped: {1}" -f $t.Tag, $key)
                continue
            }
            if ($old -ceq (ConvertTo-ResxEscaped $newValue)) { continue }
            $updated = Set-ResxValue -Text $text -Key $key -Value $newValue -NewLine (Get-NewLine $text)
            if ($null -eq $updated) { Write-Warning ("[{0}] failed to set: {1}" -f $t.Tag, $key); continue }
            $changed[$t.Tag] = $updated
            Write-Host ("[{0}] FIX  {1}" -f $t.Tag, $key)
        }
    }
}

# 2) add-keys.json -> add to EN + ZH + RU
if ($addKeys) {
    foreach ($p in $addKeys.PSObject.Properties) {
        if ($p.Name.StartsWith('_')) { continue }
        $key = $p.Name
        foreach ($t in $targets) {
            $value = [string]$p.Value.($t.Lang)
            if ([string]::IsNullOrEmpty($value)) { Write-Warning ("add-keys: {0} has no '{1}'" -f $key, $t.Lang); continue }
            $text = $changed[$t.Tag]
            $updated = Add-ResxEntry -Text $text -Key $key -Value $value -NewLine (Get-NewLine $text)
            if ($null -eq $updated) { Write-Host ("[{0}] HAVE {1}" -f $t.Tag, $key); continue }
            $changed[$t.Tag] = $updated
            Write-Host ("[{0}] ADD  {1}" -f $t.Tag, $key)
        }
    }
}

# 3) ru-add -> Strings.ru.resx only; verify the key exists in EN and ZH first
if ($ruAdd.Count -gt 0) {
    foreach ($key in ($ruAdd.Keys | Sort-Object)) {
        if (-not (Test-ResxKey $changed['EN'] $key)) { Write-Warning ("ru-add: no EN key '{0}' - skipped" -f $key); continue }
        if (-not (Test-ResxKey $changed['ZH'] $key)) { Write-Warning ("ru-add: no ZH key '{0}' - skipped" -f $key); continue }
        $text = $changed['RU']
        $updated = Add-ResxEntry -Text $text -Key $key -Value $ruAdd[$key] -NewLine (Get-NewLine $text)
        if ($null -eq $updated) { Write-Host ("[RU] HAVE {0}" -f $key); continue }
        $changed['RU'] = $updated
        Write-Host ("[RU] ADD  {0}" -f $key)
    }
}

# ------------------------------------------------------------------ write ----

foreach ($t in $targets) {
    $original = [System.IO.File]::ReadAllText($t.Path, [System.Text.Encoding]::UTF8)
    if ($original -ceq $changed[$t.Tag]) { Write-Host ("[{0}] unchanged" -f $t.Tag); continue }
    if ($PSCmdlet.ShouldProcess($t.Path, 'write resx')) {
        Save-Resx $t.Path $changed[$t.Tag] (Test-ResxBom $t.Path)
        Write-Host ("[{0}] written: {1}" -f $t.Tag, $t.Path)
    }
}

# ------------------------------------------------------- new ru resources ----

if ($newFiles) {
    $template = @'
<?xml version="1.0" encoding="utf-8"?>
<root>
  <resheader name="resmimetype">
    <value>text/microsoft-resx</value>
  </resheader>
  <resheader name="version">
    <value>2.0</value>
  </resheader>
  <resheader name="reader">
    <value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
  <resheader name="writer">
    <value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
@@DATA@@
</root>
'@
    foreach ($f in $newFiles.files) {
        $path = Join-Path $Root $f.path
        $body = ''
        foreach ($p in $f.entries.PSObject.Properties) {
            $body += '  <data name="' + $p.Name + '" xml:space="preserve">' + "`r`n" +
                     '    <value>' + (ConvertTo-ResxEscaped ([string]$p.Value)) + '</value>' + "`r`n" +
                     '  </data>' + "`r`n"
        }
        $xml = $template.Replace('@@DATA@@', $body.TrimEnd("`r`n"))
        if ((Test-Path -LiteralPath $path) -and ([System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8) -ceq $xml)) {
            Write-Host ("[NEW] unchanged: {0}" -f $f.path)
            continue
        }
        if ($PSCmdlet.ShouldProcess($path, 'write resx')) {
            [System.IO.File]::WriteAllText($path, $xml, (New-Object System.Text.UTF8Encoding($false)))
            Write-Host ("[NEW] written: {0}" -f $f.path)
        }
    }
}

Write-Host ''
Write-Host 'Done. Re-run Dump-Localization.ps1 to verify.'

