#nullable enable
using System;
using System.Collections.Generic;
using ComplexBreedingRedux.Features;
using MBMScripts;
using RaceBreedingExtension.Bloodline.Data;

namespace ComplexBreedingRedux.Data.Factions;

/// <summary>
/// 派系数据。派系取代了原版“访客职业/阶级”机制，改为“品阶（1~5，与职业品阶同步）”。
/// 访客携带派系身份（DataType == Id）与该派系允许的血脉（<see cref="Bloodlines"/>），
/// 不再拥有任何职业/精华/自定义偏好。
/// </summary>
internal sealed class FactionData
{
    public FactionData(
        string internalName,
        string defaultName,
        int id,
        string colorHex,
        int venerealDiseaseChance,
        EFactionReputation unlockReputation)
    {
        InternalName = internalName;
        Name = defaultName;
        Id = id;
        ColorHex = colorHex;
        VenerealDiseaseChance = venerealDiseaseChance;
        UnlockReputation = unlockReputation;
    }

    /// <summary>稳定的英文内部名（本地化资源键 = "Faction_" + InternalName + "_Name"）。</summary>
    public string InternalName { get; }

    /// <summary>本地化后的派系显示名。</summary>
    public string Name { get; set; }

    /// <summary>派系 Id，同时作为访客（Client）的 DataType（0-4，与游戏内置 5 类访客槽位一致）。</summary>
    public int Id { get; }

    /// <summary>访客 UI 中显示的派系颜色。</summary>
    public string ColorHex { get; }

    /// <summary>性病感染几率（%）。</summary>
    public int VenerealDiseaseChance { get; }

    /// <summary>解锁所需的“任一派系”最低综合声誉等级（Unknown = 始终可用）。</summary>
    public EFactionReputation UnlockReputation { get; }

    /// <summary>
    /// 该派系访客允许携带的血脉（生成时随机取其一；为空表示不带血脉）。
    /// 等级只由「模组派系声誉等级」决定：
    ///   Unknown 1-3 / Obscure 1-4 / Insignificant 1-5 / Noteworthy 1-6 /
    ///   Reputable 1-7 / Significant 1-8 / Famous 1-9 / Fabled 1-10。
    /// ★ 不参考玩家原版声望等级，也不受访客品阶影响。见 FactionSystem.RollVisitorBloodlineLevel。
    /// </summary>
    public IReadOnlyList<EBloodline> Bloodlines { get; set; } = Array.Empty<EBloodline>();

    /// <summary>
    /// 是否已解锁：当任一派系的综合声誉等级 ≥ UnlockReputation 时解锁。
    /// 综合声誉 = max(该派系隐藏声望等级, 玩家原版可见声望等级)，因此声望提升必然能解锁后续派系。
    /// </summary>
    public bool IsUnlocked()
    {
        if (UnlockReputation == EFactionReputation.Unknown)
            return true;
        return FactionSystem.GetMaxEffectiveReputationIndex() >= (int)UnlockReputation;
    }
}
