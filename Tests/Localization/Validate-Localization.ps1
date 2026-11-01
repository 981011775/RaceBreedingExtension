#Requires -Version 5.1
<#
  Validate-Localization.ps1
  用途：校验 RaceBreedingExtension 的本地化文件与代码引用是否一致（EN / zh-CN / ru）。

  检查项
    1. resx 是否为合法 XML，且 EN / zh-CN / ru 的键集合完全一致
    2. 三种语言的 {0} / {1} 占位符完全一致（防止格式化时崩溃或串位）
    3. ru 里是否残留空白值 / 未翻译的英文
    4. 代码中 Loc("...") / GetString("...") / Text("...") 用到的字面量键是否存在于默认（EN）resx
    5. 代码中 Strings.<Name> 强类型引用是否存在于默认（EN）resx（即 Designer 与 resx 是否同步）
    6. 子模块 resx（DepravedShield / VoiceReplay / TitsModCompat）EN / zh-CN / ru 键集合是否一致

  用法：
    powershell -NoProfile -ExecutionPolicy Bypass -File Tests\Localization\Validate-Localization.ps1
#>
[CmdletBinding()]
param(
    [string]$Root
)

$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if ([string]::IsNullOrWhiteSpace($Root)) { $Root = (Resolve-Path (Join-Path $scriptDir '..\..')).Path }

$results = New-Object System.Collections.Generic.List[object]
function Add-Result([string]$Name, [bool]$Ok, [string]$Detail) {
    $results.Add([pscustomobject]@{ Name = $Name; Ok = $Ok; Detail = $Detail })
}

function Read-ResxMap([string]$path) {
    if (-not (Test-Path -LiteralPath $path)) { return $null }
    $doc = New-Object System.Xml.XmlDocument
    $doc.LoadXml([System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8))
    $map = @{}
    foreach ($d in $doc.root.data) { $map[$d.name] = [string]$d.value }
    return $map
}

function Get-Placeholders([string]$s) {
    $m = [regex]::Matches($s, '\{\d+\}') | ForEach-Object { $_.Value } | Sort-Object
    return ($m -join ',')
}

# ------------------------------------------------------------ main resx -----

$resxDir = Join-Path $Root 'ComplexBreedingRedux\Properties'
$en = Read-ResxMap (Join-Path $resxDir 'Strings.resx')
$zh = Read-ResxMap (Join-Path $resxDir 'Strings.zh-cn.resx')
$ru = Read-ResxMap (Join-Path $resxDir 'Strings.ru.resx')

Add-Result 'main resx parsed' (($null -ne $en) -and ($null -ne $zh) -and ($null -ne $ru)) `
    ("EN={0} ZH={1} RU={2}" -f $en.Count, $zh.Count, $ru.Count)

$missingZh = @($en.Keys | Where-Object { -not $zh.ContainsKey($_) })
$missingRu = @($en.Keys | Where-Object { -not $ru.ContainsKey($_) })
$extraZh = @($zh.Keys | Where-Object { -not $en.ContainsKey($_) })
$extraRu = @($ru.Keys | Where-Object { -not $en.ContainsKey($_) })
Add-Result 'key parity EN/ZH/RU' `
    (($missingZh.Count -eq 0) -and ($missingRu.Count -eq 0) -and ($extraZh.Count -eq 0) -and ($extraRu.Count -eq 0)) `
    ("missingZH={0} missingRU={1} extraZH={2} extraRU={3}" -f $missingZh.Count, $missingRu.Count, $extraZh.Count, $extraRu.Count)

$phBad = New-Object System.Collections.Generic.List[string]
foreach ($k in $en.Keys) {
    if (-not $zh.ContainsKey($k) -or -not $ru.ContainsKey($k)) { continue }
    $a = Get-Placeholders $en[$k]
    if ($a -ne (Get-Placeholders $zh[$k])) { $phBad.Add("ZH $k EN[$a] ZH[$(Get-Placeholders $zh[$k])]") }
    if ($a -ne (Get-Placeholders $ru[$k])) { $phBad.Add("RU $k EN[$a] RU[$(Get-Placeholders $ru[$k])]") }
}
Add-Result 'placeholder parity' ($phBad.Count -eq 0) (($phBad | Select-Object -First 5) -join ' ;; ')

$ruEmpty = @($ru.Keys | Where-Object { [string]::IsNullOrWhiteSpace($ru[$_]) })
$ruLatinWhitelist = @('PrefType_Vip', 'PrefType_Vvip')
$ruLatinOnly = @()
foreach ($k in $ru.Keys) {
    if ($ruLatinWhitelist -contains $k) { continue }
    $v = $ru[$k]
    $stripped = $v -replace '(?s)<[^>]*>', '' -replace '\{\d+\}', '' -replace '[\s0-9%#=~\-.,:\+\(\)xX]', ''
    if ($stripped.Length -gt 0 -and $stripped -notmatch '[\u0400-\u04FF]') { $ruLatinOnly += $k }
}
Add-Result 'ru values present' ($ruEmpty.Count -eq 0) ("empty={0}" -f ($ruEmpty -join ','))
Add-Result 'ru values translated' ($ruLatinOnly.Count -eq 0) ("latin-only: {0}" -f ($ruLatinOnly -join ','))

# -------------------------------------------------- code key references -----

# 每个子模块使用自己的 resx；其余（ComplexBreedingRedux / Bloodline / HunterCamp /
# GroundCamp / UniqueLookColor / CombatPower ...）统一使用 ComplexBreedingRedux 的主 resx。
$moduleMaps = @{}
foreach ($mod in 'DepravedShield', 'VoiceReplay', 'TitsModCompat') {
    $map = Read-ResxMap (Join-Path (Join-Path $Root "$mod\Properties") 'Strings.resx')
    if ($null -ne $map) { $moduleMaps[$mod] = $map }
}

$csFiles = Get-ChildItem -Path $Root -Recurse -Include *.cs -File |
    Where-Object { $_.FullName -notmatch '\\\\(bin|obj|Tests)\\\\' -and $_.Name -notlike '*.Designer.cs' }

$literalKeys = New-Object System.Collections.Generic.HashSet[string]
$strongKeys = New-Object System.Collections.Generic.HashSet[string]
$strongIgnore = @('Culture', 'ResourceManager', 'Assembly', 'resx', 'zh', 'cn', 'ru', 'Designer')

$literalBad = New-Object System.Collections.Generic.List[string]
$strongBad = New-Object System.Collections.Generic.List[string]

foreach ($f in $csFiles) {
    $text = [System.IO.File]::ReadAllText($f.FullName, [System.Text.Encoding]::UTF8)
    $rel = $f.FullName.Substring($Root.Length).TrimStart('\')
    $seg = $rel.Split('\')[0]
    $map = if ($moduleMaps.ContainsKey($seg)) { $moduleMaps[$seg] } else { $en }

    # 只统计“整个参数就是一个字面量”的调用：Loc("Key") / Text("Key") / GetString("Key")
    foreach ($m in [regex]::Matches($text, '(?:Loc|Text|GetString)\(\s*"([A-Za-z0-9_]+)"\s*\)')) {
        $key = $m.Groups[1].Value
        $literalKeys.Add($key) | Out-Null
        if (-not $map.ContainsKey($key)) { $literalBad.Add("$rel : $key") }
    }
    # 强类型引用：Strings.SomeKey（排除完全限定名与文件名写法）
    foreach ($m in [regex]::Matches($text, '(?<![\\\w.])Strings\.([A-Za-z_][A-Za-z0-9_]*)')) {
        $name = $m.Groups[1].Value
        if ($strongIgnore -contains $name) { continue }
        $strongKeys.Add($name) | Out-Null
        if (-not $map.ContainsKey($name)) { $strongBad.Add("$rel : $name") }
    }
}

Add-Result 'code literal keys exist' ($literalBad.Count -eq 0) (($literalBad | Sort-Object) -join ',')
Add-Result 'Strings.* properties exist' ($strongBad.Count -eq 0) (($strongBad | Sort-Object) -join ',')

# ------------------------------------------------------- sub module resx -----

foreach ($mod in 'DepravedShield', 'VoiceReplay', 'TitsModCompat') {
    $dir = Join-Path $Root "$mod\Properties"
    $e = Read-ResxMap (Join-Path $dir 'Strings.resx')
    $z = Read-ResxMap (Join-Path $dir 'Strings.zh-CN.resx')
    $r = Read-ResxMap (Join-Path $dir 'Strings.ru.resx')
    $ok = ($null -ne $e) -and ($null -ne $z) -and ($null -ne $r)
    if ($ok) {
        $ok = (@($e.Keys | Where-Object { -not $z.ContainsKey($_) }).Count -eq 0) -and
              (@($e.Keys | Where-Object { -not $r.ContainsKey($_) }).Count -eq 0)
    }
    Add-Result "$mod resx EN/ZH/RU parity" $ok ("EN={0} ZH={1} RU={2}" -f $e.Count, $z.Count, $r.Count)
}

# ------------------------------------------------------------------ report ---

$failed = @($results | Where-Object { -not $_.Ok })
foreach ($r in $results) {
    $tag = if ($r.Ok) { 'PASS' } else { 'FAIL' }
    Write-Host ("[{0}] {1}  {2}" -f $tag, $r.Name, $r.Detail)
}
Write-Host ''
Write-Host ("total={0}  failed={1}" -f $results.Count, $failed.Count)
if ($failed.Count -gt 0) { exit 1 }
exit 0

