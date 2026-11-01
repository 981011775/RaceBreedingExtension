#nullable enable
using System;
using System.Collections.Generic;
using MBMScripts;

namespace RaceBreedingExtension.Bloodline.Data;

/// <summary>
/// 物种 → 血脉映射表（取代原来 SpeciesRegistry 中的 HighRaceTraitId）。
/// 只用于「女性（雌性奴隶 / 进化物种）」：由 BloodlineSystem.ApplySpeciesBloodline 调用。
///
/// 规则（依据用户给出的进化链条说明）：
///   - 从进化种族（SpeciesRegistry 中的物种 InternalName）开始，按链条由低到高排列；
///   - 链条最低端的物种给「最低血脉等级 +1」，中段给 +3，顶端给 +5；
///   - 有些链条从低端直接获得，有些从中段直接获得（基础种族跳过，例如哥布林 Goblin）；
///   - 物种给出的等级是「最低等级」：角色若因遗传拥有同名血脉且等级更低，会被提升到该等级。
///
/// 怪物（雄性）不在本表中初始化血脉：其血脉只在「遗传时生成」
/// （BreedingSystem → BloodlineSystem.ApplyInheritedBloodline）。
/// </summary>
internal static class SpeciesBloodlineMap
{
    /// <summary>
    /// 物种 InternalName → (血脉, 最低等级)。
    /// 仅用于女性（雌性奴隶 / 进化物种）：由 BloodlineSystem.ApplySpeciesBloodline 调用。
    /// </summary>
    private static readonly Dictionary<string, (EBloodline Bloodline, int MinLevel)> BySpeciesName =
        new Dictionary<string, (EBloodline, int)>(StringComparer.OrdinalIgnoreCase)
    {
        // ─── 基础种族（原本无血脉，按需求补充）───
        { "Horse",         (EBloodline.Wild, 1) },     // 马 → 野性血脉+1
        { "Orc",           (EBloodline.Monster, 1) },  // 兽人 → 怪物血脉+1

        // ─── 天使链条（神圣）───
        { "Aasimar",       (EBloodline.Divine, 1) },   // 神裔
        { "Angel",         (EBloodline.Divine, 5) },   // 天使
        { "FallenAngel",   (EBloodline.Divine, 5) },   // 堕天使

        // ─── 食尸鬼链条（魔力）───
        { "Ghoul",         (EBloodline.Magic, 1) },    // 食尸鬼
        { "Dhampir",       (EBloodline.Magic, 3) },    // 半血裔
        { "Vampire",       (EBloodline.Magic, 5) },    // 吸血鬼

        // ─── 精灵链条（魔力）───
        { "Mermaid",       (EBloodline.Magic, 1) },    // 海精灵
        { "Drow",          (EBloodline.Magic, 1) },    // 黑暗精灵
        { "MoonElf",       (EBloodline.Magic, 5) },    // 月精灵

        // ─── 妖精（自然）───
        { "Fairy",         (EBloodline.Nature, 1) },   // 妖精

        // ─── 哥布林链条（怪物；跳过基础哥布林，从大地精开始）───
        { "Hobgoblin",     (EBloodline.Monster, 1) },  // 大地精
        { "Redcap",        (EBloodline.Monster, 3) },  // 红帽
        { "Bugbear",       (EBloodline.Monster, 5) },  // 熊地精

        // ─── 狐人链条（怪物）───
        { "Kitsune",       (EBloodline.Monster, 1) },  // 狐兽人

        // ─── 狼人链条（恶魔）───
        { "Werewolf",      (EBloodline.Demon, 1) },    // 狼人
        { "ArcticWerewolf",(EBloodline.Demon, 5) },    // 极地狼人

        // ─── 兔人链条（自然）───
        { "Wererabbit",        (EBloodline.Nature, 1) }, // 兔人
        { "MagicWererabbit",   (EBloodline.Nature, 5) }, // 魔法兔人

        // ─── 魅魔链条（恶魔；从猫又开始）───
        { "Nekomata",      (EBloodline.Demon, 1) },    // 猫又
        { "Weretiger",     (EBloodline.Demon, 3) },    // 虎人
        { "Succubus",      (EBloodline.Demon, 3) },    // 魅魔
        { "Devil",         (EBloodline.Demon, 5) },    // 恶魔

        // ─── 牛头人链条（怪物）───
        { "Minotaur",      (EBloodline.Monster, 1) },  // 牛头人
        { "Dairycow",      (EBloodline.Monster, 3) },  // 奶牛

        // ─── 史莱姆（野性）───
        { "Slime",         (EBloodline.Wild, 1) },     // 史莱姆

        // ─── 傀儡链条（元素）───
        { "Golem",         (EBloodline.Elemental, 1) },// 傀儡
        { "Genasi",        (EBloodline.Elemental, 3) },// 元素裔

        // ─── 巨龙链条（龙之）───
        { "Drake",         (EBloodline.Dragon, 1) },   // 亚龙
        { "Salamander",    (EBloodline.Dragon, 3) },   // 火蜥蜴
        { "Dracolich",     (EBloodline.Dragon, 5) },   // 龙巫妖
        { "Dragon",        (EBloodline.Dragon, 5) },   // 龙
    };

    /// <summary>按物种 InternalName 查找（未映射返回 false）。</summary>
    public static bool TryGetBySpeciesName(string? internalName, out EBloodline bloodline, out int minLevel)
    {
        bloodline = EBloodline.None;
        minLevel = 0;
        if (string.IsNullOrWhiteSpace(internalName))
            return false;
        if (!BySpeciesName.TryGetValue(internalName!, out (EBloodline Bloodline, int MinLevel) value))
            return false;
        bloodline = value.Bloodline;
        minLevel = value.MinLevel;
        return true;
    }
}
