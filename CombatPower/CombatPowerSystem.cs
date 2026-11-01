#nullable enable
using ComplexBreedingRedux.Data;
using ComplexBreedingRedux.Data.Professions;
using ComplexBreedingRedux.Data.Species;
using ComplexBreedingRedux.Features;
using MBMScripts;
using RaceBreedingExtension.Bloodline;
using RaceBreedingExtension.Bloodline.Data;
using UnityEngine;

namespace RaceBreedingExtension.CombatPower;

/// <summary>
/// 战斗力（CombatPower）计算。
///
/// 奴隶（女性）：
///   战斗力 = 体力总量 × 种族稀有度系数 × 职业稀有度系数 × 血脉特质系数 × (1 + 职业战斗力加成)
///     - 体力总量      = MaxHealth
///     - 种族稀有度系数 = 物种品阶（1~5）→ 1.00/1.05/1.10/1.15/1.20（无物种按 1.00）
///     - 职业稀有度系数 = 职业品阶（1~5）→ 同上（无职业按 1.00）
///     - 血脉特质系数   = 1 + 0.1 × 血脉等级（无血脉 = 1.0）
///
/// 怪物（雄性）：
///   战斗力 = (Trait96 + 交配时间 + 交配每秒体力消耗 × 100)
///            × 怪物种族系数 × 血脉特质系数 × (1 + 职业战斗力加成)
///     - 三项均取正值（负值按 0 计）后相加
///     - 交配时间 = SexTime（已含 Trait99 / 血脉加成）
///     - 交配每秒体力消耗 = DecreasingMentalityOnSex（含 Trait100 / 血脉加成）
///     - 怪物种族系数：哥布林 1.00 / 兽人 1.05 / 狼人 1.10 / 牛头人 1.15 / 火蜥蜴 1.20，其它 1.00
/// </summary>
internal static class CombatPowerSystem
{
    /// <summary>通用的战斗力计算入口（女性 → 奴隶公式；雄性/其它 → 怪物公式）。</summary>
    public static int GetCombatPower(Character? character)
    {
        if (character == null || character.IsDead)
            return 0;

        float value = character is Female
            ? GetSlavePower(character)
            : GetMonsterPower(character);

        return Mathf.Max(0, Mathf.RoundToInt(value));
    }

    /// <summary>奴隶（女性）战斗力公式。</summary>
    public static float GetSlavePower(Character character)
    {
        float health = Mathf.Max(0f, character.MaxHealth);
        float raceCoef = GetRaceRarityCoefficient(character);
        float professionCoef = GetProfessionRarityCoefficient(character);
        float bloodlineCoef = GetBloodlineCoefficient(character);
        float professionBonus = 1f + GetProfessionPowerBonus(character);

        return health * raceCoef * professionCoef * bloodlineCoef * professionBonus;
    }

    /// <summary>怪物（雄性）战斗力公式。</summary>
    public static float GetMonsterPower(Character monster)
    {
        float trait96 = Mathf.Max(0f, monster.GetTraitValue(ETrait.Trait96));
        float sexTime = Mathf.Max(0f, monster.SexTime);
        float mentalityCost = Mathf.Max(0f, monster.DecreasingMentalityOnSex) * 100f;

        float baseValue = trait96 + sexTime + mentalityCost;
        float raceCoef = GetMonsterRaceCoefficient(monster.Race);
        float bloodlineCoef = GetBloodlineCoefficient(monster);
        float professionBonus = 1f + GetProfessionPowerBonus(monster);

        return baseValue * raceCoef * bloodlineCoef * professionBonus;
    }

    // ─── 系数 ─────────────────────────────────────────────────────────────────

    /// <summary>种族稀有度系数：女性取物种品阶（SpeciesIdentity → Tier），基础种族/未知物种按 1.00。</summary>
    public static float GetRaceRarityCoefficient(Character character)
    {
        int tier = 1;
        if (GameData.TryGetSpecies(character, out ISpeciesData? species) && species != null)
            tier = species.Tier;

        return GetRarityCoefficient(tier);
    }

    /// <summary>职业稀有度系数：取职业品阶（无职业 = 1.00）。</summary>
    public static float GetProfessionRarityCoefficient(Character character)
    {
        IProfessionData? profession = TryGetProfession(character);
        return profession == null ? 1.00f : GetRarityCoefficient(profession.Tier);
    }

    /// <summary>品阶（1~5）→ 稀有度系数（越界按 1.00）。</summary>
    public static float GetRarityCoefficient(int tier)
        => tier >= 1 && tier < CombatPowerConfig.RarityCoefficient.Length
            ? CombatPowerConfig.RarityCoefficient[tier]
            : CombatPowerConfig.RarityCoefficient[1];

    /// <summary>怪物种族系数（未登记种族 1.00）。</summary>
    public static float GetMonsterRaceCoefficient(ERace race)
        => CombatPowerConfig.MonsterRaceCoefficient.TryGetValue(race, out float coef)
            ? coef
            : CombatPowerConfig.DefaultMonsterRaceCoefficient;

    /// <summary>怪物种族的稀有度品阶（1~5，用于 UI 配色）。</summary>
    public static int GetMonsterRaceTier(ERace race)
        => CombatPowerConfig.MonsterRaceTier.TryGetValue(race, out int tier)
            ? tier
            : CombatPowerConfig.DefaultMonsterRaceTier;

    /// <summary>血脉特质系数 = 1 + 0.1 × 等级（无血脉 = 1.0）。</summary>
    public static float GetBloodlineCoefficient(Character character)
    {
        BloodlineData? data = BloodlineSystem.TryGetBloodline(character, out int level);
        return data == null || level <= 0
            ? 1f
            : 1f + CombatPowerConfig.BloodlineCoefficientPerLevel * level;
    }

    /// <summary>职业战斗力加成（0.10 = +10%；无职业 = 0）。</summary>
    public static float GetProfessionPowerBonus(Character character)
        => TryGetProfession(character)?.CombatPowerBonus ?? CombatPowerConfig.NoProfessionBonus;

    /// <summary>读取角色职业（无职业返回 null）。</summary>
    public static IProfessionData? TryGetProfession(Character? character)
    {
        if (character == null)
            return null;
        return GameData.TryGetProfession(character, out IProfessionData? profession) ? profession : null;
    }
}
