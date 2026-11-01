#nullable enable
using MBMScripts;

namespace RaceBreedingExtension.Bloodline.Data;

/// <summary>
/// 血脉数据类（参照 ComplexBreedingRedux 的 EssenceData / ProfessionData 写法）。
/// </summary>
internal sealed class BloodlineData : IBloodlineData
{
    public BloodlineData(string internalName, EBloodline id, string colorHex)
    {
        InternalName = internalName;
        Id = id;
        ColorHex = colorHex;
        Name = internalName;
        Description = string.Empty;
    }

    public string InternalName { get; }
    public EBloodline Id { get; }
    public string ColorHex { get; }
    public string Name { get; set; }
    public string Description { get; set; }
    public int SexTimePerLevel { get; set; }
    public float MentalityCostPerLevel { get; set; }
    public int FoodConsumptionPerLevel { get; set; }
    public int GrowthTimePerLevel { get; set; }
    public float ConceptionRatePerLevel { get; set; }
    public int ShieldPerLevel { get; set; }
    public EItemType? OrgasmItem { get; set; }
    public bool OrgasmRequiresDepraved { get; set; } = true;
    public EMonsterBloodlineEffect MonsterEffect { get; set; }
    public float MonsterEffectValue { get; set; }
}
