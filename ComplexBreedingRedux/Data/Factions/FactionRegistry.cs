#nullable enable
using System.Collections.Generic;
using RaceBreedingExtension.Bloodline.Data;

namespace ComplexBreedingRedux.Data.Factions;

/// <summary>
/// 五个派系的静态注册表：
/// 佣兵公会→王国派系（Kingdom）、骑士团→恶魔派系（Demonic）、王室→怪物派系（Monster，已实装），
/// 保留宗教教团（Sect）与奥秘议会（Conclave）。
///
/// 各派系访客携带的血脉（等级只由该派系的「模组派系声誉等级」决定：
///   Unknown 1-3 / Obscure 1-4 / Insignificant 1-5 / Noteworthy 1-6 /
///   Reputable 1-7 / Significant 1-8 / Famous 1-9 / Fabled 1-10；
///   ★ 不参考原版声望等级、不受访客品阶影响）：
///   王国派系 KingdomFaction  → 自然血脉 / 野性血脉
///   恶魔派系 DemonicFaction  → 恶魔血脉
///   怪物派系 MonsterFaction  → 怪物血脉 / 龙之血脉
///   宗教教团 SectFaction     → 神圣血脉
///   奥秘议会 ConclaveFaction → 魔力血脉 / 元素血脉
/// </summary>
internal static class FactionRegistry
{
    public static readonly FactionData Kingdom = new(
        internalName: "Kingdom",
        defaultName: "Kingdom Faction",
        id: 0,
        colorHex: "#D4A017",
        venerealDiseaseChance: 20,
        unlockReputation: EFactionReputation.Unknown)
    {
        Bloodlines = new[] { EBloodline.Nature, EBloodline.Wild },
    };

    public static readonly FactionData Sect = new(
        internalName: "Sect",
        defaultName: "Religious Sect",
        id: 1,
        colorHex: "#9FA8B7",
        venerealDiseaseChance: 25,
        unlockReputation: EFactionReputation.Unknown)
    {
        Bloodlines = new[] { EBloodline.Divine },
    };

    public static readonly FactionData Demonic = new(
        internalName: "Demonic",
        defaultName: "Demonic Faction",
        id: 2,
        colorHex: "#C92A42",
        venerealDiseaseChance: 10,
        unlockReputation: EFactionReputation.Noteworthy)
    {
        Bloodlines = new[] { EBloodline.Demon },
    };

    public static readonly FactionData Conclave = new(
        internalName: "Conclave",
        defaultName: "Arcane Conclave",
        id: 3,
        colorHex: "#7B4FD8",
        venerealDiseaseChance: 15,
        unlockReputation: EFactionReputation.Insignificant)
    {
        Bloodlines = new[] { EBloodline.Magic, EBloodline.Elemental },
    };

    public static readonly FactionData Monster = new(
        internalName: "Monster",
        defaultName: "Monster Faction",
        id: 4,
        colorHex: "#3FA34D",
        venerealDiseaseChance: 5,
        unlockReputation: EFactionReputation.Reputable)
    {
        Bloodlines = new[] { EBloodline.Monster, EBloodline.Dragon },
    };

    public static IReadOnlyList<FactionData> All { get; } = new[]
    {
        Kingdom, Sect, Demonic, Conclave, Monster,
    };
}
