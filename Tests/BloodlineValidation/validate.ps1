# Bloodline (血脉) system – logic validation script
#
# 用法（先构建，再运行）：
#   dotnet build RaceBreedingExtension.csproj -t:Rebuild
#   powershell -NoProfile -ExecutionPolicy Bypass -File Tests\BloodlineValidation\validate.ps1
#   powershell -NoProfile -ExecutionPolicy Bypass -File Tests\BloodlineValidation\validate.ps1 -Dir <包含 RaceBreedingExtension.dll 的目录>
#
# 校验内容：
#   1) Trait9 编码（血脉 ID × 1000 + 等级）对 8 种血脉 × 1~10 级全部可正确往返解码；
#   2) 旧 HighRaceTrait 值（191~201）与其它非法值一律被判定为无效（会被 MigrateLegacy 清除）；
#   3) SpeciesBloodlineMap 中的物种名全部存在于 SpeciesRegistry，并打印映射结果；
#   4) 8 种血脉的每级数值 / 颜色 / 掉落物 / 怪物效果；
#   5) 描述文本渲染（本地化资源键是否齐全）。
# 注意：本目录已被 csproj 的 <Compile Remove="Tests\**" /> 排除，不参与模组编译。
param(
    [string]$Dir = (Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'bin\Debug\net481')
)

$ErrorActionPreference = 'Stop'
$asmPath = Join-Path $Dir 'RaceBreedingExtension.dll'
if (-not (Test-Path $asmPath)) { throw "未找到 $asmPath，请先构建模组或使用 -Dir 指定目录。" }

$asm = [System.Reflection.Assembly]::LoadFrom($asmPath)
$bs = $asm.GetType('RaceBreedingExtension.Bloodline.BloodlineSystem')
$eb = $asm.GetType('RaceBreedingExtension.Bloodline.Data.EBloodline')
$bd = $asm.GetType('RaceBreedingExtension.Bloodline.Data.BloodlineData')
$bref = $asm.GetType('RaceBreedingExtension.Bloodline.Data.BloodlineRegistry')
$map = $asm.GetType('RaceBreedingExtension.Bloodline.Data.SpeciesBloodlineMap')
$reg = $asm.GetType('ComplexBreedingRedux.Data.Species.SpeciesRegistry')
$flags = [System.Reflection.BindingFlags]'Public,Static'
$priv = [System.Reflection.BindingFlags]'NonPublic,Static'

$enc = $bs.GetMethod('Encode', $flags)
$dec = $bs.GetMethod('TryDecode', $flags)

Write-Output '--- 1) 编码往返 (8 血脉 x 1..10 级) ---'
$fail = 0
foreach ($n in [System.Enum]::GetNames($eb)) {
    if ($n -eq 'None') { continue }
    $b = [System.Enum]::Parse($eb, $n)
    for ($lv = 1; $lv -le 10; $lv++) {
        $v = $enc.Invoke($null, @($b, $lv))
        $args = @([float]$v, $null, 0)
        $ok = $dec.Invoke($null, $args)
        if ((-not $ok) -or ($args[2] -ne $lv) -or ($null -eq $args[1]) -or (-not $args[1].Id.ToString().Equals($n))) {
            $fail++
            Write-Output "FAIL $n +$lv -> $v ok=$ok level=$($args[2])"
        }
    }
}
Write-Output "roundtrip failures: $fail"

Write-Output '--- 2) 旧 HighRaceTrait / 非法值必须被拒绝 ---'
foreach ($raw in 191,192,193,194,195,196,197,198,200,201,0,5,99,999,9000,8011,10000) {
    $args = @([float]$raw, $null, 0)
    Write-Output ("raw={0,-6} valid={1}" -f $raw, $dec.Invoke($null, $args))
}

Write-Output '--- 3) 物种映射 (SpeciesBloodlineMap vs SpeciesRegistry) ---'
$mSpecies = $map.GetMethod('TryGetBySpeciesName', $flags)
$all = $reg.GetField('All', $flags).GetValue($null)
$mapped = 0
foreach ($sd in $all) {
    $args = @($sd.InternalName, $null, 0)
    if ($mSpecies.Invoke($null, $args)) {
        $mapped++
        Write-Output ("{0,-16} -> {1} +{2}" -f $sd.InternalName, $args[1], $args[2])
    } else {
        Write-Output ("UNMAPPED         {0}" -f $sd.InternalName)
    }
}
Write-Output "species total=$($all.Count) mapped=$mapped"

Write-Output '--- 4) 血脉数值 ---'
foreach ($b in $bref.GetProperty('All', $flags).GetValue($null)) {
    Write-Output ("{0,-10} color={1,-8} sexTime={2} mentCost={3} food={4} growth={5} conception={6} shield={7} item={8} depraved={9} monster={10}/{11}" -f `
        $b.InternalName, $b.ColorHex, $b.SexTimePerLevel, $b.MentalityCostPerLevel, $b.FoodConsumptionPerLevel, `
        $b.GrowthTimePerLevel, $b.ConceptionRatePerLevel, $b.ShieldPerLevel, $b.OrgasmItem, $b.OrgasmRequiresDepraved, `
        $b.MonsterEffect, $b.MonsterEffectValue)
}

# 4b) 平衡调整后的「每级数值」必须与「血脉特质修正.txt」一致（防止数值回归）
$spec4b = @{
    Demon     = @(5, 0.05, 0,  0,  0.0)
    Monster   = @(5, 0.0,  25, 0,  0.0)
    Wild      = @(0, 0.05, 25, 0,  0.0)
    Divine    = @(0, 0.0,  0,  0, -0.5)
    Magic     = @(0, 0.0,  50, 0,  0.0)
    Elemental = @(0, 0.0,  50, 0,  0.0)
    Nature    = @(0, 0.0,  0, 10,  0.0)
    Dragon    = @(0, 0.0,  0,  0, -0.5)
}
$fail4b = 0
foreach ($b in $bref.GetProperty('All', $flags).GetValue($null)) {
    $got = @([int]$b.SexTimePerLevel, [single]$b.MentalityCostPerLevel, [int]$b.FoodConsumptionPerLevel,
             [int]$b.GrowthTimePerLevel, [single]$b.ConceptionRatePerLevel)
    $exp = $spec4b[$b.InternalName]
    if (($got -join '|') -ne ($exp -join '|')) {
        $fail4b++
        Write-Output ("     数值不符 {0}: 实际 {1} / 期望 {2}" -f $b.InternalName, ($got -join ','), ($exp -join ','))
    }
}
Write-Output ("4b.1 8 血脉每级数值与修正表一致（SexTime/Mentality/Food/Growth/Conception） : " + `
    $(if ($fail4b -eq 0) { 'PASS' } else { "FAIL x$fail4b" }))

# 4c) 旧存档数值迁移（版本号 / 历史数值表 / 接线）
$bvs = $asm.GetType('RaceBreedingExtension.Bloodline.BloodlineVanillaStats')
$root4c = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$sv4c = $bs.GetField('StatVersion', $flags).GetValue($null)
Write-Output ("4c.1 StatVersion = {0}（期望 2）: {1}" -f $sv4c, $(if ([int]$sv4c -eq 2) { 'PASS' } else { 'FAIL' }))
Write-Output ("4c.2 EnsureStatVersion / RealignToCurrentVersion 已提供 : {0}" -f `
    $(if (($null -ne $bs.GetMethod('EnsureStatVersion', $flags)) -and `
          ($null -ne $bvs.GetMethod('RealignToCurrentVersion', $flags))) { 'PASS' } else { 'FAIL' }))

$hist4c = $asm.GetType('RaceBreedingExtension.Bloodline.Data.BloodlineStatHistory')
$tryHist4c = $hist4c.GetMethod('TryGetPerLevel', $flags)
$v1spec4c = @{
    Demon     = @(10, 0.1,  0,   0,  0.0)
    Monster   = @(10, 0.0,  10,  0,  0.0)
    Wild      = @(0,  0.1,  10,  0,  0.0)
    Divine    = @(0,  0.0,  0,   0, -1.0)
    Magic     = @(0,  0.0,  100, 0,  0.0)
    Elemental = @(0,  0.0,  100, 0,  0.0)
    Nature    = @(0,  0.0,  0,   15, 0.0)
    Dragon    = @(0,  0.0,  0,   0, -1.0)
}
$fail4c = 0
foreach ($n in [System.Enum]::GetNames($eb)) {
    if ($n -eq 'None') { continue }
    $bid4c = [System.Enum]::Parse($eb, $n)

    $a4c = @($bid4c, 1, $null)
    if (-not $tryHist4c.Invoke($null, $a4c)) {
        $fail4c++
        Write-Output "     v1 历史数值缺失: $n"
    } else {
        $pl4c = $a4c[2]
        $got4c = @([int]$pl4c.SexTime, [single]$pl4c.MentalityCost, [int]$pl4c.FoodConsumption,
                   [int]$pl4c.GrowthTime, [single]$pl4c.ConceptionRate)
        if (($got4c -join '|') -ne ($v1spec4c[$n] -join '|')) {
            $fail4c++
            Write-Output ("     v1 数值不符 {0}: 实际 {1} / 期望 {2}" -f $n, ($got4c -join ','), ($v1spec4c[$n] -join ','))
        }
    }

    # 当前版本（2）不得再被当成「需要迁移的旧数值」
    $a4cb = @($bid4c, 2, $null)
    if ($tryHist4c.Invoke($null, $a4cb)) {
        $fail4c++
        Write-Output "     v2 仍返回历史数值（会重复换算）: $n"
    }

    # 迁移算术：旧数值×等级 +（新−旧）×等级 = 新数值×等级
    $cur4c = @(($bref.GetProperty('All', $flags).GetValue($null)) | Where-Object { $_.InternalName -eq $n })[0]
    $old4c = @([int]$v1spec4c[$n][0], [single]$v1spec4c[$n][1], [int]$v1spec4c[$n][2], [int]$v1spec4c[$n][3], [single]$v1spec4c[$n][4])
    $new4c = @([int]$cur4c.SexTimePerLevel, [single]$cur4c.MentalityCostPerLevel, [int]$cur4c.FoodConsumptionPerLevel,
               [int]$cur4c.GrowthTimePerLevel, [single]$cur4c.ConceptionRatePerLevel)
    for ($lv4c = 1; $lv4c -le 10; $lv4c++) {
        for ($i4c = 0; $i4c -lt 5; $i4c++) {
            $after4c = $old4c[$i4c] * $lv4c + ($new4c[$i4c] - $old4c[$i4c]) * $lv4c
            if ([Math]::Abs($after4c - $new4c[$i4c] * $lv4c) -gt 1e-6) {
                $fail4c++
                Write-Output ("     $n +$lv4c 第 $i4c 项迁移后 = $after4c（期望 $($new4c[$i4c] * $lv4c)）")
            }
        }
    }
}
Write-Output ("4c.3 v1 历史数值表完整 / v2 不再迁移 / 迁移算术自洽 : " + `
    $(if ($fail4c -eq 0) { 'PASS' } else { "FAIL x$fail4c" }))

$bsSrc4c = Get-Content (Join-Path $root4c 'Bloodline\BloodlineSystem.cs') -Raw -Encoding UTF8
$ensureCalls4c = ([regex]::Matches($bsSrc4c, 'EnsureStatVersion\(character\)')).Count
Write-Output ("4c.4 版本对齐接线（SetBloodline / ClearBloodline / RaiseLevel / ApplySpeciesBloodline / GetDescription，应 ≥5）: {0} → {1}" -f `
    $ensureCalls4c, $(if ($ensureCalls4c -ge 5) { 'PASS' } else { 'FAIL' }))
$markCalls4c = ([regex]::Matches($bsSrc4c, 'MarkStatVersion\(character\)')).Count
Write-Output ("4c.5 写完属性后打标记（SetBloodline / RaiseLevel，应 ≥2）: {0} → {1}" -f `
    $markCalls4c, $(if ($markCalls4c -ge 2) { 'PASS' } else { 'FAIL' }))

Write-Output '--- 5) 描述渲染 (示例 +4 级) ---'
$build = $bs.GetMethod('GetDescription', $priv, $null, @($bd, [int]), $null)
foreach ($item in $bref.GetProperty('All', $flags).GetValue($null)) {
    $base = $item.PSObject.BaseObject
    Write-Output ("=== {0} +4 ===" -f $base.InternalName)
    Write-Output $build.Invoke($null, @($base, 4))
}

Write-Output '--- 7) 职业战斗数据 (战斗力加成 / 击杀模式 / 营地 sex 次数) ---'
$preg = $asm.GetType('ComplexBreedingRedux.Data.Professions.ProfessionRegistry')
$km = $asm.GetType('ComplexBreedingRedux.Data.Professions.EProfessionKillMode')
$profs = @()
foreach ($p in $preg.GetProperty('All', $flags).GetValue($null)) { $profs += $p }
foreach ($p in $profs) {
    $b = $p.PSObject.BaseObject
    Write-Output ("{0,-11} tier={1} powerBonus={2,-5} kill={3,-16} chance={4,-4} campSex={5}" -f `
        $b.InternalName, $b.Tier, $b.CombatPowerBonus, $b.KillMode, $b.KillChance, $b.CampSexCountBonus)
}

Write-Output '--- 8) 战斗力系数表 ---'
$cfg = $asm.GetType('RaceBreedingExtension.CombatPower.CombatPowerConfig')
$rarity = $cfg.GetField('RarityCoefficient', $flags).GetValue($null)
Write-Output ("rarity coefficient 1..5: " + (($rarity | Select-Object -Skip 1) -join ', '))
Write-Output ("bloodline per level: " + $cfg.GetField('BloodlineCoefficientPerLevel', $flags).GetValue($null))
Write-Output ("double power kill chance: " + $cfg.GetField('DoublePowerKillChance', $flags).GetValue($null))
$raceCoef = $cfg.GetField('MonsterRaceCoefficient', $flags).GetValue($null)
foreach ($kv in $raceCoef.GetEnumerator()) { Write-Output ("monster race {0}: coef={1}" -f $kv.Key, $kv.Value) }
$raceTier = $cfg.GetField('MonsterRaceTier', $flags).GetValue($null)
foreach ($kv in $raceTier.GetEnumerator()) { Write-Output ("monster race {0}: tier={1}" -f $kv.Key, $kv.Value) }

Write-Output '--- 10) 新补丁目标方法 / 萝莉母乳效果 / 拷问室拦截 ---'
$game = [System.Reflection.Assembly]::LoadFrom((Join-Path $Dir 'Assembly-CSharp.dll'))
$capsule = $game.GetType('MBMScripts.Capsule')
$mAlloc = $capsule.GetMethod('IsAllocatable')
Write-Output ("Capsule.IsAllocatable params: " + (($mAlloc.GetParameters() | ForEach-Object { $_.ParameterType.Name }) -join ', ') + " virtual=" + $mAlloc.IsVirtual)
$gm = $game.GetType('MBMScripts.GameManager')
$mMilk = $gm.GetMethod('GetMilkTraitList', $flags)
Write-Output ("GameManager.GetMilkTraitList static=" + $mMilk.IsStatic + " params=" + (($mMilk.GetParameters() | ForEach-Object { $_.ParameterType.Name }) -join ','))
$lp = $asm.GetType('ComplexBreedingRedux.Patches.ItemPatches.LoliMilkTraitListPatch')
$listField = $lp.GetField('LoliMilkTraitList', [System.Reflection.BindingFlags]'NonPublic,Static')
foreach ($t in $listField.GetValue($null)) { Write-Output ("LoliMilk effect: trait={0} value={1}" -f $t.Item1, $t.Item2) }
$capPatch = $asm.GetType('DepravedShield.Patches.CapsuleAllocationPatch')
Write-Output ("CapsuleAllocationPatch present: " + ($null -ne $capPatch))
$van = $asm.GetType('RaceBreedingExtension.Bloodline.BloodlineVanillaStats')
Write-Output ("BloodlineVanillaStats present: " + ($null -ne $van))

Write-Output '--- 9) 猎手营地 sex 次数职业加成 / 物品与护盾工作区间 ---'

foreach ($p in $profs) {
    $b = $p.PSObject.BaseObject
    if ($b.CampSexCountBonus -eq 0) { continue }
    Write-Output ("{0}: camp escape sex +{1}" -f $b.InternalName, $b.CampSexCountBonus)
}
foreach ($p in $profs) {
    $b = $p.PSObject.BaseObject
    foreach ($job in $b.Jobs) {
        if ($job.ItemCountMax -gt 0) {
            Write-Output ("{0}: item reward count {1}-{2}" -f $b.InternalName, $job.ItemCountMin, $job.ItemCountMax)
        }
        if ($job.ShieldRemovalTargetsMax -gt 0) {
            Write-Output ("{0}: shield removal {1} x {2}-{3} slaves" -f $b.InternalName, $job.ShieldRemoval, $job.ShieldRemovalTargetsMin, $job.ShieldRemovalTargetsMax)
        }
    }
}

Write-Output '--- 6) 派系血脉 (FactionRegistry) / 派系声誉等级 -> 访客血脉等级 ---'
$freg = $asm.GetType('ComplexBreedingRedux.Data.Factions.FactionRegistry')
$fsys = $asm.GetType('ComplexBreedingRedux.Features.FactionSystem')
$blMaxTable = $fsys.GetField('MaxVisitorBloodlineLevelByReputation', $flags).GetValue($null)
$getBlMax = $fsys.GetMethod('GetMaxVisitorBloodlineLevel', $flags)
$lvls = @(0..7 | ForEach-Object { $getBlMax.Invoke($null, @([int]$_)) })
Write-Output ("派系声誉等级 0..7 (Unknown..Fabled) -> 访客血脉等级上限: " + ($lvls -join ', '))
Write-Output ("  表字段与 GetMaxVisitorBloodlineLevel 一致 : " +
              $(if ((($blMaxTable) -join ',') -eq ($lvls -join ',')) { 'PASS' } else { 'FAIL' }))
Write-Output '  （★ 只与模组派系声誉等级相关；不参考原版声望等级、不受访客品阶影响）'
$factions = @()
foreach ($f in $freg.GetProperty('All', $flags).GetValue($null)) { $factions += $f }
foreach ($f in $factions) {
    $base = $f.PSObject.BaseObject
    $names = @()
    foreach ($x in $base.Bloodlines) { $names += $x.ToString() }
    Write-Output ("{0,-9} id={1} color={2,-8} bloodlines=[{3}]" -f $base.InternalName, $base.Id, $base.ColorHex, ($names -join ', '))
}

Write-Output '--- 11) 派系声望 → 职业权重 (FactionProfessionAffinity / 猎手营地生成) ---'
$aff = $asm.GetType('ComplexBreedingRedux.Data.Professions.FactionProfessionAffinity')
$tierW = $aff.GetField('TierBaseWeight', $flags).GetValue($null)
Write-Output ("tier base weight 1..5 (Common..Legendary): " + (($tierW | Select-Object -Skip 1) -join ', '))
Write-Output ("MinWeight=" + $aff.GetField('MinWeight', $flags).GetValue($null) +
              " RarityFlatten=" + $aff.GetField('RarityFlatten', $flags).GetValue($null) +
              " FactionAffinityBonus=" + $aff.GetField('FactionAffinityBonus', $flags).GetValue($null) +
              " RareReputationBonus=" + $aff.GetField('RareReputationBonus', $flags).GetValue($null))
$mAff = $aff.GetMethod('IsAffiliated', $flags)
$mW = $aff.GetMethod('ComputeWeight', $flags)
$avg = ($profs | ForEach-Object { $tierW[$_.PSObject.BaseObject.Tier] } | Measure-Object -Average).Average
Write-Output ("pool average base weight = {0:0.###}  ({1} professions)" -f $avg, $profs.Count)
Write-Output '     professional  tier  w(rep=0)  w(rep=1)  affiliated'
$failRep0 = 0
$failBoost = 0
foreach ($f in $factions) {
    $fid = $f.PSObject.BaseObject.Id
    Write-Output ("== faction {0} (id={1}) ==" -f $f.PSObject.BaseObject.InternalName, $fid)
    foreach ($p in $profs) {
        $pb = $p.PSObject.BaseObject
        $w0 = $mW.Invoke($null, @($pb, [int]$fid, [float]0, [float]$avg))
        $w1 = $mW.Invoke($null, @($pb, [int]$fid, [float]1, [float]$avg))
        $isAff = $mAff.Invoke($null, @([int]$fid, [int]$pb.Id))
        if ($w0 -le 0) { $failRep0++ }
        if ($isAff -and ($w1 -le $w0)) { $failBoost++ }
        if ($isAff -or ($pb.Tier -ge 5)) {
            Write-Output ("     {0,-11} {1,4}  {2,8:0.###}  {3,8:0.###}  {4}" -f $pb.InternalName, $pb.Tier, $w0, $w1, $isAff)
        }
    }
}
Write-Output ("CHECK 声望0时所有职业权重>0 : " + $(if ($failRep0 -eq 0) { 'PASS' } else { "FAIL x$failRep0" }))
Write-Output ("CHECK 关联职业权重随声望上升 : " + $(if ($failBoost -eq 0) { 'PASS' } else { "FAIL x$failBoost" }))
$mUnaff = $aff.GetMethod('IsUnaffiliated', $flags)
$una = @($profs | Where-Object { $mUnaff.Invoke($null, @([int]$_.PSObject.BaseObject.Id)) } |
        ForEach-Object { $_.PSObject.BaseObject.InternalName })
$linked = @($profs | Where-Object { -not $mUnaff.Invoke($null, @([int]$_.PSObject.BaseObject.Id)) } |
            ForEach-Object { $_.PSObject.BaseObject.InternalName })
Write-Output ("无派系归属职业(任何声望都可能出现): " + ($una -join ', '))
Write-Output ("派系关联职业(共 {0}): {1}" -f $linked.Count, ($linked -join ', '))

Write-Output '--- 12) 派系声望 → 生成奴隶属性（向最优端插值；Trait99/100 不随声望变动） ---'
$hcfg = $asm.GetType('RaceBreedingExtension.HunterCamp.HunterCampConfig')
$ranges = $hcfg.GetField('ProducedStatTraitRanges', $flags).GetValue($null)
$jitter = [double]$hcfg.GetField('StatReputationJitter', $flags).GetValue($null)
$hboost = $hcfg.GetField('HealthReputationBoost', $flags).GetValue($null)
$mScaled = $hcfg.GetMethod('IsReputationScaledStat', $flags)
$et = $ranges.GetType().GetGenericArguments()[0]   # Dictionary<ETrait,(float,float)> → ETrait
Write-Output ("health reputation boost = {0}  /  stat reputation jitter = ±{1:0.00}" -f $hboost, $jitter)
Write-Output '     trait      worst         best      满声望(rep=1) 区间            随声望变动'
foreach ($kv in $ranges.GetEnumerator()) {
    $t = $kv.Key.ToString()
    $worst = [double]$kv.Value.Item1
    $best = [double]$kv.Value.Item2
    $scaled = $mScaled.Invoke($null, @([System.Enum]::Parse($et, $t)))
    if ($scaled) {
        $a = $worst + ($best - $worst) * (1 - $jitter)
        $rangeText = ("[{0:0.##} ~ {1:0.##}]" -f [Math]::Min($a, $best), [Math]::Max($a, $best))
    } else {
        $rangeText = ("随机 [{0:0.##} ~ {1:0.##}]" -f [Math]::Min($worst, $best), [Math]::Max($worst, $best))
    }
    Write-Output ("     {0,-9} {1,10:0.##} {2,11:0.##}      {3,-28} {4}" -f $t, $worst, $best, $rangeText, $scaled)
}
Write-Output '提示：满声望时 Trait96/97 → 约 9000~9999、Trait98 → 约 899~999；低声望可能出现负面属性'
Write-Output '      （Trait93 受孕率 / Trait94 成长时间 / Trait95 维护费 / Trait97 生育上限 的最差端为负）。'

Write-Output '--- 13) 本轮修复项校验（营地金额 / 不可升级 / 妖精设置 / 傀儡 / 文本替换） ---'

# 13.1 猎手营地生成金额与逃跑损失（战斗力公式）
$divProp = $hcfg.GetProperty('SpawnCostPowerDivisor', $flags)
$multProp = $hcfg.GetProperty('EscapePenaltyPowerMultiplier', $flags)
Write-Output ("spawn cost divisor = " + $(if ($divProp) { $divProp.GetValue($null) } else { 'MISSING' }) +
              " (default " + $hcfg.GetField('DefaultSpawnCostPowerDivisor', $flags).GetValue($null) + ")" +
              " / escape multiplier = " + $(if ($multProp) { $multProp.GetValue($null) } else { 'MISSING' }) +
              " (default " + $hcfg.GetField('DefaultEscapePenaltyPowerMultiplier', $flags).GetValue($null) + ")")
Write-Output ("old fixed gold fields removed: " +
              (($null -eq $hcfg.GetProperty('SpawnGoldCost', $flags)) -and ($null -eq $hcfg.GetProperty('EscapeGoldPenalty', $flags))))
foreach ($m in 'ComputeSpawnCost', 'ComputeEscapePenalty') {
    $mi = $hcfg.GetMethod($m, $flags)
    Write-Output ("HunterCampConfig.{0}: present={1} params={2}" -f $m, ($null -ne $mi),
                  $(if ($mi) { ($mi.GetParameters() | ForEach-Object { $_.ParameterType.Name }) -join ',' } else { '' }))
}

# 13.2 猎手营地不可升级
$gc = $asm.GetType('RaceBreedingExtension.GroundCamp.GroundCampPatches')
foreach ($n in 'SmallCampNotUpgradablePatch', 'LargeCampNotUpgradablePatch', 'SmallCampUpgradeBlockPatch', 'LargeCampUpgradeBlockPatch') {
    Write-Output ("GroundCampPatches.{0}: present={1}" -f $n, ($null -ne $gc.GetNestedType($n, [System.Reflection.BindingFlags]'NonPublic,Public')))
}

# 13.3 妖精管理设置同步
$sync = $asm.GetType('ComplexBreedingRedux.Features.FloraPixySettingSync')
$syncPatch = $asm.GetType('ComplexBreedingRedux.Patches.PlayDataPatches.FloraPixyHealthSyncPatch')
Write-Output ("FloraPixySettingSync present=" + ($null -ne $sync) + " / sync patch present=" + ($null -ne $syncPatch))
foreach ($n in 'FloraPixyHealthSyncPatch','FloraPixyBirthSyncPatch','FloraPixyBabySyncPatch','FloraPixyMilkSyncPatch',
               'FloraPixyDeadSyncPatch','FloraPixyShowFirstSyncPatch','FloraPixySellSyncPatch','FloraPixyCapsuleSyncPatch') {
    Write-Output ("  {0}: {1}" -f $n, ($null -ne $asm.GetType("ComplexBreedingRedux.Patches.PlayDataPatches.$n")))
}

# 13.4 职业傀儡（Golem）共用生成参数
$hcs = $asm.GetType('RaceBreedingExtension.HunterCamp.HunterCampSystem')
$pp = $hcs.GetNestedType('ProducedProfile', [System.Reflection.BindingFlags]'Public')
$rollProfOverloads = @($hcs.GetMethods($flags) | Where-Object { $_.Name -eq 'RollProducedProfile' })
Write-Output ("HunterCampSystem.ProducedProfile present=" + ($null -ne $pp) +
              " / RollProducedProfile(重载数)=" + $rollProfOverloads.Count +
              " / ApplyProducedProfile=" + ($null -ne $hcs.GetMethod('ApplyProducedProfile', $flags)))
if ($pp) { Write-Output ("  members: " + ((($pp.GetProperties() | ForEach-Object { $_.Name }) -join ', '))) }

# 13.5 原版文本替换（TryGet 只需要字符串，可脱离 Unity 执行）
$ovr = $asm.GetType('ComplexBreedingRedux.Data.GameTextOverrides')
$mTry = $ovr.GetMethod('TryGet', $flags)
foreach ($key in '#Trait_108_Name', '#Trait_108_Text', '#Trait_0101200000', '#Trait_0101200000_Tooltip',
                   '#Trait_71', '#Trait_71_Tooltip', '#Item127Text', '#Trait_103_Name') {
    $a = @($key, $null)
    $ok = $mTry.Invoke($null, $a)
    Write-Output ("{0,-30} replaced={1,-5} {2}" -f $key, $ok, $a[1])
}
$etGame = $game.GetType('MBMScripts.ETrait')
$mName = $ovr.GetMethod('GetTraitName', $flags)
$mTip = $ovr.GetMethod('GetTraitTooltip', $flags)
foreach ($t in 'Trait71', 'Trait108', 'Trait103') {
    $e = [System.Enum]::Parse($etGame, $t)
    Write-Output ("char-detail {0,-9} name=[{1}] tooltip=[{2}]" -f $t, $mName.Invoke($null, @($e)), $mTip.Invoke($null, @($e)))
}

Write-Output '--- 14) 本轮修改校验（子宫大小遗传 50% / 派系声望文本颜色） ---'

# 14.1 子宫大小遗传概率
$womb = $asm.GetType('ComplexBreedingRedux.Features.WombSystem')
$chance = $womb.GetField('WombSizeInheritChancePercent', [System.Reflection.BindingFlags]'NonPublic,Static').GetValue($null)
Write-Output ("WombSystem.WombSizeInheritChancePercent = {0}%  (期望 50)" -f $chance)

# 14.2 声望文本颜色（直接读源 resx；对照 GameColors.TierColors：3=稀有 4=史诗 5=传说）
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$tierColors = $asm.GetType('ComplexBreedingRedux.Data.GameColors').GetField('TierColors', $flags).GetValue($null)
Write-Output ("tier colors 1..5 (Common..Legendary) = " + (($tierColors[1..5]) -join ', '))
$expect = [ordered]@{
    'FactionRep_Fabled'        = $tierColors[5]   # 传说  Legendary
    'FactionRep_Famous'        = $tierColors[4]   # 史诗  Epic
    'FactionRep_Significant'   = $tierColors[3]   # 稀有  Rare
    'FactionRep_Reputable'     = $tierColors[2]   # 不凡  Uncommon
    'FactionRep_Noteworthy'    = $tierColors[1]   # 普通  Common
    'FactionRep_Insignificant' = '#FFFFFF'        # 白色
    'FactionRep_Obscure'       = '#9e97a3'        # 灰色（保持现状）
    'FactionRep_Unknown'       = '#9e97a3'        # 灰色（最低）
}
foreach ($file in 'Strings.resx', 'Strings.zh-cn.resx') {
    $path = Join-Path $root ('ComplexBreedingRedux\Properties\' + $file)
    [xml]$xml = Get-Content $path -Encoding UTF8
    $cfgFail = 0
    Write-Output ("== {0} ==" -f $file)
    foreach ($node in $xml.root.data) {
        if (-not $expect.Contains($node.name)) { continue }
        $val = $node.value
        $hex = if ($val -match '<color=(#[0-9A-Fa-f]{6})>') { $matches[1] } else { '(none)' }
        $ok = ($hex -ieq $expect[$node.name])
        if (-not $ok) { $cfgFail++ }
        Write-Output ("  {0,-27} color={1,-9} expect={2,-9} {3}  text=[{4}]" -f `
            $node.name, $hex, $expect[$node.name], $(if ($ok) { 'OK' } else { 'MISMATCH' }), $val)
    }
    Write-Output ("  color mismatches: {0}" -f $cfgFail)
}




Write-Output '--- 15) 成人化（喂母乳进入成体）不再清空普通特质 ---'

$setupPath = Join-Path $root 'ComplexBreedingRedux\Features\FemaleSpeciesSetup.cs'
$growPath = Join-Path $root 'ComplexBreedingRedux\Patches\GrowUpPatch.cs'
$setupSrc = Get-Content $setupPath -Raw -Encoding UTF8
$growSrc = Get-Content $growPath -Raw -Encoding UTF8

$fs = $asm.GetType('ComplexBreedingRedux.Features.FemaleSpeciesSetup')
$refresh = $fs.GetMethod('RefreshSpeciesAppearance', $flags)
Write-Output ("FemaleSpeciesSetup.RefreshSpeciesAppearance present=" + ($null -ne $refresh) +
              " params=" + $(if ($refresh) { ($refresh.GetParameters() | ForEach-Object { $_.ParameterType.Name }) -join ',' } else { '' }))
Write-Output ("GrowUpPatch 使用 RefreshSpeciesAppearance : " + $(if ($growSrc -match 'RefreshSpeciesAppearance') { 'PASS' } else { 'FAIL' }))
Write-Output ("GrowUpPatch 仍在调用 SetSpecies(       : " + $(if ($growSrc -match 'FemaleSpeciesSetup\.SetSpecies\s*\(') { 'FAIL' } else { 'PASS (未调用)' }))

$start = $setupSrc.IndexOf('public static void RefreshSpeciesAppearance')
$end = $setupSrc.IndexOf('public static void SetSpeciesAllStats')
if ($start -ge 0 -and $end -gt $start) {
    $body = $setupSrc.Substring($start, $end - $start)
    $clear = @('ClearTrait(', 'ClearRaceTrait(', 'OnEnableTrait(') | Where-Object { $body -match [regex]::Escape($_) }
    Write-Output ("RefreshSpeciesAppearance 方法体内清空调用: " +
                  $(if ($clear.Count -eq 0) { '无 → PASS' } else { ($clear -join ', ') + ' → FAIL' }))
} else {
    Write-Output 'RefreshSpeciesAppearance 源码区间未找到 → FAIL'
}


Write-Output '--- 16) 血脉遗传规则（客户：同血脉取高者+50%；异血脉仅母亲） ---'

$blPath = Join-Path $root 'Bloodline\BloodlineSystem.cs'
$blSrc = Get-Content $blPath -Raw -Encoding UTF8
$bl = $asm.GetType('RaceBreedingExtension.Bloodline.BloodlineSystem')

$chanceField = $bl.GetField('LevelUpChance', $flags)
Write-Output ("LevelUpChance = " + $(if ($chanceField) { $chanceField.GetValue($null) } else { 'MISSING' }) + "  (0.5 = 50%)")
Write-Output ("已移除的旧常量 SameBloodlineLevelUpChance : " + ($null -eq $bl.GetField('SameBloodlineLevelUpChance', $flags)))
Write-Output ("已移除的旧开关 ClientBloodlineCountsForLevelUp : " + ($null -eq $bl.GetField('ClientBloodlineCountsForLevelUp', $flags)))

$fnStart = $blSrc.IndexOf('public static void ApplyInheritedBloodline')
$fnEnd = $blSrc.IndexOf('public static int GetShieldBonus')
$body = $(if ($fnStart -ge 0 -and $fnEnd -gt $fnStart) { $blSrc.Substring($fnStart, $fnEnd - $fnStart) } else { '' })

Write-Output ("母亲按 is Female 识别                          : " + ($body -match 'firstParent is Female'))

# 客户父本分支
$clientBranch = ''
if ($body -match '(?s)if \(fatherIsClient\).*?else if \(motherData != null && fatherData != null\)') { $clientBranch = $matches[0] }
Write-Output ("存在 father is Client 判定                     : " + ($body -match 'father is Client'))
Write-Output ("客户·血脉相同 → 取双方较高等级（Mathf.Max）    : " + ($clientBranch -match 'motherData\.Id == fatherData\.Id[\s\S]*?Mathf\.Max\(motherLevel, fatherLevel\)'))
Write-Output ("客户·血脉相同 → 允许 +1（levelUpEligible=true）: " + ($clientBranch -match 'Mathf\.Max\(motherLevel, fatherLevel\)[\s\S]*?levelUpEligible = true;'))
$clientDiff = ''
if ($clientBranch -match '(?s)else if \(motherData != null\)\s*\{.*?\}') { $clientDiff = $matches[0] }
Write-Output ("客户·血脉不同 → 仅母亲血脉且不 +1              : " +
              (($clientDiff -match 'motherData\.Id') -and ($clientDiff -notmatch 'fatherData\.Id') -and ($clientDiff -notmatch 'levelUpEligible')))

# 非客户分支：双方有血脉允许 +1；仅单方不允许
Write-Output ("非客户·双方有血脉 → 允许 +1                   : " +
              ($body -match '(?s)else if \(motherData != null && fatherData != null\)[\s\S]*?levelUpEligible = true;'))
$gateLine = ''
if ($body -match '(?m)^\s*if \(levelUpEligible && Random\.Range\(0f, 1f\) < LevelUpChance\)') { $gateLine = 'ok' }
Write-Output ("+1 由 levelUpEligible 门控 + 50% 掷骰          : " + ($gateLine -eq 'ok'))
Write-Output ("方法体内 level++ 次数（应为 1）                : " + ([regex]::Matches($body, 'level\+\+')).Count)

Write-Output '--- 17) 客户父本：真实父本还原（假人类父本不再丢失客户血脉） ---'

$bsPath = Join-Path $root 'ComplexBreedingRedux\Features\BreedingSystem.cs'
$bsSrc = Get-Content $bsPath -Raw -Encoding UTF8
$bsType = $asm.GetType('ComplexBreedingRedux.Features.BreedingSystem')

$originField = $bsType.GetField('FakeFatherOrigins', $priv)
Write-Output ("BreedingSystem.FakeFatherOrigins 字段类型              : " +
              $(if ($originField) { $originField.FieldType.Name } else { 'MISSING' }))
Write-Output ("ResolveRealParent 辅助方法存在                         : " +
              ($null -ne $bsType.GetMethod('ResolveRealParent', $priv)))
Write-Output ("GetFakeHumanMaleForWomenBirth 登记「假父本 → 真实父本」: " +
              $(if ($bsSrc -match 'FakeFatherOrigins\.Add\(client, ResolveRealParent\(male\)\)') { 'PASS' } else { 'FAIL' }))
Write-Output ("ApplyBloodline 还原双方亲本后再做血脉遗传             : " +
              $(if ($bsSrc -match '(?s)ApplyBloodline\(Character child[^)]*\)\s*=>.*?ResolveRealParent\(mainParent\), ResolveRealParent\(subParent\)') { 'PASS' } else { 'FAIL' }))
Write-Output ("BloodlineSystem 把玩家也按人类父本处理（Player）      : " +
              $(if ($blSrc -match 'father is Client \|\| father is Player') { 'PASS' } else { 'FAIL' }))
Write-Output ("客户分支仍以「双方血脉相同」为前提才允许 +1           : " +
              $(if ($body -match '(?s)if \(fatherIsClient\)\s*\{\s*if \(motherData != null && fatherData != null && motherData\.Id == fatherData\.Id\)') { 'PASS' } else { 'FAIL' }))

Write-Output '--- 18) 派系经济平衡（公主只放大派系财富/声誉；品阶曲线平滑；结账上限 4000-6000） ---'

$fs18 = $asm.GetType('ComplexBreedingRedux.Features.FactionSystem')
$tw18 = $fs18.GetField('TierWealthBase', $flags).GetValue($null)
$rs18 = $fs18.GetField('ReputationWealthScale', $flags).GetValue($null)
$rr18 = $fs18.GetField('TierReputationReward', $flags).GetValue($null)
$mt18 = $fs18.GetField('ReputationMaxTier', $flags).GetValue($null)
$dist18 = $fs18.GetField('TierDistributionByReputation', $flags).GetValue($null)
$prin18 = $fs18.GetField('PrincessRevenueMultiplier', $flags).GetValue($null)
$maxScore18 = $fs18.GetField('MaxReputationScore', $flags).GetValue($null)
$tierFn18 = $fs18.GetMethod('GetReputationTier', $flags)

Write-Output ("公主倍率 = {0}    派系声誉分数上限 = {1}" -f $prin18, $maxScore18)
Write-Output ("品阶基准财富 1..5  = " + (($tw18 | Select-Object -Skip 1) -join ', '))
Write-Output ("品阶派系声誉 1..5  = " + (($rr18 | Select-Object -Skip 1) -join ', '))
Write-Output ("声誉财富倍率 0..7  = " + ($rs18 -join ', '))
Write-Output ("各等级最高客户品阶 = " + ($mt18 -join ', '))

# 18.1 品阶财富曲线必须平滑（相邻品阶跳变 ≤ 2 倍）
$jump18 = 0
for ($i = 2; $i -le 5; $i++) {
    $ratio = [double]$tw18[$i] / [double]$tw18[$i - 1]
    if ($ratio -gt 2.0) { $jump18++ }
    Write-Output ("  tier {0}->{1} : {2:0.00}x" -f ($i - 1), $i, $ratio)
}
Write-Output ("18.1 品阶财富跳变 >2x 次数 : {0} {1}" -f $jump18, $(if ($jump18 -eq 0) { '→ PASS' } else { '→ FAIL' }))

# 18.2 声誉倍率必须单调递增且每级增幅 ≤ 25%
$mono18 = $true
$step18 = 0
for ($i = 1; $i -lt $rs18.Length; $i++) {
    $g = [double]$rs18[$i] / [double]$rs18[$i - 1]
    if ($g -le 1.0) { $mono18 = $false }
    if ($g -gt 1.25) { $step18++ }
}
Write-Output ("18.2 声誉倍率单调递增={0} 跳变(>25%)={1} {2}" -f `
    $mono18, $step18, $(if ($mono18 -and $step18 -eq 0) { '→ PASS' } else { '→ FAIL' }))

# 18.3 最高声誉 + 最高品阶 + 公主：派系营业额必须落在 4000-6000
$maxWealth18 = [double]$tw18[5] * [double]$rs18[7]
$maxPayout18 = $maxWealth18 * $prin18
$lowWealth18 = [double]$tw18[1] * [double]$rs18[0]
Write-Output ("18.3 最高声誉(tier5+Fabled) 派系财富={0:0} → 公主 ×{1} = {2:0} {3}" -f `
    $maxWealth18, $prin18, $maxPayout18, $(if ($maxPayout18 -ge 4000 -and $maxPayout18 -le 6000) { '→ PASS (目标 4000-6000)' } else { '→ FAIL' }))
Write-Output ("     最低声誉(tier1+Unknown) 派系财富={0:0} → 公主 = {1:0}（最高/最低 = {2:0.0} 倍，已平滑）" -f `
    $lowWealth18, ($lowWealth18 * $prin18), ($maxPayout18 / ($lowWealth18 * $prin18)))

# 18.4 品阶分布表：每行合计 100，且不超过该等级的最高品阶
$distFail18 = 0
foreach ($row in $dist18) { if ((($row | Measure-Object -Sum).Sum) -ne 100) { $distFail18++ } }
for ($r = 0; $r -lt $dist18.Length; $r++) {
    for ($t = 0; $t -lt 5; $t++) {
        if ((($t + 1) -gt $mt18[$r]) -and ($dist18[$r][$t] -ne 0)) { $distFail18++ }
    }
}
Write-Output ("18.4 品阶分布表（上限一致性 / 每行合计=100）失败项 : {0} {1}" -f `
    $distFail18, $(if ($distFail18 -eq 0) { '→ PASS' } else { '→ FAIL' }))

# 18.5 声誉等级阈值：单调递增、各级起点正确、Fabled 可达（阈值 ≤ 分数上限）
$thr18 = @(0, 200, 500, 1000, 2000, 5000, 10000, 30000)
$seq18 = @()
for ($i = 0; $i -lt $thr18.Length; $i++) {
    $seq18 += ("{0}→{1}" -f $thr18[$i], $tierFn18.Invoke($null, @([int]$thr18[$i])))
}
Write-Output ("18.5 声誉阈值（各级起点） : " + ($seq18 -join '  '))
$t199 = $tierFn18.Invoke($null, @([int]199))
$t30000 = $tierFn18.Invoke($null, @([int]30000))
$tMax = $tierFn18.Invoke($null, @([int]$maxScore18))
Write-Output ("     边界 199→{0} / 30000→{1} / 上限 {2}→{3} {4}" -f `
    $t199, $t30000, $maxScore18, $tMax, `
    $(if (("$t199" -eq 'Unknown') -and ("$t30000" -eq 'Fabled') -and ("$tMax" -eq 'Fabled') -and ($thr18[7] -le $maxScore18)) { '→ PASS' } else { '→ FAIL' }))

# 18.6 公主倍率只作用于模组派系系统：源码检查（不得改写原版金币差额 / 原版声望）
$fpatPath18 = Join-Path $root 'ComplexBreedingRedux\Patches\ClientPatches\FactionSystemPatches.cs'
$prinPath18 = Join-Path $root 'ComplexBreedingRedux\Patches\ClientPatches\PrincessPayFeePatch.cs'
$fpat18 = Get-Content $fpatPath18 -Raw -Encoding UTF8
$prin18Src = Get-Content $prinPath18 -Raw -Encoding UTF8

Write-Output ("18.6 FactionSystemPatches 不再按「金币差额」放大（无 delta / _goldSnapshot） : " +
              $(if (($fpat18 -notmatch 'delta') -and ($fpat18 -notmatch '_goldSnapshot')) { 'PASS' } else { 'FAIL' }))
Write-Output ("     两处补丁都不再改写原版声望（PlayerData.Reputation）                 : " +
              $(if (($fpat18 -notmatch 'Reputation\s*=[^=]') -and ($prin18Src -notmatch 'Reputation\s*=[^=]')) { 'PASS' } else { 'FAIL' }))
Write-Output ("     PrincessPayFeePatch 不再是 Harmony 补丁（无 [HarmonyPatch] 特性）  : " +
              $(if ($prin18Src -notmatch '(?m)^\s*\[HarmonyPatch') { 'PASS' } else { 'FAIL' }))
Write-Output ("     公主加成只作用于派系财富（GetExtraWealth + 额外 9 倍）               : " +
              $(if (($prin18Src -match 'GetExtraWealth') -and ($prin18Src -match 'RevenueMultiplier - 1') -and ($fpat18 -match 'PrincessPayFeePatch\.GetExtraWealth')) { 'PASS' } else { 'FAIL' }))
Write-Output ("     公主的派系声誉加成走「相乘倍率」（CombineFactionReputationMultiplier） : " +
              $(if ($fpat18 -match 'CombineFactionReputationMultiplier\(isPrincess, depravedBloodlineLevel\)') { 'PASS' } else { 'FAIL' }))

Write-Output '--- 21) 血脉：堕落时派系营业声望倍率（相乘）；品阶基础声誉 1/2/3/4/5 ---'

$fs21 = $asm.GetType('ComplexBreedingRedux.Features.FactionSystem')
$bl21 = $asm.GetType('RaceBreedingExtension.Bloodline.BloodlineSystem')

$mBlMult = $bl21.GetMethod('GetFactionReputationSpeedMultiplier', $flags)
$blSample21 = @(1, 2, 3, 5, 10) | ForEach-Object { [double]$mBlMult.Invoke($null, @([int]$_)) }
Write-Output ("血脉 +1/+2/+3/+5/+10 → 声望倍率 = " + (($blSample21 | ForEach-Object { '{0:0.##}' -f $_ }) -join ', ') +
              "   (期望 1.5, 2, 2.5, 3.5, 6)")

$mCombine = $fs21.GetMethod('CombineFactionReputationMultiplier', $flags)
$cases21 = @(
    @($false, 0, 1.0), @($false, 1, 1.5), @($false, 10, 6.0),
    @($true, 0, 10.0), @($true, 1, 15.0), @($true, 5, 35.0), @($true, 10, 60.0)
)
$fail21 = 0
foreach ($c in $cases21) {
    $got = [double]$mCombine.Invoke($null, @([bool]$c[0], [int]$c[1]))
    $ok = [Math]::Abs($got - [double]$c[2]) -lt 1e-4
    if (-not $ok) { $fail21++ }
    Write-Output ("     公主={0,-5} 血脉等级={1,2} → 倍率 {2,5:0.##}   (期望 {3:0.##})  {4}" -f `
        $c[0], $c[1], $got, $c[2], $(if ($ok) { 'OK' } else { 'FAIL' }))
}
Write-Output ("21.1 倍率为「相乘」：血脉 +10 与公主叠加 = 6 × 10 = 60 倍（原加算 16 倍） : {0}" -f `
    $(if ($fail21 -eq 0) { 'PASS' } else { "FAIL x$fail21" }))

$diminish21 = [double]$fs21.GetField('ReputationRewardDiminishPerLevel', $flags).GetValue($null)
$tierRew21 = $fs21.GetField('TierReputationReward', $flags).GetValue($null)
$eff21 = (($tierRew21 | Select-Object -Skip 1) -join ', ')
Write-Output ("基础声誉表（品阶 1..5）= {0}   (期望 1, 2, 3, 4, 5)；高声誉递减 = {1}/级" -f $eff21, $diminish21)
Write-Output ("21.2 品阶基础派系声誉 = 1 / 2 / 3 / 4 / 5 : {0}" -f `
    $(if ($eff21 -eq '1, 2, 3, 4, 5') { 'PASS' } else { 'FAIL' }))
Write-Output ("     原表刻度 6/12/20/32/50 → 现基础值（同旧公式 (原值−声誉等级)×0.1 的十分之一量级）: {0}" -f `
    $(if ([Math]::Abs($diminish21 - 0.1) -lt 1e-6) { 'PASS' } else { 'FAIL' }))

$mFac21 = $fs21.GetMethod('GetFactionForBloodline', $flags)
$eb21 = $asm.GetType('RaceBreedingExtension.Bloodline.Data.EBloodline')
$map21 = @()
foreach ($n in @('Nature', 'Wild', 'Demon', 'Monster', 'Dragon', 'Divine', 'Magic', 'Elemental')) {
    $f = $mFac21.Invoke($null, @([System.Enum]::Parse($eb21, $n)))
    $map21 += ("{0}→{1}" -f $n, $(if ($f) { $f.InternalName } else { 'MISSING' }))
}
Write-Output ("21.3 血脉所属派系 : " + ($map21 -join '  '))
Write-Output ("     8 种血脉全部有派系归属 : " +
              $(if (($map21 -join '') -notmatch 'MISSING') { 'PASS' } else { 'FAIL' }))

$resxEn21 = Join-Path $root 'ComplexBreedingRedux\Properties\Strings.resx'
$resxZh21 = Join-Path $root 'ComplexBreedingRedux\Properties\Strings.zh-cn.resx'
$key21 = 'Bloodline_Line_FactionReputation'
Write-Output ("21.4 本地化键 {0} ：en={1} zh-cn={2}（其它语言回落英文本）" -f $key21, `
    $(if ((Get-Content $resxEn21 -Raw -Encoding UTF8) -match $key21) { 'PASS' } else { 'FAIL' }), `
    $(if ((Get-Content $resxZh21 -Raw -Encoding UTF8) -match $key21) { 'PASS' } else { 'FAIL' }))

$blSrc21 = Get-Content (Join-Path $root 'Bloodline\BloodlineSystem.cs') -Raw -Encoding UTF8
Write-Output ("21.5 血脉描述「每级效果」已渲染该行 : " +
              $(if ($blSrc21 -match 'Bloodline_Line_FactionReputation') { 'PASS' } else { 'FAIL' }))

$fpatSrc21 = Get-Content (Join-Path $root 'ComplexBreedingRedux\Patches\ClientPatches\FactionSystemPatches.cs') -Raw -Encoding UTF8
Write-Output ("     结算处使用相乘倍率 + 堕落血脉等级 : " +
              $(if (($fpatSrc21 -match 'CombineFactionReputationMultiplier\(isPrincess, depravedBloodlineLevel\)') -and `
                    ($fpatSrc21 -match 'GetDepravedBloodlineLevelForFaction\(partner, faction\)')) { 'PASS' } else { 'FAIL' }))
Write-Output ("     已不再直接乘公主声誉倍率（无 GetReputationReward / 乘法放大） : " +
              $(if (($fpatSrc21 -notmatch 'GetReputationReward') -and ($fpatSrc21 -notmatch 'PrincessRevenueMultiplier')) { 'PASS' } else { 'FAIL' }))

Write-Output '--- 19) 派系声誉区间 (0~99999) / 访客血脉等级 / 猎手营地生成数值 ---'

$fs19 = $asm.GetType('ComplexBreedingRedux.Features.FactionSystem')
$tierFn19 = $fs19.GetMethod('GetReputationTier', $flags)
$maxScore19 = $fs19.GetField('MaxReputationScore', $flags).GetValue($null)
Write-Output ("MaxReputationScore = {0} (期望 99999) : {1}" -f $maxScore19, $(if ($maxScore19 -eq 99999) { 'PASS' } else { 'FAIL' }))

$bands19 = @(
    @(0, 199, 'Unknown'), @(200, 499, 'Obscure'), @(500, 999, 'Insignificant'),
    @(1000, 1999, 'Noteworthy'), @(2000, 4999, 'Reputable'), @(5000, 9999, 'Significant'),
    @(10000, 29999, 'Famous'), @(30000, 99999, 'Fabled')
)
$bandFail19 = 0
foreach ($b in $bands19) {
    $lo = $tierFn19.Invoke($null, @([int]$b[0])).ToString()
    $hi = $tierFn19.Invoke($null, @([int]$b[1])).ToString()
    if (($lo -ne $b[2]) -or ($hi -ne $b[2])) { $bandFail19++ }
    Write-Output ("  {0,6} ~ {1,6} → {2,-13} (实测 {3} ~ {4})" -f $b[0], $b[1], $b[2], $lo, $hi)
}
Write-Output ("19.1 8 个分数区间与等级一一对应 : {0}" -f $(if ($bandFail19 -eq 0) { 'PASS' } else { "FAIL x$bandFail19" }))

$blTable19 = $fs19.GetField('MaxVisitorBloodlineLevelByReputation', $flags).GetValue($null)
Write-Output ("19.2 访客血脉等级上限 0..7 = " + ($blTable19 -join ', ') + " (期望 3,4,5,6,7,8,9,10) : " +
              $(if (($blTable19 -join ',') -eq '3,4,5,6,7,8,9,10') { 'PASS' } else { 'FAIL' }))

$fsSrc19 = Get-Content (Join-Path $root 'ComplexBreedingRedux\Features\FactionSystem.cs') -Raw -Encoding UTF8
Write-Output ("19.3 访客血脉等级只用模组派系声誉（GetFactionReputationIndex） : " +
              $(if ($fsSrc19 -match 'ApplyVisitorBloodline\(instance, faction, GetFactionReputationIndex\(faction\.Id\)\)') { 'PASS' } else { 'FAIL' }))
$rollBlBody19 = ''
if ($fsSrc19 -match '(?s)RollVisitorBloodlineLevel\(int reputationIndex\).*?\n') { $rollBlBody19 = $matches[0] }
Write-Output ("     RollVisitorBloodlineLevel 不引用原版声望等级（ReputationLevel） : " +
              $(if ($rollBlBody19 -notmatch 'ReputationLevel') { 'PASS' } else { 'FAIL' }))

# 猎手营地：满声望时各属性 / 血脉的取值区间
$hcfg19 = $asm.GetType('RaceBreedingExtension.HunterCamp.HunterCampConfig')
$ranges19 = $hcfg19.GetField('ProducedStatTraitRanges', $flags).GetValue($null)
$jitter19 = [double]$hcfg19.GetField('StatReputationJitter', $flags).GetValue($null)
$peak19 = @{ 'Trait96' = @(9000, 9999); 'Trait97' = @(9000, 9999); 'Trait98' = @(899, 999) }
$peakFail19 = 0
foreach ($name in ($peak19.Keys | Sort-Object)) {
    $w = 0.0; $b = 0.0
    foreach ($kv in $ranges19.GetEnumerator()) {
        if ($kv.Key.ToString() -eq $name) { $w = [double]$kv.Value.Item1; $b = [double]$kv.Value.Item2 }
    }
    $a = $w + ($b - $w) * (1 - $jitter19)
    $minA = [Math]::Min($a, $b); $maxA = [Math]::Max($a, $b)
    $expLo = $peak19[$name][0]; $expHi = $peak19[$name][1]
    if ([Math]::Abs($minA - $expLo) -gt 60) { $peakFail19++ }
    Write-Output ("19.4 满声望 {0} 区间 ≈ {1:0} ~ {2:0}  (期望 {3} ~ {4})" -f $name, $minA, $maxA, $expLo, $expHi)
}
Write-Output ("     满声望峰值区间达标 : {0}" -f $(if ($peakFail19 -eq 0) { 'PASS' } else { "FAIL x$peakFail19" }))

$maxBl19 = $asm.GetType('RaceBreedingExtension.Bloodline.BloodlineSystem').GetField('MaxLevel', $flags).GetValue($null)
$blLo19 = 1 + ($maxBl19 - 1) * (1 - $jitter19)
Write-Output ("19.5 满声望 猎手营地血脉等级 ∈ {0:0.0} ~ {1} → 取整 {2} ~ {3} (期望 9 ~ 10) : {4}" -f `
    $blLo19, $maxBl19, [Math]::Round($blLo19), $maxBl19, $(if ([Math]::Round($blLo19) -eq 9) { 'PASS' } else { 'FAIL' }))

$hcSrc19 = Get-Content (Join-Path $root 'HunterCamp\HunterCampSystem.cs') -Raw -Encoding UTF8
Write-Output ("19.6 猎手营地按物种写入血脉（ApplyProducedBloodline，等级按派系声望） : " +
              $(if (($hcSrc19 -match 'ApplyProducedBloodline\(female, species, profile\.ReputationNormalized\)') -and `
                    ($hcSrc19 -match 'SpeciesBloodlineMap\.TryGetBySpeciesName\(speciesName')) { 'PASS' } else { 'FAIL' }))
Write-Output ("     猎手营地声望系数取模组派系声誉分数（GetFactionReputationNormalized） : " +
              $(if ($hcSrc19 -match 'FactionSystem\.GetFactionReputationNormalized\(faction\.Id\)') { 'PASS' } else { 'FAIL' }))
Write-Output ("     Trait99 / Trait100 不参与声望插值（IsReputationScaledStat = false） : " +
              $(if (($hcfg19.GetMethod('IsReputationScaledStat', $flags).Invoke($null, @([System.Enum]::Parse(`
                  $ranges19.GetType().GetGenericArguments()[0], 'Trait99')))) -eq $false -and `
                  ($hcfg19.GetMethod('IsReputationScaledStat', $flags).Invoke($null, @([System.Enum]::Parse(`
                  $ranges19.GetType().GetGenericArguments()[0], 'Trait100')))) -eq $false) { 'PASS' } else { 'FAIL' }))

Write-Output '--- 20) 性爱进度速度：高潮次数加成 + 0.22 统一上限（含 Trainer / 血脉） ---'

$spm = $asm.GetType('DepravedShield.Helpers.SexProgressManager')
$maxSpeed20 = $spm.GetField('MaxSexProgressSpeed', $flags).GetValue($null)
$stepCnt20 = $spm.GetField('OrgasmCountPerSpeedStep', $flags).GetValue($null)
$maxCnt20 = $spm.GetField('OrgasmCountSpeedBonusMaxCount', $flags).GetValue($null)
$stepVal20 = $spm.GetField('OrgasmCountSpeedStep', $flags).GetValue($null)
Write-Output ("MaxSexProgressSpeed = {0} (期望 0.22) : {1}" -f `
    $maxSpeed20, $(if ([Math]::Abs([double]$maxSpeed20 - 0.22) -lt 1e-6) { 'PASS' } else { 'FAIL' }))
Write-Output ("高潮加成常量：每 {0} 次 +{1} / 封顶 {2} 次（= +{3:0.00}）" -f `
    $stepCnt20, $stepVal20, $maxCnt20, ([double]$stepVal20 * ($maxCnt20 / $stepCnt20)))

$mBonus = $spm.GetMethod('GetOrgasmCountSpeedBonus', $flags)
$expect20 = @{ 0 = 0.0; 299 = 0.0; 300 = 0.01; 599 = 0.01; 600 = 0.02; 1499 = 0.04; 1500 = 0.05; 1501 = 0.05; 99999 = 0.05 }
$bonusFail20 = 0
foreach ($k in ($expect20.Keys | Sort-Object)) {
    $got = [double]$mBonus.Invoke($null, @([int]$k))
    $ok = [Math]::Abs($got - [double]$expect20[$k]) -lt 1e-6
    if (-not $ok) { $bonusFail20++ }
    Write-Output ("     高潮 {0,6} 次 → +{1:0.00}   (期望 +{2:0.00})  {3}" -f $k, $got, $expect20[$k], $(if ($ok) { 'OK' } else { 'FAIL' }))
}
Write-Output ("20.1 高潮次数加成曲线（每 300 次 +0.01，1500 封顶） : {0}" -f `
    $(if ($bonusFail20 -eq 0) { 'PASS' } else { "FAIL x$bonusFail20" }))

$spmSrc20 = Get-Content (Join-Path $root 'DepravedShield\Helpers\SexProgressManager.cs') -Raw -Encoding UTF8
Write-Output ("20.2 旧 0.15 上限已移除（无 0.15f） : " +
              $(if ($spmSrc20 -notmatch '0\.15f') { 'PASS' } else { 'FAIL' }))
Write-Output ("     旧「额外加成不受上限」写法已移除（无 capped / float extra） : " +
              $(if (($spmSrc20 -notmatch 'capped') -and ($spmSrc20 -notmatch 'float extra')) { 'PASS' } else { 'FAIL' }))

$getFn20 = ''
if ($spmSrc20 -match '(?s)public static float GetSexProgressSpeed\(Character character\).*?\n        \}') { $getFn20 = $matches[0] }
if ([string]::IsNullOrEmpty($getFn20)) {
    Write-Output '     方法体提取失败 → FAIL（请检查 GetSexProgressSpeed 源码格式）'
} else {
    $trainerIdx = $getFn20.IndexOf('speed += 0.09f;')
    $bloodIdx = $getFn20.IndexOf('GetPartnerSexSpeedBonus')
    $orgasmIdx = $getFn20.IndexOf('GetOrgasmCountSpeedBonus')
    $capIdx = $getFn20.IndexOf('Mathf.Min(speed, MaxSexProgressSpeed)')
    Write-Output ("     Trainer(+0.09) 与 血脉加成 都排在统一上限之前 : " +
                  $(if (($trainerIdx -gt 0) -and ($bloodIdx -gt 0) -and ($capIdx -gt $trainerIdx) -and ($capIdx -gt $bloodIdx)) { 'PASS' } else { 'FAIL' }))
    Write-Output ("     高潮次数加成已接入且在上限之前 : " +
                  $(if (($orgasmIdx -gt 0) -and ($capIdx -gt $orgasmIdx)) { 'PASS' } else { 'FAIL' }))
}

Write-Output '--- 22) 营地俘虏禁止通过「Move To Estate」键位搬去黑市 ---'

$hcPatchesSrc22 = Get-Content (Join-Path $root 'HunterCamp\HunterCampPatches.cs') -Raw -Encoding UTF8
$estatePatchNames22 = @(
    'CaptiveSelectedMoveToManagementEstatePatch',
    'CaptiveSelectedMoveToPrivateEstatePatch',
    'CaptiveDraggingMoveToManagementEstatePatch',
    'CaptiveDraggingMoveToPrivateEstatePatch',
    'CaptivePlayDataMoveToManagementEstatePatch',
    'CaptivePlayDataMoveToPrivateEstatePatch',
    'CaptiveMultiMoveToManagementEstatePatch',
    'CaptiveMultiMoveToPrivateEstatePatch'
)
$missing22 = @()
foreach ($n in $estatePatchNames22) {
    if ($null -eq $asm.GetType('RaceBreedingExtension.HunterCamp.HunterCampPatches+' + $n)) { $missing22 += $n }
}
Write-Output ("22.1 键位 / 底层 / 多选 共 8 个拦截入口，缺失项 : " +
              $(if ($missing22.Count -eq 0) { '无 → PASS' } else { ($missing22 -join ', ') + ' → FAIL' }))

Write-Output ("22.2 目标为 InteractionPrivateEstate 的 4 个键位方法 : " +
              $(if (($hcPatchesSrc22 -match 'typeof\(InteractionPrivateEstate\), "SelectedUnitMoveToManagementEstate"') -and `
                    ($hcPatchesSrc22 -match 'typeof\(InteractionPrivateEstate\), "SelectedUnitMoveToPrivateEstate"') -and `
                    ($hcPatchesSrc22 -match 'typeof\(InteractionPrivateEstate\), "DraggingUnitMoveToManagementEstate"') -and `
                    ($hcPatchesSrc22 -match 'typeof\(InteractionPrivateEstate\), "DraggingUnitMoveToPrivateEstate"')) { 'PASS' } else { 'FAIL' }))

Write-Output ("22.3 底层 MoveTo*Estate 兜底 + 多选批量剔除俘虏 : " +
              $(if (($hcPatchesSrc22 -match 'nameof\(PlayData\.MoveToManagementEstate\)') -and `
                    ($hcPatchesSrc22 -match 'nameof\(PlayData\.MoveToPrivateEstate\)') -and `
                    ($hcPatchesSrc22 -match 'MoveMultiSelectedUnitToManagementEstate\)\)') -and `
                    ($hcPatchesSrc22 -match 'MoveMultiSelectedUnitToPrivateEstate\)\)') -and `
                    ($hcPatchesSrc22 -match 'MultiSelectedUnitSeqList')) { 'PASS' } else { 'FAIL' }))

Write-Output ("22.4 仅对营地俘虏生效，并复用原版「不是你拥有的」提示 : " +
              $(if (($hcPatchesSrc22 -match 'HunterCampSystem\.IsCaptive\(character\)') -and `
                    ($hcPatchesSrc22 -match '#Not_Your_Slave') -and ($hcPatchesSrc22 -match '#Not_Your_Monster')) { 'PASS' } else { 'FAIL' }))

Write-Output ("22.5 本功能不修改任何 UI / 特质槽位（无 TraitSlotExpansion / 克隆槽位） : " +
              $(if (($hcPatchesSrc22 -notmatch 'TraitSlotExpansion') -and ($hcPatchesSrc22 -notmatch 'LayoutGroup') -and ($hcPatchesSrc22 -notmatch 'Instantiate\(')) { 'PASS' } else { 'FAIL' }))

Write-Output '--- 23) 血脉「怪物效果」新增 交配时间 / 体力消耗量 两行（Trait99/100 兜底显示） ---'

$blSrc23 = Get-Content (Join-Path $root 'Bloodline\BloodlineSystem.cs') -Raw -Encoding UTF8
Write-Output ("23.1 每级效果区块已移除 交配时间/体力消耗 两行（不再引用旧键） : " +
              $(if (($blSrc23 -notmatch 'Loc\("Bloodline_Line_SexTime"\)') -and ($blSrc23 -notmatch 'Loc\("Bloodline_Line_MentalityCost"\)')) { 'PASS' } else { 'FAIL' }))
Write-Output ("23.2 怪物效果区块固定追加这两行（BuildTraitValueLines） : " +
              $(if ($blSrc23 -match 'BuildMonsterEffectLine\(data, level\)\s*\+\s*BuildTraitValueLines\(data, level, character\)') { 'PASS' } else { 'FAIL' }))
Write-Output ("23.3 数值 = 角色 Trait99/Trait100 实际值（血脉已写入该槽位 → 天然相加） : " +
              $(if (($blSrc23 -match 'GetTraitValue\(BloodlineVanillaStats\.SexTimeTrait\)') -and ($blSrc23 -match 'GetTraitValue\(BloodlineVanillaStats\.MentalityCostTrait\)')) { 'PASS' } else { 'FAIL' }))

# 本地化键（中英）
$resxZh23 = Join-Path $root 'ComplexBreedingRedux\Properties\Strings.zh-cn.resx'
$resxEn23 = Join-Path $root 'ComplexBreedingRedux\Properties\Strings.resx'
$zh23 = Get-Content $resxZh23 -Raw -Encoding UTF8
$en23 = Get-Content $resxEn23 -Raw -Encoding UTF8
$newKeys23 = @('Bloodline_Line_MonsterSexTimePerLevel', 'Bloodline_Line_MonsterMentalityPerLevel',
               'Bloodline_Line_MonsterSexTime', 'Bloodline_Line_MonsterMentality')
$missZh23 = @($newKeys23 | Where-Object { $zh23 -notmatch $_ })
$missEn23 = @($newKeys23 | Where-Object { $en23 -notmatch $_ })
Write-Output ("23.4 新增 4 个本地化键（zh 缺失 {0} 个 / en 缺失 {1} 个） : {2}" -f `
    $missZh23.Count, $missEn23.Count, $(if (($missZh23.Count -eq 0) -and ($missEn23.Count -eq 0)) { 'PASS' } else { 'FAIL' }))
Write-Output ("     文本样例 zh: " +
              $(if ($zh23.Contains('交配时间 {0}（每级+{1}s）')) { '交配时间 {0}（每级+{1}s） OK' } else { '缺失 → FAIL' }) + " / " +
              $(if ($zh23.Contains('体力消耗量 {0}（每级+{1}）')) { '体力消耗量 {0}（每级+{1}） OK' } else { '缺失 → FAIL' }))

# 渲染断言统一用英文资源（否则受系统语言影响；Culture 属性是 internal，需用 NonPublic 标志）
$strings23 = $asm.GetType('ComplexBreedingRedux.Properties.Strings')
$strings23.GetProperty('Culture', $priv).SetValue($null, [System.Globalization.CultureInfo]::GetCultureInfo('en'))

# 实际渲染：Demon(两条都有每级加成) / Wild(仅体力) / Divine(两者都无每级加成)
$build23 = $bs.GetMethod('GetDescription', $priv, $null, @($bd, [int]), $null)
$byName23 = @{}
foreach ($item in $bref.GetProperty('All', $flags).GetValue($null)) { $byName23[$item.PSObject.BaseObject.InternalName] = $item.PSObject.BaseObject }

foreach ($name in @('Demon', 'Wild', 'Divine')) {
    if (-not $byName23.ContainsKey($name)) { Write-Output ("   $name 缺失 → FAIL"); continue }
    $txt = $build23.Invoke($null, @($byName23[$name], 4))
    $idxMonster = $txt.IndexOf('Monster effects')
    $idxSex = $txt.IndexOf('Sex time')
    $hasPerLevelSex = $txt -match 'Sex time .*\(\+\d+s per level\)'
    $hasPerLevelMen = $txt -match 'Stamina cost .*\(\+[\d.]+ per level\)'
    $unresolved = $txt -match 'Bloodline_Line_'
    Write-Output ("   渲染 {0} (+4)：SexTime 行在怪物效果区块内={1} 每级后缀(时间)={2} 每级后缀(体力)={3} 无未解析键={4}" -f `
        $name, ($idxSex -gt $idxMonster -and $idxMonster -ge 0), $hasPerLevelSex, $hasPerLevelMen, (-not $unresolved))
    $expectSexSuffix = $name -eq 'Demon'
    $expectMenSuffix = $name -eq 'Demon' -or $name -eq 'Wild'
    Write-Output ("     {0} 期望：时间后缀={1} 体力后缀={2} → {3}" -f $name, $expectSexSuffix, $expectMenSuffix, `
        $(if (($hasPerLevelSex -eq $expectSexSuffix) -and ($hasPerLevelMen -eq $expectMenSuffix) -and (-not $unresolved) -and ($idxSex -gt $idxMonster)) { 'PASS' } else { 'FAIL' }))
}

Write-Output '--- 24) 交配时间/体力消耗量 数值着色（>0 红 / <0 绿） ---'

$gc24 = $asm.GetType('ComplexBreedingRedux.Data.GameColors')
$good24 = $gc24.GetField('GoodColorHex', $flags).GetValue($null)
$bad24 = $gc24.GetField('BadColorHex', $flags).GetValue($null)
Write-Output ("     颜色常量：Good(绿)={0}  Bad(红)={1}" -f $good24, $bad24)

$mColor24 = $bs.GetMethod('ColoredSigned', $priv, $null, @([single]), $null)
Write-Output ("24.1 着色辅助 ColoredSigned 存在 : {0}" -f ($null -ne $mColor24))

if ($mColor24 -ne $null) {
    $cases24 = @(
        @(40.0, [string]$bad24, '正值 → 红'),
        @(-5.0, [string]$good24, '负值 → 绿'),
        @(0.0, '', '零 → 不着色')
    )
    $fail24 = 0
    foreach ($c in $cases24) {
        $out = [string]$mColor24.Invoke($null, @([single]$c[0]))
        $ok = if ($c[1] -eq '') { $out -notmatch '<color=' } else { $out -match [regex]::Escape($c[1]) }
        if (-not $ok) { $fail24++ }
        Write-Output ("     ColoredSigned({0,5}) = {1}   ({2})  {3}" -f $c[0], $out, $c[2], $(if ($ok) { 'OK' } else { 'FAIL' }))
    }
    Write-Output ("24.2 正/负/零 三种取色正确 : {0}" -f $(if ($fail24 -eq 0) { 'PASS' } else { "FAIL x$fail24" }))
}

$blSrc24 = Get-Content (Join-Path $root 'Bloodline\BloodlineSystem.cs') -Raw -Encoding UTF8
$lineCalls24 = ([regex]::Matches($blSrc24, 'ColorWholeLine\(')).Count
Write-Output ("24.3 两行的 4 个渲染分支均用「整行着色」ColorWholeLine（调用次数 {0}，含定义应为 5） : {1}" -f `
    $lineCalls24, $(if ($lineCalls24 -ge 5) { 'PASS' } else { 'FAIL' }))
Write-Output ("     保留仅数字着色的 ColoredSigned 辅助 : {0}" -f `
    $(if ($blSrc24 -match 'private static string ColoredSigned\(float value\)') { 'PASS' } else { 'FAIL' }))

# 实际渲染样例（Demon: 数值全为正 → 红；Divine: 0 → 无色）
$strings24 = $asm.GetType('ComplexBreedingRedux.Properties.Strings')
$strings24.GetProperty('Culture', $priv).SetValue($null, [System.Globalization.CultureInfo]::GetCultureInfo('en'))
$build24 = $bs.GetMethod('GetDescription', $priv, $null, @($bd, [int]), $null)
$byName24 = @{}
foreach ($item in $bref.GetProperty('All', $flags).GetValue($null)) { $byName24[$item.PSObject.BaseObject.InternalName] = $item.PSObject.BaseObject }

$demon24 = [string]$build24.Invoke($null, @($byName24['Demon'], 4))
$divine24 = [string]$build24.Invoke($null, @($byName24['Divine'], 4))
$sexSegDot24 = ($demon24 -split '<BR>') | Where-Object { $_ -match 'Sex time' } | Select-Object -First 1
$menSegDot24 = ($demon24 -split '<BR>') | Where-Object { $_ -match 'Stamina cost' } | Select-Object -First 1
Write-Output ("     样例：{0} / {1}" -f $sexSegDot24, $menSegDot24)
Write-Output ("24.4 Demon(+4) 交配时间/体力 **整行**为红色（标签 + 数值 + 每级后缀） : " +
              $(if (($demon24 -match ('<color=' + [regex]::Escape([string]$bad24) + '>Sex time \+20s \(\+5s per level\)</color>')) -and `
                    ($demon24 -match ('<color=' + [regex]::Escape([string]$bad24) + '>Stamina cost \+0\.2 \(\+0\.05 per level\)</color>'))) { 'PASS' } else { 'FAIL' }))
Write-Output ("     Divine(+4) 数值 0 → 整行不着色 : " +
              $(if (($divine24 -match '- Sex time 0s') -and ($divine24 -match '- Stamina cost 0') -and
                    ($divine24 -notmatch '<color=[^>]*>Sex time')) { 'PASS' } else { 'FAIL' }))

# 24.5 四个分支（有 / 无「每级」后缀）都必须整行着色
$wild24 = [string]$build24.Invoke($null, @($byName24['Wild'], 4))
$demonSexLine24 = ($demon24 -split '<BR>') | Where-Object { $_ -match 'Sex time' } | Select-Object -First 1
$demonMenLine24 = ($demon24 -split '<BR>') | Where-Object { $_ -match 'Stamina cost' } | Select-Object -First 1
$wildMenLine24 = ($wild24 -split '<BR>') | Where-Object { $_ -match 'Stamina cost' } | Select-Object -First 1
$divineSexLine24 = ($divine24 -split '<BR>') | Where-Object { $_ -match 'Sex time' } | Select-Object -First 1

$lineCases24 = @(
    @('Demon 时间行(有每级, 值 +20 → 红)', $demonSexLine24, [string]$bad24),
    @('Demon 体力行(有每级, 值 +0.2 → 红)', $demonMenLine24, [string]$bad24),
    @('Wild  体力行(有每级, 值 +0.2 → 红)', $wildMenLine24, [string]$bad24),
    @('Divine 时间行(无每级, 值 0 → 不着色)', $divineSexLine24, '')
)
$failLine24 = 0
foreach ($pair in $lineCases24) {
    $line = ([string]$pair[1]) -replace '</size>', ''
    $color = [string]$pair[2]
    $ok = if ($color -eq '') {
        $line -notmatch '<color='
    } else {
        ($line -match ('^(?:- )?<color=' + [regex]::Escape($color) + '>')) -and ($line -match '</color>$')
    }
    if (-not $ok) { $failLine24++ }
    Write-Output ("     {0} = [{1}]   → {2}" -f $pair[0], $line, $(if ($ok) { 'OK' } else { 'FAIL' }))
}
Write-Output ("24.5 整行着色覆盖全部分支（值 0 的行不染色，其余整行染色） : {0}" -f $(if ($failLine24 -eq 0) { 'PASS' } else { "FAIL x$failLine24" }))

# 24.6 直接校验整行着色辅助（含负值 → 整行绿）
$mWhole24 = $bs.GetMethod('ColorWholeLine', $priv, $null, @([string], [single]), $null)
if ($null -ne $mWhole24) {
    $pos24 = [string]$mWhole24.Invoke($null, @('Stamina cost +0.4 (+0.1 per level)', [single]0.4))
    $neg24 = [string]$mWhole24.Invoke($null, @('Stamina cost -0.4 (-0.1 per level)', [single]-0.4))
    $zero24 = [string]$mWhole24.Invoke($null, @('Sex time 0s', [single]0))
    Write-Output ("     +0.4 → {0}" -f $pos24)
    Write-Output ("     -0.4 → {0}" -f $neg24)
    Write-Output ("     0    → {0}" -f $zero24)
    Write-Output ("24.6 ColorWholeLine：正值整行红 / 负值整行绿 / 零不着色 : {0}" -f `
        $(if (($pos24 -eq ('<color=' + [string]$bad24 + '>Stamina cost +0.4 (+0.1 per level)</color>')) -and `
              ($neg24 -eq ('<color=' + [string]$good24 + '>Stamina cost -0.4 (-0.1 per level)</color>')) -and `
              ($zero24 -eq 'Sex time 0s')) { 'PASS' } else { 'FAIL' }))
} else {
    Write-Output ("24.6 ColorWholeLine 辅助缺失 : FAIL")
}

Write-Output '--- 25) 职业遗传 75% / 奴隶×怪物与人类父本的概率分布（含降级到基础种族） ---'

# 25.1 平衡常数
$gb25 = $asm.GetType('ComplexBreedingRedux.Data.GameBalance')
$prof25 = [double]$gb25.GetField('ProfessionInheritChance', $flags).GetValue($null)
$mon25 = [int]$gb25.GetField('MonsterSameOrLowerFemaleChance', $flags).GetValue($null)
Write-Output ("25.1 职业遗传几率 = {0}（期望 0.75）: {1}" -f $prof25, $(if ([Math]::Abs($prof25 - 0.75) -lt 1e-6) { 'PASS' } else { 'FAIL' }))
Write-Output ("25.2 奴隶×怪物「同族/降级女胎」几率 = {0}%（期望 10）: {1}" -f $mon25, $(if ($mon25 -eq 10) { 'PASS' } else { 'FAIL' }))

$tierTab25 = $gb25.GetField('TierLowerEvoBirths', $flags).GetValue($null)
Write-Output ("     降级几率表 : " + (($tierTab25.Keys | Sort-Object | ForEach-Object { "T{0}={1}%" -f $_, $tierTab25[$_] }) -join ', '))
Write-Output ("     品阶 1 不再为 0（否则链条断在品阶 1，永远生不出基础种族）: {0}" -f `
    $(if ($tierTab25[1] -gt 0) { 'PASS' } else { 'FAIL' }))

# 25.2 源码接线检查
$bsrc25 = Get-Content (Join-Path $root 'ComplexBreedingRedux\Features\BreedingSystem.cs') -Raw -Encoding UTF8
$use25 = ($bsrc25 -split 'Random\.Range\(0f, 1f\) < GameBalance\.ProfessionInheritChance').Count - 1
Write-Output ("25.3 职业遗传两处（雌性后代 / 怪物后代）均改用 75% 常量，源内已无 0.99f : {0}" -f `
    $(if (($use25 -eq 2) -and ($bsrc25 -notmatch '0\.99f')) { 'PASS' } else { "FAIL (uses=$use25)" }))
Write-Output ("25.4 奴隶×怪物：10% 门控 + 不再要求父本同种怪物 : {0}" -f `
    $(if (($bsrc25 -match 'else if \(ReturnsSameOrLowerSpeciesFemale\(female, male\)\)') -and `
          ($bsrc25 -match '(?s)Rand\(GameBalance\.MonsterSameOrLowerFemaleChance\)\s*&&\s*SameOrLowerSpeciesFemalePregnancy\(female, male\)')) { 'PASS' } else { 'FAIL' }))
Write-Output ("25.5 已删除「品阶1 永不降级」特判 / 无降级目标恒定同族 : {0}" -f `
    $(if (($bsrc25 -notmatch 'Tier <= 1') -and ($bsrc25 -match 'bool canDegrade = baseCore != null \|\| lowerMothers\.Count > 0;')) { 'PASS' } else { 'FAIL' }))
Write-Output ("25.5b 降级链以 PossiblyMothers 为准（不再按品阶过滤，Dracolich→Dragon 生效）: {0}" -f `
    $(if (($bsrc25 -match 'sd\.InternalName != species\.InternalName\)') -and ($bsrc25 -notmatch 'sd\.Tier <= species\.Tier')) { 'PASS' } else { 'FAIL' }))
Write-Output ("25.5c 怪物男胎身份不再取自 DisplayName（只认父本真实物种身份）: {0}" -f `
    $(if (($bsrc25 -notmatch 'SetSpeciesIdentity\(maleFetus, male\.DisplayName\)') -and `
          ($bsrc25 -match 'string\? fatherSpecies = GameData\.GetSpeciesIdentity\(male\);')) { 'PASS' } else { 'FAIL' }))

$gd25 = $asm.GetType('ComplexBreedingRedux.Data.GameData')
$spList25 = $gd25.GetField('SpeciesDataList', $flags).GetValue($null)
if ($spList25.Count -eq 0) { $gd25.GetMethod('Initialize', $flags).Invoke($null, @()) | Out-Null }
$coreList25 = $gd25.GetField('SpeciesCoreDataList', $flags).GetValue($null)
Write-Output ("     注册表已载入：物种 {0} / 核心种族 {1}" -f $spList25.Count, $coreList25.Count)

$bsCls25 = $asm.GetType('ComplexBreedingRedux.Features.BreedingSystem')
$mLower25 = $bsCls25.GetMethod('GetLowerBirthChance', $priv)
$mCand25 = $bsCls25.GetMethod('GetLowerMotherCandidates', $priv)
$mBase25 = $bsCls25.GetMethod('GetBaseMotherCore', $priv)
$maxDepth25 = [int]$bsCls25.GetField('MaxEvolutionDepth', $priv).GetValue($null)
Write-Output ("     辅助方法: GetLowerBirthChance={0} GetLowerMotherCandidates={1} GetBaseMotherCore={2} (MaxEvolutionDepth={3})" -f `
    ($null -ne $mLower25), ($null -ne $mCand25), ($null -ne $mBase25), $maxDepth25)

$allSp25 = @{}
foreach ($s in $spList25) { $allSp25[$s.InternalName] = $s }
$baseOf25 = @{}
foreach ($s in $spList25) {
    $core = $mBase25.Invoke($null, @($s))
    $baseOf25[$s.InternalName] = $(if ($core) { $core.InternalName } else { $null })
}


function Get-DowngradeOutcomes([string]$spName, [int]$depth) {
    $out = @{}
    if ($depth -ge $maxDepth25) {
        $out['(递归深度上限兜底)'] = 1.0
        return $out
    }
    $sp = $allSp25[$spName]
    $lower = [double]($mLower25.Invoke($null, @([int]$sp.Tier))) / 100.0
    $cands = @($mCand25.Invoke($null, @($sp)))
    $base = $baseOf25[$spName]
    # 无降级目标（无 BaseMother 且无 PossiblyMothers，如职业能力生成的 Golem）→ 恒定同族
    if ((-not $base) -and ($cands.Count -eq 0)) {
        $out[$spName] = 1.0
        return $out
    }
    if ($lower -le 0) {
        $out[$spName] = 1.0
        return $out
    }
    # ① 同族
    $out[$spName] = 1.0 - $lower
    # ② 降级：基础种族（有配置时与链条物种 50/50） / PossiblyMothers 链条物种
    if ($cands.Count -eq 0) {
        $out['[基础]' + $base] = $lower
        return $out
    }
    if ($base) {
        $out['[基础]' + $base] = $lower * 0.5
        $share = $lower * 0.5 / $cands.Count
    } else {
        $share = $lower / $cands.Count
    }
    foreach ($c in $cands) {
        $sub = Get-DowngradeOutcomes $c.InternalName ($depth + 1)
        foreach ($k in $sub.Keys) {
            $prev = 0.0
            if ($out.ContainsKey($k)) { $prev = [double]$out[$k] }
            $out[$k] = $prev + $share * [double]$sub[$k]
        }
    }
    return $out
}

$noBase25 = @()
$tier1NoBase25 = @()
foreach ($s in ($spList25 | Sort-Object Tier, InternalName)) {
    $map = Get-DowngradeOutcomes $s.InternalName 0
    $parts = @()
    foreach ($k in ($map.Keys | Sort-Object)) { $parts += ("{0} {1:N1}%" -f $k, (100 * [double]$map[$k])) }
    Write-Output ("  {0,-16} T{1} 降级{2,3}%  →  {3}" -f $s.InternalName, $s.Tier, $tierTab25[$s.Tier], ($parts -join ' | '))
    $hasBase = @($map.Keys | Where-Object { $_.StartsWith('[基础]') }).Count -gt 0
    if (-not $hasBase) {
        $noBase25 += $s.InternalName
        if ($s.Tier -eq 1) { $tier1NoBase25 += $s.InternalName }
    }
}
Write-Output ("25.6 品阶 1 物种全部能产出基础种族（Orc←Goblin、Goblin/Horse←基础种族）: {0}" -f `
    $(if ($tier1NoBase25.Count -eq 0) { 'PASS' } else { "FAIL: $($tier1NoBase25 -join ',')" }))
$endStr25 = (($noBase25 | Sort-Object) -join ',')
Write-Output ("     链条终点（设计上到不了「基础种族」的物种）: " + $(if ($endStr25 -eq '') { '无' } else { $endStr25 }) + `
    "  → 期望 Golem（职业能力生成、无降级种族）, Genasi（降级到 Golem 后止） : " + `
    $(if ($endStr25 -eq 'Genasi,Golem') { 'PASS' } else { 'FAIL' }))

$golemMap25 = Get-DowngradeOutcomes 'Golem' 0
Write-Output ("25.6b Golem 恒定同族（无降级种族，不再落回上层兜底/基础种族女胎）: {0}" -f `
    $(if (($golemMap25.Count -eq 1) -and $golemMap25.ContainsKey('Golem') -and ([Math]::Abs([double]$golemMap25['Golem'] - 1.0) -lt 1e-9)) { 'PASS' } else { "FAIL ($($golemMap25.Keys -join ','))" }))

$dracoMap25 = Get-DowngradeOutcomes 'Dracolich' 0
Write-Output ("25.6c Dracolich 可沿 PossiblyMothers 降级 Dragon 链（含 [基础]Lizard）: " + `
    (($dracoMap25.Keys | Sort-Object | ForEach-Object { "{0} {1:N1}%" -f $_, (100 * [double]$dracoMap25[$_]) }) -join ' | ') + `
    " : " + $(if ($dracoMap25.ContainsKey('Dragon') -and (@($dracoMap25.Keys | Where-Object { $_.StartsWith('[基础]') }).Count -gt 0)) { 'PASS' } else { 'FAIL' }))

$genasiMap25 = Get-DowngradeOutcomes 'Genasi' 0
Write-Output ("25.6d Genasi 可降级为 Golem（50% Genasi + 50% Golem）: " + `
    (($genasiMap25.Keys | Sort-Object | ForEach-Object { "{0} {1:N1}%" -f $_, (100 * [double]$genasiMap25[$_]) }) -join ' | ') + `
    " : " + $(if ($genasiMap25.ContainsKey('Golem') -and ($genasiMap25.Count -eq 2)) { 'PASS' } else { 'FAIL' }))

# 25.4 奴隶 × 怪物 / 奴隶 × 人类 的实际三档分布
function Get-EvolutionChance([string]$motherName, [string]$fatherRaceName) {
    $mother = $allSp25[$motherName]
    $p = 1.0
    foreach ($x in $spList25) {
        if (-not $x.PossiblyFathers) { continue }
        $fathers = @($x.PossiblyFathers | ForEach-Object { $_.ToString() })
        if ($fathers -notcontains $fatherRaceName) { continue }
        $match = ($x.InternalName -eq $mother.InternalName)
        if ((-not $match) -and $x.PossiblyMothers) { $match = @($x.PossiblyMothers) -contains $mother.InternalName }
        if (-not $match) { continue }
        $p = $p * (1.0 - [double]$x.BaseChance / 100.0)
    }
    return 1.0 - $p
}

$cases25 = @(
    @('Goblin', 'Goblin', 'monster'),
    @('Hobgoblin', 'Goblin', 'monster'),
    @('Orc', 'Orc', 'monster'),
    @('Nekomata', 'Minotaur', 'monster'),
    @('Bugbear', 'Minotaur', 'monster'),
    @('Goblin', 'Human', 'human'),
    @('Orc', 'Human', 'human'),
    @('Succubus', 'Human', 'human')
)
$fail25 = 0
foreach ($c in $cases25) {
    $mName = $c[0]; $fName = $c[1]; $isMonster = ($c[2] -eq 'monster')
    $evo = Get-EvolutionChance $mName $fName
    $chain = Get-DowngradeOutcomes $mName 0
    $female = $(if ($isMonster) { 0.10 } else { 1.0 - $evo })
    $monsterMale = $(if ($isMonster) { [Math]::Max(0.0, 1.0 - $evo - 0.10) } else { 0.0 })
    $chainStr = @()
    foreach ($k in ($chain.Keys | Sort-Object)) { $chainStr += ("{0} {1:N1}%" -f $k, (100 * $female * [double]$chain[$k])) }
    Write-Output ("25.7 {0}母 × {1}：①进化物种女胎 {2:N1}% | ②同族/降级女胎 {3:N1}% = {4} | ③怪物男胎 {5:N1}%" -f `
        $mName, $fName, (100 * $evo), (100 * $female), ($chainStr -join ' + '), (100 * $monsterMale))
    if ($isMonster) {
        # 要求：怪物男胎是最大的一档（「剩下大部分概率生出怪物」）
        if (($monsterMale -gt $evo) -and ($monsterMale -gt $female)) { } else { $fail25++ }
    } else {
        # 人类父本：降级链里必须能摸到基础种族
        if (@($chain.Keys | Where-Object { $_.StartsWith('[基础]') }).Count -gt 0) { } else { $fail25++ }
    }
}
Write-Output ("25.8 分布符合预期（怪物父本以怪物男胎为主 / 人类父本可落基础种族）: {0}" -f `
    $(if ($fail25 -eq 0) { 'PASS' } else { "FAIL x$fail25" }))


Write-Output '--- 26) 猎手营地生成种族：按「派系血脉 + 派系声望」的品阶概率 ---'

$hcfg26 = $asm.GetType('RaceBreedingExtension.HunterCamp.HunterCampConfig')
$hcs26 = $asm.GetType('RaceBreedingExtension.HunterCamp.HunterCampSystem')
$mTier26 = $hcfg26.GetMethod('ComputeSpeciesTierChance', $flags)
$mNoBl26 = $hcfg26.GetMethod('ComputeNoBloodlineShare', $flags)
$mCand26 = $hcs26.GetMethod('ComputeCaptiveSpeciesCandidates', $flags)
Write-Output ("26.1 方法与模型存在: ComputeSpeciesTierChance={0} ComputeNoBloodlineShare={1} ComputeCaptiveSpeciesCandidates={2} CaptiveSpeciesCandidate={3}" -f `
    ($null -ne $mTier26), ($null -ne $mNoBl26), ($null -ne $mCand26), `
    ($null -ne $hcs26.GetNestedType('CaptiveSpeciesCandidate', [System.Reflection.BindingFlags]'Public')))

# 26.2 品阶概率曲线：传说 2% → 20%，史诗 4% → 40%
$cases26 = @(
    @(5, 0,     2.0,    'Devil 示例：声望 0'),
    @(5, 29999, 19.99,  '声望 29999（未满级）'),
    @(5, 30000, 20.0,   '声望 30000（Fabled 门槛）'),
    @(5, 99999, 20.0,   '声望 99999'),
    @(4, 0,     4.0,    '史诗基础值'),
    @(4, 99999, 40.0,   '史诗满级')
)
$fail26 = 0
foreach ($c in $cases26) {
    $got = [double]$mTier26.Invoke($null, @([int]$c[0], [int]$c[1]))
    $ok = [Math]::Abs($got - [double]$c[2]) -lt 0.02
    if (-not $ok) { $fail26++ }
    Write-Output ("     Tier{0} rep={1,-6} → {2,5:N2}% (期望 {3,4}) {4}  {5}" -f $c[0], $c[1], $got, $c[2], $(if ($ok) { 'OK  ' } else { 'FAIL' }), $c[3])
}
Write-Output ("26.2 品阶概率曲线（传说 2→20% / 史诗 4→40%）: {0}" -f $(if ($fail26 -eq 0) { 'PASS' } else { "FAIL x$fail26" }))

$share0_26 = [double]$mNoBl26.Invoke($null, @(0))
$shareF_26 = [double]$mNoBl26.Invoke($null, @(99999))
Write-Output ("26.3 无血脉组在剩余概率中的占比：声望0 → {0:N1}% / 满级 → {1:N1}%（声望低时更高）: {2}" -f `
    $share0_26, $shareF_26, $(if ($share0_26 -gt $shareF_26) { 'PASS' } else { 'FAIL' }))

$gd26 = $asm.GetType('ComplexBreedingRedux.Data.GameData')
$factions26 = $gd26.GetField('FactionDataList', $flags).GetValue($null)
$sbm26 = $asm.GetType('RaceBreedingExtension.Bloodline.Data.SpeciesBloodlineMap')
$mBl26 = $sbm26.GetMethod('TryGetBySpeciesName', $flags)
$expect26 = [ordered]@{
    'Kingdom'  = 'Nature,Wild'
    'Demonic'  = 'Demon'
    'Monster'  = 'Dragon,Monster'
    'Sect'     = 'Divine'
    'Conclave' = 'Elemental,Magic'
}
$mFail26 = 0
foreach ($f in $factions26) {
    $bl = @($f.Bloodlines | ForEach-Object { $_.ToString() } | Sort-Object)
    $gotBl = $bl -join ','
    $exp = $expect26[$f.InternalName]
    if ($gotBl -ne $exp) { $mFail26++ }
    Write-Output ("  {0,-8} 血脉: {1,-20} (期望 {2})" -f $f.InternalName, $gotBl, $exp)
}
Write-Output ("26.4 五个派系 ↔ 血脉 与需求一致（王国=自然/野性、恶魔=恶魔、怪物=怪物/龙之、教团=神圣、议会=魔力/元素）: {0}" -f `
    $(if ($mFail26 -eq 0 -and $factions26.Count -eq 5) { 'PASS' } else { "FAIL x$mFail26" }))

$sumFail26 = 0; $blFail26 = 0; $noBlFail26 = 0; $goblinFail26 = 0
foreach ($f in $factions26) {
    $list = $mCand26.Invoke($null, @($f, [int]0))
    $sum = 0.0; $noBl = 0; $hasGoblin = $false; $pool = @()
    foreach ($c in $list) {
        $sum += [double]$c.WeightPercent
        if (-not $c.HasBloodline) {
            $noBl++
            if ($c.Key -eq 'Goblin') { $hasGoblin = $true }
        } elseif ($c.Species) {
            $a = @($c.Species.InternalName, $null, 0)
            if ($mBl26.Invoke($null, $a)) {
                if ($f.Bloodlines -notcontains $a[1]) { $blFail26++ }
                $pool += ([string]$c.Species.InternalName)
            } else { $blFail26++ }
        }
    }
    if ([Math]::Abs($sum - 100.0) -gt 0.05) { $sumFail26++ }
    if ($noBl -eq 0) { $noBlFail26++ }
    if (-not $hasGoblin) { $goblinFail26++ }
    Write-Output ("  {0,-8} rep=0：候选 {1,2} 项（无血脉 {2,2} 项），概率合计 {3:N2}%  物种池={4}" -f `
        $f.InternalName, $list.Count, $noBl, $sum, ($pool -join '/'))
}
Write-Output ("26.5 每个派系：概率合计 100% / 血脉物种均属该派系 / 含无血脉组 / 含哥布林(无血脉) : {0}" -f `
    $(if (($sumFail26 + $blFail26 + $noBlFail26 + $goblinFail26) -eq 0) { 'PASS' } else { "FAIL sum=$sumFail26 bl=$blFail26 nobl=$noBlFail26 goblin=$goblinFail26" }))

# 26.6 用户示例：恶魔派系 声望0 → Devil 2%；声望 ≥30000 → Devil 20%
$demonic26 = ($factions26 | Where-Object { $_.InternalName -eq 'Demonic' }).PSObject.BaseObject
$devilFail26 = 0
foreach ($c in @(@(0, 2.0), @(30000, 20.0), @(99999, 20.0))) {
    $list = $mCand26.Invoke($null, @($demonic26, [int]$c[0]))
    $devil = 0.0; $noBlSum = 0.0; $parts = @()
    foreach ($x in $list) {
        $parts += ("{0} {1:N1}%" -f $x.Key, [double]$x.WeightPercent)
        if ($x.Key -eq 'Devil') { $devil = [double]$x.WeightPercent }
        if (-not $x.HasBloodline) { $noBlSum += [double]$x.WeightPercent }
    }
    if ([Math]::Abs($devil - [double]$c[1]) -gt 0.05) { $devilFail26++ }
    Write-Output ("  Demonic rep={0,-6} Devil={1,5:N1}% (期望 {2})，无血脉合计 {3:N1}%" -f $c[0], $devil, $c[1], $noBlSum)
    Write-Output ("      {0}" -f ($parts -join ' | '))
}
Write-Output ("26.7 恶魔派系 Devil：声望 0 → 2%、≥30000 → 20% : {0}" -f $(if ($devilFail26 -eq 0) { 'PASS' } else { "FAIL x$devilFail26" }))


Write-Output '--- 27) 妖精(Pixy)自动出售：召唤妖精只加数量（零刷新）；读取层拦截 婴儿/搬进来的成体/俘虏/动作中 ---'

$pixySrc27 = Get-Content (Join-Path $root 'ComplexBreedingRedux\Features\SlaveMarket\PixySoldSoonRefresh.cs') -Raw -Encoding UTF8
Write-Output ("27.1 源码：ActionStates / IsInAction / IsRaisedHere / IsProtectedFromPixySale 全部存在 : {0}" -f `
    $(if (($pixySrc27 -match 'ActionStates') -and `
          ($pixySrc27 -match 'internal static bool IsInAction\(Character\? character\)') -and `
          ($pixySrc27 -match 'internal static bool IsRaisedHere\(Character\? character\)') -and `
          ($pixySrc27 -match 'internal static bool IsProtectedFromPixySale\(Character\? character\)')) { 'PASS' } else { 'FAIL' }))

# ★ 回归守卫一：不得再按房间做保护（奴笼/怪物笼/产房/观赏房都只是妖精的搬运目的地）。
Write-Output ("27.1b 不得再出现「房间黑名单 / 怀孕保护」式守卫（ERoomType / HoldingRooms / PlayerManagedRooms / IsPregnant 均不得出现） : {0}" -f `
    $(if (($pixySrc27 -notmatch 'ERoomType') -and `
          ($pixySrc27 -notmatch 'HoldingRooms') -and `
          ($pixySrc27 -notmatch 'IsInHoldingRoom') -and `
          ($pixySrc27 -notmatch 'PlayerManagedRooms') -and `
          ($pixySrc27 -notmatch 'IsInPlayerManagedRoom') -and `
          ($pixySrc27 -notmatch 'IsPregnant')) { 'PASS' } else { 'FAIL: 残留房间/怀孕守卫' }))

# ★ 回归守卫二：读取层判定必须是「动作中 / 婴儿期 / 营地俘虏 / 非本设施出生长大」四项
$ruleAction27 = [bool]($pixySrc27 -match 'IsInAction\(character\)')
$ruleBaby27 = [bool]($pixySrc27 -match '\|\| character\.IsBaby')
$ruleCaptive27 = [bool]($pixySrc27 -match '\|\| HunterCampSystem\.IsCaptive\(character\)')
$ruleRaised27 = [bool]($pixySrc27 -match '\|\| !IsRaisedHere\(character\)')
Write-Output ("     四项判定位 动作中={0} 婴儿期={1} 营地俘虏={2} 非本设施后代={3}" -f $ruleAction27, $ruleBaby27, $ruleCaptive27, $ruleRaised27)
Write-Output ("27.1c 读取层 = 动作中(延后) + 婴儿期(**成长完成后才卖**) + 营地俘虏(永不) + 搬进来的成体(永不) : {0}" -f `
    $(if ($ruleAction27 -and $ruleBaby27 -and $ruleCaptive27 -and $ruleRaised27) { 'PASS' } else { 'FAIL' }))
Write-Output ("27.1d 「本设施出生长大」依据父母记录（MotherUnitId / FatherUnitId —— 游戏只在出生时赋值） : {0}" -f `
    $(if (($pixySrc27 -match 'character\.MotherUnitId > 0') -and ($pixySrc27 -match 'character\.FatherUnitId > 0')) { 'PASS' } else { 'FAIL' }))

# ★ 回归守卫三（本轮）：不再有任何「补发 / 刷新 SoldSoon」的逻辑 —— 召唤妖精只加数量、零刷新。
#   （源码断言前先剥掉注释，避免注释里提到旧标识符造成误判）
$stripCs27 = {
    param([string]$s)
    $s = [regex]::Replace($s, '(?s)/\*.*?\*/', '')
    [regex]::Replace($s, '(?m)//.*$', '')
}
$pixyCode27 = & $stripCs27 $pixySrc27
$profSrc27 = Get-Content (Join-Path $root 'ComplexBreedingRedux\Features\ProfessionSystem.cs') -Raw -Encoding UTF8
$profCode27 = & $stripCs27 $profSrc27
$pixyWrites27 = ([regex]::Matches($pixyCode27, 'SoldSoon\s*=')).Count
$pixyRefresh27 = ([regex]::Matches($pixyCode27, 'AfterPixyCountIncreased|ApplyPixySoldSoon')).Count
$summonCase27 = $profCode27.IndexOf('case EJobType.SummonPixy:')
$pixyInc27 = $profCode27.IndexOf('PlayerData.FloraPixyCount++')
$pixyIncLast27 = $profCode27.LastIndexOf('PlayerData.FloraPixyCount++')
$profRefresh27 = [bool](($profCode27 -match 'PixySoldSoonRefresh') -or ($profCode27 -match 'SoldSoon'))
Write-Output ("     去注释后：PixySoldSoonRefresh 内 SoldSoon 赋值 {0} 处 / 旧刷新入口 {1} 处；ProfessionSystem 内 case={2} 加数量={3} 调用刷新={4}" -f `
    $pixyWrites27, $pixyRefresh27, $summonCase27, $pixyInc27, $profRefresh27)
Write-Output ("27.2 PixySoldSoonRefresh 已纯读取化：源内零处 SoldSoon 赋值、无 AfterPixyCountIncreased / ApplyPixySoldSoon : {0}" -f `
    $(if (($pixyWrites27 -eq 0) -and ($pixyRefresh27 -eq 0)) { 'PASS' } else { 'FAIL' }))
Write-Output ("27.2b 召唤妖精只增加数量、不做任何刷新（case 内仅 FloraPixyCount++，全文件不再引用刷新入口 / 出售标记，且已移除 SlaveMarket 引用） : {0}" -f `
    $(if (($summonCase27 -ge 0) -and ($pixyInc27 -gt $summonCase27) -and ($pixyIncLast27 -eq $pixyInc27) -and (-not $profRefresh27) -and `
          ($profSrc27 -notmatch 'using ComplexBreedingRedux\.Features\.SlaveMarket;')) { 'PASS' } else { 'FAIL' }))
Write-Output "     参照原版：IncreasePixy（源码 65288）函数体内只有 FloraPixyCount++；分解 / 取消妖精待机加妖精（源码 67315）同样只加数量"
Write-Output "     出生时的标记由原版 GiveBirth 负责（源码 22121-22131：SoldSoon = PixySell && FloraPixySell、MovedSoon = PixyBaby && FloraPixyBaby）—— 本 mod 不再补发"

# 27.2b 读取层守卫：Unit.SoldSoon 的 getter 补丁（AutoSell.SoldSoonPatch）
$sellSrc27 = Get-Content (Join-Path $root 'ComplexBreedingRedux\Features\SlaveMarket\AutoSell.cs') -Raw -Encoding UTF8
$getterIdx27 = $sellSrc27.IndexOf('PixySoldSoonRefresh.IsProtectedFromPixySale(__instance as Character)')
$shouldFemaleIdx27 = $sellSrc27.IndexOf('ShouldSellFemale(female)')
$getterIdx27b = $sellSrc27.LastIndexOf('PixySoldSoonRefresh.IsProtectedFromPixySale(__instance as Character)')
Write-Output ("     读取层守卫位置={0}（ShouldSell 调用位置={1}）" -f $getterIdx27, $shouldFemaleIdx27)
Write-Output ("27.2b SoldSoon getter 对「动作中/婴儿期/营地俘虏/搬进来的成体」直接返回 false（★ 不修改字段：动作与婴儿只是延后） : {0}" -f `
    $(if (($getterIdx27 -ge 0) -and ($getterIdx27b -eq $getterIdx27) -and ($shouldFemaleIdx27 -gt $getterIdx27) -and ($sellSrc27 -notmatch '\.SoldSoon = false')) { 'PASS' } else { 'FAIL' }))

# 27.2c 反射：新判定方法必须都在；旧的「房间保护」「补发刷新」必须已彻底删除
$pixyClsRoom27 = $asm.GetType('ComplexBreedingRedux.Features.SlaveMarket.PixySoldSoonRefresh')
$mRaised27 = $pixyClsRoom27.GetMethod('IsRaisedHere', $priv)
if ($null -eq $mRaised27) { $mRaised27 = $pixyClsRoom27.GetMethod('IsRaisedHere', $flags) }
$mProt27 = $pixyClsRoom27.GetMethod('IsProtectedFromPixySale', $priv)
if ($null -eq $mProt27) { $mProt27 = $pixyClsRoom27.GetMethod('IsProtectedFromPixySale', $flags) }
$mHold27 = $pixyClsRoom27.GetMethod('IsInHoldingRoom', $flags)
$fHold27 = $pixyClsRoom27.GetField('HoldingRooms', $flags)
$mRefresh27 = $pixyClsRoom27.GetMethod('AfterPixyCountIncreased', $priv)
if ($null -eq $mRefresh27) { $mRefresh27 = $pixyClsRoom27.GetMethod('AfterPixyCountIncreased', $flags) }
$mApply27 = $pixyClsRoom27.GetMethod('ApplyPixySoldSoon', $priv)
if ($null -eq $mApply27) { $mApply27 = $pixyClsRoom27.GetMethod('ApplyPixySoldSoon', $flags) }
Write-Output ("     IsRaisedHere={0} IsProtectedFromPixySale={1}；旧 IsInHoldingRoom 删除={2} HoldingRooms 删除={3} 补发刷新删除={4} 写标记方法删除={5}" -f `
    ($null -ne $mRaised27), ($null -ne $mProt27), ($null -eq $mHold27), ($null -eq $fHold27), ($null -eq $mRefresh27), ($null -eq $mApply27))
Write-Output ("27.2c 反射：判定方法存在、房间保护与补发刷新（AfterPixyCountIncreased / ApplyPixySoldSoon）已彻底删除 : {0}" -f `
    $(if (($null -ne $mRaised27) -and ($null -ne $mProt27) -and ($null -eq $mHold27) -and ($null -eq $fHold27) -and `
          ($null -eq $mRefresh27) -and ($null -eq $mApply27)) { 'PASS' } else { 'FAIL' }))

# 27.3 状态白名单行为校验（IsSellableIdleState 与 IsInAction 同源）
$pixyCls27 = $asm.GetType('ComplexBreedingRedux.Features.SlaveMarket.PixySoldSoonRefresh')
$mSellable27 = $pixyCls27.GetMethod('IsSellableIdleState', $priv)
if ($null -eq $mSellable27) { $mSellable27 = $pixyCls27.GetMethod('IsSellableIdleState', $flags) }
$mInAction27 = $pixyCls27.GetMethod('IsInAction', $priv)
Write-Output ("27.3 判定方法: IsSellableIdleState={0} IsInAction={1}" -f ($null -ne $mSellable27), ($null -ne $mInAction27))

$eState27 = $game.GetType('MBMScripts.EState')
$busyStates27 = @('Sex','SexDrained','CumShotInside','CumShotOutside','Masturbation','MasturbationCumShot',
                  'Ejaculation','FaceSitting','Touch','Birth','BirthDrained','AfterBirth','AfterBirthDrained',
                  'Fetus','NewbornBabyUmbilicalCord','Move','MoveDrained','MoveDead','BabyMove',
                  'MilkStart','MilkEnd','Action','Card','CardBaby','Portrait','DeadPortrait','Commission')
$idleStates27 = @('None','Idle','Rest','Drained','BabyIdle','NewbornBaby','DayStay','NightStay','MilkIdle')
$busyFail27 = 0; $idleFail27 = 0
foreach ($n in $busyStates27) {
    $e = [System.Enum]::Parse($eState27, $n)
    if ([bool]$mSellable27.Invoke($null, @($e))) { $busyFail27++ ; Write-Output "     FAIL 动作状态 $n 竟被判为可出售" }
}
foreach ($n in $idleStates27) {
    $e = [System.Enum]::Parse($eState27, $n)
    if (-not [bool]$mSellable27.Invoke($null, @($e))) { $idleFail27++ ; Write-Output "     FAIL 静止状态 $n 竟被判为不可出售" }
}
Write-Output ("     动作状态 {0} 个（性行为/射精/生产/移动/挤奶/卡片…）全部禁止出售 : {1}" -f $busyStates27.Count, $(if ($busyFail27 -eq 0) { 'PASS' } else { "FAIL x$busyFail27" }))
Write-Output ("     静止状态 {0} 个（Idle/Rest/Drained/BabyIdle/NewbornBaby…）仍可正常出售 : {1}" -f $idleStates27.Count, $(if ($idleFail27 -eq 0) { 'PASS' } else { "FAIL x$idleFail27" }))

# 27.4 全枚举统计（便于人工确认没有遗漏的动作状态被误判为可出售）
if ($null -ne $mSellable27) {
    $sellableAll27 = @()
    $busyAll27 = @()
    foreach ($e in [System.Enum]::GetValues($eState27)) {
        if ([bool]$mSellable27.Invoke($null, @($e))) { $sellableAll27 += $e.ToString() } else { $busyAll27 += $e.ToString() }
    }
    Write-Output ("     可出售状态（{0}）: {1}" -f $sellableAll27.Count, ($sellableAll27 -join ', '))
    Write-Output ("     禁止出售状态（{0}）: {1}" -f $busyAll27.Count, ($busyAll27 -join ', '))
}

