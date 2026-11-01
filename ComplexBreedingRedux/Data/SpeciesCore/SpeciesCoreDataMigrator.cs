#nullable enable
using System;
using System.Linq;
using ComplexBreedingRedux.Data.Species;
using MBMScripts;
using UnityEngine;

namespace ComplexBreedingRedux.Data.SpeciesCore;

internal static class SpeciesCoreDataMigrator
{
    public static void MigrateSpeciesCoreData(this ISpeciesData data, Type type)
    {
        ISpeciesCoreData speciesCoreData = GameData.SpeciesCoreDataList.First(x => x.GetReplacedType == type);
        speciesCoreData.SkinColors = data.SkinColors;
        speciesCoreData.EyeColorForMigration = data.EyeColor;
        speciesCoreData.HairColors = data.HairColors;
        speciesCoreData.HairColorMigration = data.HairColors is { Length: > 0 } ? data.HairColors[0] : null;
        speciesCoreData.HairColorMigrationPower = data.HairColorFocus;
        speciesCoreData.HairColorSameAsSkin = data.HairColorSameAsSkin;
        speciesCoreData.ScaleColors = data.ScaleColors;
        speciesCoreData.HornColors = data.HornColor.HasValue ? new Color[] { data.HornColor.Value } : null;
        speciesCoreData.Figure = data.Figure;
        speciesCoreData.EyeBallType = data.EyeBallType;
        speciesCoreData.EarType = data.EarType;
        speciesCoreData.EarHairType = data.EarHairType;
        speciesCoreData.HornType = data.HornType;
        speciesCoreData.HornDragonType = data.HornDragonType;
        speciesCoreData.TailType = data.TailType;
        speciesCoreData.TailDragonType = data.TailDragonType;
        speciesCoreData.TitsTypes = data.TitsType.HasValue ? new int[] { data.TitsType.Value } : new int[] { 2 };
        speciesCoreData.HairBodyType = data.HairBodyType;
        speciesCoreData.UpperBody = data.UpperBody;
        speciesCoreData.LowerBody = data.LowerBody;
        speciesCoreData.MilkQuality = data.MilkQuality;
        speciesCoreData.MilkProductionRate = data.MilkProductionRate;
        speciesCoreData.MaxMilkSizeChange = data.MaxMilkSizeChange;
        speciesCoreData.ConceptionRate = data.ConceptionRate;
        speciesCoreData.MaxHealth = data.MaxHealth;
        speciesCoreData.GrowthTime = data.GrowthTime;
        speciesCoreData.MaxBirthCount = data.MaxBirthCount;
        speciesCoreData.Maintenance = data.Maintenance;
        speciesCoreData.DecreasingMentalityOnPregnant = data.DecreasingMentalityOnPregnant;
        speciesCoreData.GuaranteedTraits = data.GuaranteedTraits;
    }
}
