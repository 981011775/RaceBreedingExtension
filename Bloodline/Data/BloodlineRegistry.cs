#nullable enable
using System.Collections.Generic;
using System.Linq;
using MBMScripts;

namespace RaceBreedingExtension.Bloodline.Data;

/// <summary>
/// 八种血脉的静态注册表（全部为「每级」数值，等级 1~10）。
///
/// 数值来源：
///   · 「种族特质新.txt」—— 原始设计（颜色、掉落物、怪物效果等）；
///   · 「血脉特质修正.txt」—— 平衡调整，只改动下表列出的数值，其余效果保持不变。
/// ★ 改动任何每级数值时必须同步两处，否则旧存档里的角色不会跟着变
///   （每级属性是写进原版特质槽位、随存档保存的，见 BloodlineVanillaStats）：
///   ① <see cref="BloodlineSystem.StatVersion"/> +1；
///   ② 在 <see cref="BloodlineStatHistory"/> 中补录「改动前的旧数值」。
///
/// 颜色：
///   恶魔 紫色 / 怪物 红色 / 野性 墨绿色 / 神圣 金色 /
///   魔力 蓝色 / 元素 青色 / 自然 葱绿色 / 龙之 橙色
/// </summary>
internal static class BloodlineRegistry
{
    /// <summary>恶魔血脉：被恶魔力量侵蚀的血脉。</summary>
    public static readonly BloodlineData Demon = new("Demon", EBloodline.Demon, "#8A2BE2")
    {
        // 堕落时高潮 5%/级 获得兽人奶（猫/狗）
        OrgasmItem = EItemType.FurryMilk,
        OrgasmRequiresDepraved = true,
        // 每级交配时间 +5s（平衡调整：原 +10s）
        SexTimePerLevel = 5,
        // 每级体力消耗量 +0.05（平衡调整：原 +0.1）
        MentalityCostPerLevel = 0.05f,
        // 怪物：与其性交的女性性爱进度速度 +0.01/级
        MonsterEffect = EMonsterBloodlineEffect.PartnerSexSpeed,
        MonsterEffectValue = 0.01f,
    };

    /// <summary>怪物血脉：被怪物力量强化的血脉。</summary>
    public static readonly BloodlineData Monster = new("Monster", EBloodline.Monster, "#E01B1B")
    {
        // 堕落时高潮 5%/级 获得小兽人奶（兔/羊）
        OrgasmItem = EItemType.SmallFurryMilk,
        OrgasmRequiresDepraved = true,
        // 每级交配时间 +5s（平衡调整：原 +10s）
        SexTimePerLevel = 5,
        // 每级维护费用 +25（平衡调整：原 +10）
        FoodConsumptionPerLevel = 25,
        // 怪物：cumshot 时女性防护结界 -20/级
        MonsterEffect = EMonsterBloodlineEffect.PartnerShieldDrain,
        MonsterEffectValue = 20f,
    };

    /// <summary>野性血脉：与野性力量融合的血脉。</summary>
    public static readonly BloodlineData Wild = new("Wild", EBloodline.Wild, "#145A32")
    {
        // 堕落时高潮 5%/级 获得矮人奶
        OrgasmItem = EItemType.DwarfMilk,
        OrgasmRequiresDepraved = true,
        // 每级体力消耗量 +0.05（平衡调整：原 +0.1）
        MentalityCostPerLevel = 0.05f,
        // 每级维护费用 +25（平衡调整：原 +10）
        FoodConsumptionPerLevel = 25,
        // 怪物：cumshot 时女性防护结界 -20/级
        MonsterEffect = EMonsterBloodlineEffect.PartnerShieldDrain,
        MonsterEffectValue = 20f,
    };

    /// <summary>神圣血脉：被神圣力量祝福的血脉。</summary>
    public static readonly BloodlineData Divine = new("Divine", EBloodline.Divine, "#FFD700")
    {
        // 女性每级获得 400 点防护结界
        ShieldPerLevel = 400,
        // 堕落时高潮 5%/级 获得特质升级药
        OrgasmItem = EItemType.Item_TraitUpgradePill,
        OrgasmRequiresDepraved = true,
        // 每级生育率 -50%（平衡调整：原 -100%）
        ConceptionRatePerLevel = -0.5f,
        // 怪物：cumshot 时女性有 1%/级 几率触发特质升级
        MonsterEffect = EMonsterBloodlineEffect.PartnerTraitUpgrade,
        MonsterEffectValue = 1f,
    };

    /// <summary>魔力血脉：被魔法力量侵蚀的血脉。</summary>
    public static readonly BloodlineData Magic = new("Magic", EBloodline.Magic, "#2E75D8")
    {
        // 堕落时高潮 5%/级 获得精灵奶
        OrgasmItem = EItemType.ElfMilk,
        OrgasmRequiresDepraved = true,
        // 每级维护费用 +50（平衡调整：原 +100）
        FoodConsumptionPerLevel = 50,
        // 女性每级获得 100 点防护结界
        ShieldPerLevel = 100,
        // 怪物：与其性交的女性性爱进度速度 +0.01/级
        MonsterEffect = EMonsterBloodlineEffect.PartnerSexSpeed,
        MonsterEffectValue = 0.01f,
    };

    /// <summary>元素血脉：与元素力量融合的血脉。</summary>
    public static readonly BloodlineData Elemental = new("Elemental", EBloodline.Elemental, "#00CED1")
    {
        // 堕落时高潮 5%/级 获得人类奶
        OrgasmItem = EItemType.HumanMilk,
        OrgasmRequiresDepraved = true,
        // 每级维护费用 +50（平衡调整：原 +100）
        FoodConsumptionPerLevel = 50,
        // 女性每级获得 100 点防护结界
        ShieldPerLevel = 100,
        // 怪物：cumshot 时女性有 5%/级 几率恢复 50% 体力
        MonsterEffect = EMonsterBloodlineEffect.PartnerStaminaRestore,
        MonsterEffectValue = 5f,
    };

    /// <summary>自然血脉：被自然力量祝福的血脉。</summary>
    public static readonly BloodlineData Nature = new("Nature", EBloodline.Nature, "#7CFC00")
    {
        // 堕落时高潮 5%/级 获得萝莉奶
        OrgasmItem = EItemType.LoliMilk,
        OrgasmRequiresDepraved = true,
        // 每级婴儿成长时间 +10（平衡调整：原 +15）
        GrowthTimePerLevel = 10,
        // 女性每级获得 200 点防护结界
        ShieldPerLevel = 200,
        // 怪物：cumshot 时女性有 5%/级 几率恢复 50% 体力
        MonsterEffect = EMonsterBloodlineEffect.PartnerStaminaRestore,
        MonsterEffectValue = 5f,
    };

    /// <summary>龙之血脉：被龙的力量强化的血脉。</summary>
    public static readonly BloodlineData Dragon = new("Dragon", EBloodline.Dragon, "#FF8C00")
    {
        // 堕落时高潮 5%/级 获得龙裔奶
        OrgasmItem = EItemType.DragonianMilk,
        OrgasmRequiresDepraved = true,
        // 每级生育率 -50%（平衡调整：原 -100%）
        ConceptionRatePerLevel = -0.5f,
        // 女性每级获得 400 点防护结界
        ShieldPerLevel = 400,
        // 怪物：cumshot 时场景中的女性有 3%/级 几率触发特质升级（平衡调整：原 1%/级）
        MonsterEffect = EMonsterBloodlineEffect.PartnerTraitUpgrade,
        MonsterEffectValue = 3f,
    };

    /// <summary>所有血脉（按 ID 升序）。</summary>
    public static IReadOnlyList<BloodlineData> All { get; } = new[]
    {
        Demon, Monster, Wild, Divine, Magic, Elemental, Nature, Dragon,
    };

    private static readonly Dictionary<EBloodline, BloodlineData> _byId = All.ToDictionary(x => x.Id);

    /// <summary>按 ID 查找（未注册返回 null）。</summary>
    public static BloodlineData? GetById(EBloodline id)
        => _byId.TryGetValue(id, out BloodlineData? data) ? data : null;
}
