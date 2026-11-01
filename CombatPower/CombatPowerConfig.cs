#nullable enable
using System.Collections.Generic;
using MBMScripts;

namespace RaceBreedingExtension.CombatPower;

/// <summary>
/// 战斗力（CombatPower）系数表与调参常量。
/// 数值全部来自用户规格：
///   奴隶战斗力 = 体力总量 × 种族稀有度系数 × 职业稀有度系数 × 血脉特质系数 × (1 + 职业加成)
///   怪物战斗力 = (Trait96 + 怪物交配时间 + 怪物交配每秒体力消耗×100)
///                × 怪物种族系数 × 血脉特质系数 × (1 + 职业加成)
/// </summary>
internal static class CombatPowerConfig
{
    /// <summary>
    /// 稀有度系数（索引 = 稀有度品阶 1~5）：
    ///   1 普通 Common 1.00 / 2 不凡 Uncommon 1.05 / 3 稀有 Rare 1.10 /
    ///   4 史诗 Epic 1.15 / 5 传说 Legendary 1.20
    /// 同时用于「种族稀有度系数」（奴隶：取物种品阶）与「职业稀有度系数」（取职业品阶）。
    /// </summary>
    public static readonly float[] RarityCoefficient = { 0f, 1.00f, 1.05f, 1.10f, 1.15f, 1.20f };

    /// <summary>
    /// 怪物种族系数（按 ERace）：哥布林 1.00 / 兽人 1.05 / 狼人 1.10 / 牛头人 1.15 / 火蜥蜴 1.20，
    /// 未列出的种族使用 <see cref="DefaultMonsterRaceCoefficient"/>（1.00）。
    /// </summary>
    public static readonly Dictionary<ERace, float> MonsterRaceCoefficient = new Dictionary<ERace, float>
    {
        { ERace.Goblin,     1.00f },
        { ERace.Orc,        1.05f },
        { ERace.Werewolf,   1.10f },
        { ERace.Minotaur,   1.15f },
        { ERace.Salamander, 1.20f },
    };

    /// <summary>未在 <see cref="MonsterRaceCoefficient"/> 中登记的种族使用的系数。</summary>
    public const float DefaultMonsterRaceCoefficient = 1.00f;

    /// <summary>
    /// 怪物种族的“稀有度品阶”（用于 UI 名称配色，与 <see cref="MonsterRaceCoefficient"/> 一一对应）：
    /// 哥布林 1 / 兽人 2 / 狼人 3 / 牛头人 4 / 火蜥蜴 5，未登记种族 1。
    /// </summary>
    public static readonly Dictionary<ERace, int> MonsterRaceTier = new Dictionary<ERace, int>
    {
        { ERace.Goblin,     1 },
        { ERace.Orc,        2 },
        { ERace.Werewolf,   3 },
        { ERace.Minotaur,   4 },
        { ERace.Salamander, 5 },
    };

    /// <summary>未登记种族的品阶（1 = 普通）。</summary>
    public const int DefaultMonsterRaceTier = 1;

    /// <summary>血脉特质系数：每 +1 级 +0.1（+1 → 1.1 … +10 → 2.0）；无血脉 = 1.0。</summary>
    public const float BloodlineCoefficientPerLevel = 0.1f;

    /// <summary>单体击杀：战斗力 ≥ 怪物 2 倍时的必杀几率（%）。</summary>
    public const int DoublePowerKillChance = 100;

    /// <summary>没有职业时的战斗力加成（0%）。</summary>
    public const float NoProfessionBonus = 0f;
}
