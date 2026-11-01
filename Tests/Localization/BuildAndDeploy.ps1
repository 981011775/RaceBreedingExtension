#Requires -Version 5.1
<#
  BuildAndDeploy.ps1
  用途：Rebuild 本模组并把主 DLL 与本地化卫星程序集（ru / zh-CN）部署到游戏 Mods 目录。

  背景：若曾经用 Assembly.LoadFrom 在当前 PowerShell 会话里加载过本 DLL，
        bin 下的文件会被该进程锁定导致 Build 的 CopyLocal 步骤失败（MSB3021/MSB3027）。
        此时可以用 -KillPid <该 PowerShell 的 PID> 让本脚本来释放锁（脚本本身运行在独立进程中）。

  用法：
    powershell -NoProfile -ExecutionPolicy Bypass -File Tests\Localization\BuildAndDeploy.ps1
    powershell -NoProfile -ExecutionPolicy Bypass -File Tests\Localization\BuildAndDeploy.ps1 -KillPid 1234
#>
[CmdletBinding()]
param(
    [string]$Root,
    [string]$GameMods = 'D:\Game\MonsterBlackMarket\Mods',
    [int]$KillPid = 0,
    [string]$Stamp = 'backup-loc',
    [string]$OutputDir = ''
)

$ErrorActionPreference = 'Stop'
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if ([string]::IsNullOrWhiteSpace($Root)) { $Root = (Resolve-Path (Join-Path $scriptDir '..\..')).Path }
$bin = Join-Path $Root 'bin\Debug\net481'

if ($KillPid -gt 0) {
    Write-Host ("releasing lock: Stop-Process -Id {0}" -f $KillPid)
    Stop-Process -Id $KillPid -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
}

Write-Host '--- dotnet build -t:Rebuild ---'
$buildArgs = @('build', '-t:Rebuild', '-nodeReuse:false', '-p:UseSharedCompilation=false')
if (-not [string]::IsNullOrWhiteSpace($OutputDir)) {
    New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
    $buildArgs += ('-p:OutputPath=' + (Resolve-Path $OutputDir).Path + '\')
    $bin = (Resolve-Path $OutputDir).Path
}
Write-Host ('dotnet ' + ($buildArgs -join ' '))
Push-Location $Root
try {
    $build = & dotnet @buildArgs 2>&1
} finally {
    Pop-Location
}
$buildText = ($build | Out-String)
$errors = [regex]::Matches($buildText, 'error [A-Z]+\d+')
$buildText.Split("`n") | Where-Object { $_ -match '个错误|个警告|已成功|Build succeeded|error [A-Z]+\d+' } | ForEach-Object { Write-Host $_.Trim() }
if ($errors.Count -gt 0) {
    Write-Host 'BUILD FAILED - deploy skipped'
    exit 1
}

$targets = @(
    @{ Src = (Join-Path $bin 'RaceBreedingExtension.dll'); Dst = (Join-Path $GameMods 'RaceBreedingExtension.dll') },
    @{ Src = (Join-Path $bin 'ru\RaceBreedingExtension.resources.dll'); Dst = (Join-Path $GameMods 'ru\RaceBreedingExtension.resources.dll') },
    @{ Src = (Join-Path $bin 'zh-CN\RaceBreedingExtension.resources.dll'); Dst = (Join-Path $GameMods 'zh-CN\RaceBreedingExtension.resources.dll') }
)

foreach ($t in $targets) {
    if (-not (Test-Path -LiteralPath $t.Src)) { Write-Host ("MISSING build output: {0}" -f $t.Src); exit 1 }
    if (Test-Path -LiteralPath $t.Dst) { Copy-Item -LiteralPath $t.Dst -Destination ($t.Dst + '.' + $Stamp) -Force }
    Copy-Item -LiteralPath $t.Src -Destination $t.Dst -Force
    $h1 = (Get-FileHash -LiteralPath $t.Src -Algorithm SHA256).Hash
    $h2 = (Get-FileHash -LiteralPath $t.Dst -Algorithm SHA256).Hash
    Write-Host ("{0}  len={1}  sha256={2}  match={3}" -f $t.Dst, (Get-Item -LiteralPath $t.Dst).Length, $h2.Substring(0, 16), ($h1 -eq $h2))
}

Write-Host 'DEPLOY OK'

# 若使用了替代输出目录，尽力回填标准 bin 目录（被占用时忽略失败）
if (-not [string]::IsNullOrWhiteSpace($OutputDir)) {
    $canonical = Join-Path $Root 'bin\Debug\net481'
    foreach ($rel in 'RaceBreedingExtension.dll', 'ru\RaceBreedingExtension.resources.dll', 'zh-CN\RaceBreedingExtension.resources.dll') {
        $src = Join-Path $bin $rel
        $dst = Join-Path $canonical $rel
        if (-not (Test-Path -LiteralPath $src)) { continue }
        $dstDir = Split-Path -Parent $dst
        if (-not (Test-Path -LiteralPath $dstDir)) { New-Item -ItemType Directory -Force -Path $dstDir | Out-Null }
        try {
            Copy-Item -LiteralPath $src -Destination $dst -Force -ErrorAction Stop
            Write-Host ("bin refresh OK : {0}" -f $rel)
        } catch {
            Write-Host ("bin refresh SKIP (locked): {0}" -f $rel)
        }
    }
}
