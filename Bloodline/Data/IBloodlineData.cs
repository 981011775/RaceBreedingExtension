#nullable enable
using MBMScripts;

namespace RaceBreedingExtension.Bloodline.Data;

/// <summary>
/// 血脉（Bloodline）数据接口（参照 ComplexBreedingRedux 的精华 IEssenceData / 职业 IProfessionData）。
/// 所有数值均为「每级」数值，实际生效值 = 每级数值 × 血脉等级（等级 1~10）。
/// </summary>
internal interface IBloodlineData
{
    /// <summary>稳定的英文内部名（本地化资源键 = "Bloodline_" + InternalName + "_Name" / "_Desc"）。</summary>
    string InternalName { get; }

    /// <summary>血脉 ID。</summary>
    EBloodline Id { get; }

    /// <summary>UI 中该血脉的显示颜色（十六进制，例如 "#800080"）。</summary>
    string ColorHex { get; }

    /// <summary>本地化后的血脉名称（例如「恶魔血脉」）。</summary>
    string Name { get; set; }

    /// <summary>本地化后的血脉风味描述（例如「被恶魔力量侵蚀的血脉。」）。</summary>
    string Description { get; set; }

    /// <summary>每级交配时间 SexTime 增加量（秒）。</summary>
    int SexTimePerLevel { get; set; }

    /// <summary>每级体力消耗量 DecreasingMentalityOnSex 增加量。</summary>
    float MentalityCostPerLevel { get; set; }

    /// <summary>每级维护费用 FoodConsumption 增加量。</summary>
    int FoodConsumptionPerLevel { get; set; }

    /// <summary>每级婴儿成长时间 GrowthTime 增加量。</summary>
    int GrowthTimePerLevel { get; set; }

    /// <summary>每级生育率 ConceptionRate 变化量（负值 = 降低，-1 = -100%）。</summary>
    float ConceptionRatePerLevel { get; set; }

    /// <summary>每级防护结界 DepravedShield 增加量（仅女性生效，0 = 无）。</summary>
    int ShieldPerLevel { get; set; }

    /// <summary>堕落时高潮会获得的物品（null = 无）。</summary>
    EItemType? OrgasmItem { get; set; }

    /// <summary>获得物品是否要求角色处于堕落状态（InDepraved）。</summary>
    bool OrgasmRequiresDepraved { get; set; }

    /// <summary>怪物（雄性持有者）专属效果类型。</summary>
    EMonsterBloodlineEffect MonsterEffect { get; set; }

    /// <summary>怪物专属效果的每级数值（速度 / 护盾削减量 / 几率 %）。</summary>
    float MonsterEffectValue { get; set; }
}
