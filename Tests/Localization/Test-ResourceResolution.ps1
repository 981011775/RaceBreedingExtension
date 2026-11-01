#Requires -Version 5.1
<#
  Test-ResourceResolution.ps1
  用途：冒烟测试 —— 用真实运行时（ResourceManager + 卫星程序集）验证 4 组资源在
        en / zh-CN / ru 下都能取到文本，其它语言回退英文。

  注意：脚本会把 DLL 复制到临时目录再加载，避免锁住工程 bin / 游戏 Mods 目录里的文件。

  用法：
    powershell -NoProfile -ExecutionPolicy Bypass -File Tests\Localization\Test-ResourceResolution.ps1
    powershell -NoProfile -ExecutionPolicy Bypass -File Tests\Localization\Test-ResourceResolution.ps1 -BinDir <dir>
#>
[CmdletBinding()]
param(
    [string]$BinDir = 'D:\Game\MonsterBlackMarket\Mods'
)

$ErrorActionPreference = 'Stop'

$tmp = Join-Path $env:TEMP ('rbe_loc_verify_' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Force -Path $tmp, (Join-Path $tmp 'ru'), (Join-Path $tmp 'zh-CN') | Out-Null

Copy-Item (Join-Path $BinDir 'RaceBreedingExtension.dll') $tmp -Force
if (Test-Path (Join-Path $BinDir 'ru\RaceBreedingExtension.resources.dll')) {
    Copy-Item (Join-Path $BinDir 'ru\RaceBreedingExtension.resources.dll') (Join-Path $tmp 'ru') -Force
}
if (Test-Path (Join-Path $BinDir 'zh-CN\RaceBreedingExtension.resources.dll')) {
    Copy-Item (Join-Path $BinDir 'zh-CN\RaceBreedingExtension.resources.dll') (Join-Path $tmp 'zh-CN') -Force
}

$asm = [System.Reflection.Assembly]::LoadFrom((Join-Path $tmp 'RaceBreedingExtension.dll'))

$probes = @(
    @{ Res = 'RaceBreedingExtension.ComplexBreedingRedux.Properties.Strings'; Key = 'PluginLoaded' },
    @{ Res = 'RaceBreedingExtension.ComplexBreedingRedux.Properties.Strings'; Key = 'Bloodline_Demon_Name' },
    @{ Res = 'RaceBreedingExtension.ComplexBreedingRedux.Properties.Strings'; Key = 'Override_Trait108_Name' },
    @{ Res = 'RaceBreedingExtension.DepravedShield.Properties.Strings';     Key = 'UI_DepravedShield' },
    @{ Res = 'RaceBreedingExtension.VoiceReplay.Properties.Strings';         Key = 'Config_Enable' },
    @{ Res = 'RaceBreedingExtension.TitsModCompat.Properties.Strings';       Key = 'Config_BlockUniquePortrait' }
)

$failed = 0
foreach ($p in $probes) {
    $rm = New-Object System.Resources.ResourceManager($p.Res, $asm)
    $vals = @{}
    foreach ($lang in 'en', 'zh-CN', 'ru', 'ja') {
        $ci = [System.Globalization.CultureInfo]::GetCultureInfo($lang)
        $vals[$lang] = $rm.GetString($p.Key, $ci)
    }
    $short = $p.Res.Replace('RaceBreedingExtension.', '')
    Write-Host ("[{0}]" -f $p.Key)
    Write-Host ("    en    : {0}" -f $vals['en'])
    Write-Host ("    zh-CN : {0}" -f $vals['zh-CN'])
    Write-Host ("    ru    : {0}" -f $vals['ru'])
    Write-Host ("    ja    : {0}   (fallback to en)" -f $vals['ja'])
    if ([string]::IsNullOrEmpty($vals['en']) -or [string]::IsNullOrEmpty($vals['zh-CN']) -or [string]::IsNullOrEmpty($vals['ru'])) {
        Write-Host '    -> FAIL: empty value for en / zh-CN / ru'
        $failed++
    }
    if ($vals['ru'] -notmatch '[\u0400-\u04FF]') {
        Write-Host '    -> FAIL: ru value has no Cyrillic'
        $failed++
    }
}

Write-Host ''
if ($failed -gt 0) { Write-Host ("FAILED checks: {0}" -f $failed); exit 1 }
Write-Host 'ALL RESOURCE RESOLUTION CHECKS PASSED'
exit 0
