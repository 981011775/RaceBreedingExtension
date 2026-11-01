#nullable enable
using System;
using System.Collections.Generic;
using MBMScripts;
using RaceBreedingExtension.Bloodline.Data;
using UnityEngine;

namespace RaceBreedingExtension.Bloodline;

/// <summary>
/// 血脉「每级效果」通过写入原版特质槽位实现（与游戏原生特质效果完全一致，
/// 最终是否生效、以及各属性上下限一律交由原版游戏逻辑决定）：
///   ETrait.Trait93  → ConceptionRate           生育率
///   ETrait.Trait94  → GrowthTime               婴儿成长时间
///   ETrait.Trait95  → FoodConsumption          维护费用（Maintenance）
///   ETrait.Trait99  → SexTime                  交配时间
///   ETrait.Trait100 → DecreasingMentalityOnSex 体力消耗量
///
/// 女性奴隶与怪物（雄性）都会写入同样的数值（是否生效看原版逻辑）。
/// ★ 平衡调整（改每级数值）：数值写进角色后会随存档保存，旧存档角色不会自动更新，
///   需由 <see cref="RealignToCurrentVersion"/> 按 <see cref="BloodlineStatHistory"/>
///   记录的旧数值换算（版本号见 <see cref="BloodlineSystem.StatVersion"/>）。
/// 例外：防护结界（DepravedShield）是模组自有系统，仅女性生效，
/// 在 SexProgressManager.GetDepravedShield 中以 ShieldPerLevel × 等级 叠加，不写特质槽位。
///
/// ★ 重要：数值写入（AddTraitValue）是效果的唯一来源，必须优先且独立完成；
///   「登记进特质列表（AddTrait）+ 排序」只是让这些属性像原生母乳一样显示/可继承，
///   放在数值写入之后并整体 try/catch —— 即使游戏特质列表状态异常，
///   也绝不会中断数值写入，更不会让调用方（繁殖/初始化流程）抛出异常。
/// </summary>
internal static class BloodlineVanillaStats
{
    /// <summary>生育率槽位（原版 ConceptionRate getter 叠加该特质值）。</summary>
    public const ETrait ConceptionRateTrait = ETrait.Trait93;

    /// <summary>成长时间槽位（原版 GrowthTime getter 叠加该特质值）。</summary>
    public const ETrait GrowthTimeTrait = ETrait.Trait94;

    /// <summary>维护费用槽位（原版 Maintenance getter 叠加该特质值）。</summary>
    public const ETrait FoodConsumptionTrait = ETrait.Trait95;

    /// <summary>交配时间槽位（原版 SexTime getter 叠加该特质值）。</summary>
    public const ETrait SexTimeTrait = ETrait.Trait99;

    /// <summary>体力消耗量槽位（原版 DecreasingMentalityOnSex getter 叠加该特质值）。</summary>
    public const ETrait MentalityCostTrait = ETrait.Trait100;

    /// <summary>
    /// 写入（sign = +1）或移除（sign = -1）某个血脉等级对应的原版属性数值。
    /// 等级变化时必须先按旧等级移除、再按新等级写入，避免重复叠加。
    /// </summary>
    public static void Apply(Character? character, BloodlineData? data, int level, int sign)
    {
        if (character == null || data == null || level <= 0 || sign == 0)
            return;

        ApplyValues(character, data, level, sign,
            data.ConceptionRatePerLevel, data.GrowthTimePerLevel, data.FoodConsumptionPerLevel,
            data.SexTimePerLevel, data.MentalityCostPerLevel);
    }

    /// <summary>
    /// 旧存档数值迁移：把角色身上「按旧版每级数值写入」的加成换算成当前版本的数值。
    /// 写入量 =（当前每级数值 − 旧版每级数值）× 等级，因此只对真正变化过的项生效。
    /// <paramref name="appliedVersion"/> = 该角色上次写入数值时的版本
    /// （0 / 1 = 平衡调整前的 v1）；取不到旧版本数值时不做任何改动。
    /// 由 <see cref="BloodlineSystem.EnsureStatVersion"/> 保证每个角色只执行一次。
    /// </summary>
    public static void RealignToCurrentVersion(Character? character, BloodlineData? data, int level, int appliedVersion)
    {
        if (character == null || data == null || level <= 0)
            return;

        if (!BloodlineStatHistory.TryGetPerLevel(data.Id, appliedVersion, out BloodlineStatHistory.PerLevel old))
            return;

        ApplyValues(character, data, level, +1,
            data.ConceptionRatePerLevel - old.ConceptionRate,
            data.GrowthTimePerLevel - old.GrowthTime,
            data.FoodConsumptionPerLevel - old.FoodConsumption,
            data.SexTimePerLevel - old.SexTime,
            data.MentalityCostPerLevel - old.MentalityCost,
            registerTraits: false);
    }

    /// <summary>
    /// 数值写入本体：传入「每级数值」，实际写入量 = 每级数值 × 等级 × sign。
    /// （正常写入与旧存档迁移共用，区别只在传入的每级数值是「当前值」还是「新旧差值」。）
    /// <paramref name="registerTraits"/> = false 时只写数值、不动特质列表
    /// （旧存档迁移用：那些槽位本来就已登记过，且迁移可能发生在 UI 遍历特质列表的过程中）。
    /// </summary>
    private static void ApplyValues(
        Character character, BloodlineData data, int level, int sign,
        float conceptionPerLevel, float growthTimePerLevel, float foodPerLevel,
        float sexTimePerLevel, float mentalityPerLevel,
        bool registerTraits = true)
    {
        List<ETrait> touched = new List<ETrait>(5);

        // ── 1) 数值写入（效果本体，逐条独立保护） ──
        Add(character, touched, ConceptionRateTrait, conceptionPerLevel * level * sign);
        Add(character, touched, GrowthTimeTrait, growthTimePerLevel * level * sign);
        Add(character, touched, FoodConsumptionTrait, foodPerLevel * level * sign);
        Add(character, touched, SexTimeTrait, sexTimePerLevel * level * sign);
        Add(character, touched, MentalityCostTrait, mentalityPerLevel * level * sign);

        if (touched.Count == 0)
            return;

        // ── 2) 登记进特质列表 + 排序（仅为显示/继承一致性；失败不影响上面的数值） ──
        if (registerTraits)
        {
            try
            {
                foreach (ETrait trait in touched)
                {
                    if (!character.TraitContains(trait))
                        character.AddTrait(trait);
                }
                character.SortTrait();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Bloodline] 属性特质登记/排序失败（数值加成不受影响）：" + e.Message);
            }
        }

        LogDetail(character, data, level, sign, touched);
    }

    /// <summary>写入一条原版属性数值（效果本体）：单独 try/catch，任何异常都不得影响其它条目。</summary>
    private static void Add(Character character, List<ETrait> touched, ETrait trait, float delta)
    {
        if (delta == 0f)
            return;

        try
        {
            character.AddTraitValue(trait, delta);
            touched.Add(trait);
        }
        catch (Exception e)
        {
            Debug.LogError($"[Bloodline] AddTraitValue({trait}, {delta}) 失败：{e.Message}");
        }
    }

    /// <summary>诊断日志（需在模组菜单开启 EnableLogging，或调试模式）。</summary>
    private static void LogDetail(Character character, BloodlineData data, int level, int sign, List<ETrait> touched)
    {
        if (!DepravedShield.ModConfig.EnableLogging && !ComplexBreedingRedux.Utilities.GameManagerUtils.DEBUG_MODE)
            return;

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append($"[Bloodline] {character.DisplayName} {data.Name}+{level} {(sign > 0 ? "写入" : "移除")}属性：");
        foreach (ETrait trait in touched)
        {
            sb.Append(trait).Append('=').Append(character.GetTraitValue(trait).ToString("0.###")).Append("  ");
        }
        Debug.Log(sb.ToString());
    }
}

