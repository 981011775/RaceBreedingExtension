#nullable enable
using System.Collections.Generic;

namespace RaceBreedingExtension.Bloodline.Data;

/// <summary>
/// 血脉「每级数值」的历史记录 —— 仅供旧存档数值迁移使用
/// （见 <see cref="BloodlineSystem.EnsureStatVersion"/> / <see cref="BloodlineVanillaStats.RealignToCurrentVersion"/>）。
///
/// 背景：血脉的每级属性是以「写入原版特质槽位（Trait93 生育率 / Trait94 成长时间 /
/// Trait95 维护费用 / Trait99 交配时间 / Trait100 体力消耗量）」的方式实现的，
/// 数值一旦写入就随存档保存在角色身上。因此调整平衡（修改每级数值）后，旧存档里的
/// 角色仍然带着「旧数值」的加成 —— 这里保留每个历史版本的每级数值，迁移时按
/// 「旧数值 × 等级」移除、再按「新数值 × 等级」写入，让平衡调整对旧存档立即生效。
///
/// ★ 以后每次修改 <see cref="BloodlineRegistry"/> 的每级数值：
///   1) 把改动前的数值作为新的一条历史记录补到下面；
///   2) 把 <see cref="BloodlineSystem.StatVersion"/> +1。
/// </summary>
internal static class BloodlineStatHistory
{
    /// <summary>
    /// 某个历史版本里「每级属性数值」的五个条目（只含会写进特质槽位的五项；
    /// 防护结界 / 堕落掉落物 / 怪物效果都不写槽位，因此不在此列）。
    /// </summary>
    internal readonly struct PerLevel
    {
        public PerLevel(int sexTime, float mentalityCost, int foodConsumption, int growthTime, float conceptionRate)
        {
            SexTime = sexTime;
            MentalityCost = mentalityCost;
            FoodConsumption = foodConsumption;
            GrowthTime = growthTime;
            ConceptionRate = conceptionRate;
        }

        /// <summary>每级交配时间（秒 → Trait99）。</summary>
        public int SexTime { get; }

        /// <summary>每级体力消耗量（→ Trait100）。</summary>
        public float MentalityCost { get; }

        /// <summary>每级维护费用（→ Trait95）。</summary>
        public int FoodConsumption { get; }

        /// <summary>每级婴儿成长时间（→ Trait94）。</summary>
        public int GrowthTime { get; }

        /// <summary>每级生育率变化量（比例 → Trait93）。</summary>
        public float ConceptionRate { get; }
    }

    /// <summary>
    /// v1（平衡调整前）的每级数值，与 BloodlineRegistry 调整前一一对应：
    /// 恶魔 交配 +10s / 体力 +0.1；怪物 交配 +10s / 维护 +10；
    /// 野性 体力 +0.1 / 维护 +10；神圣 生育率 -100%；魔力 维护 +100；
    /// 元素 维护 +100；自然 成长时间 +15；龙之 生育率 -100%。
    /// （0 表示该血脉在这个版本对这项没有加成。）
    /// </summary>
    private static readonly Dictionary<EBloodline, PerLevel> V1 = new Dictionary<EBloodline, PerLevel>
    {
        [EBloodline.Demon] = new PerLevel(sexTime: 10, mentalityCost: 0.1f, foodConsumption: 0, growthTime: 0, conceptionRate: 0f),
        [EBloodline.Monster] = new PerLevel(sexTime: 10, mentalityCost: 0f, foodConsumption: 10, growthTime: 0, conceptionRate: 0f),
        [EBloodline.Wild] = new PerLevel(sexTime: 0, mentalityCost: 0.1f, foodConsumption: 10, growthTime: 0, conceptionRate: 0f),
        [EBloodline.Divine] = new PerLevel(sexTime: 0, mentalityCost: 0f, foodConsumption: 0, growthTime: 0, conceptionRate: -1f),
        [EBloodline.Magic] = new PerLevel(sexTime: 0, mentalityCost: 0f, foodConsumption: 100, growthTime: 0, conceptionRate: 0f),
        [EBloodline.Elemental] = new PerLevel(sexTime: 0, mentalityCost: 0f, foodConsumption: 100, growthTime: 0, conceptionRate: 0f),
        [EBloodline.Nature] = new PerLevel(sexTime: 0, mentalityCost: 0f, foodConsumption: 0, growthTime: 15, conceptionRate: 0f),
        [EBloodline.Dragon] = new PerLevel(sexTime: 0, mentalityCost: 0f, foodConsumption: 0, growthTime: 0, conceptionRate: -1f),
    };

    /// <summary>
    /// 取「某个已应用数值版本」所对应的每级数值（用于把它从角色身上移除）。
    /// 版本 0 / 1 = 平衡调整前的 v1；版本 ≥ 2 = 当前版本（调用方无需迁移，返回 false）。
    /// </summary>
    public static bool TryGetPerLevel(EBloodline id, int appliedVersion, out PerLevel perLevel)
    {
        if (appliedVersion <= 1)
            return V1.TryGetValue(id, out perLevel);

        perLevel = default;
        return false;
    }
}
