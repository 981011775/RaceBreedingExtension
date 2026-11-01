#nullable enable
using System.Collections.Generic;
using MBMScripts;
using UnityEngine;

namespace ComplexBreedingRedux.Data.SpeciesCore;

internal static class SpeciesCoreRegistry
{
    public static readonly SpeciesCoreData Elf = new("Elf", ERace.Elf, typeof(Elf))
    {
        SkinColors = [new Color(1f, 0.92f, 0.89f)],
        Figure = EFigure.Woman,
        EyeBallType = 1,
        EarType = 2,
        TitsTypes = [1, 2],
        UpperBody = 1,
        LowerBody = 1,
        HealthRegeneration = 1f,
        MilkQuality = 400,
        MilkProductionRate = 0.5f,
        MaxMilkSizeChange = 1f,
        ConceptionRate = 30,
        MaxHealth = 120,
        GrowthTime = 140,
        MaxBirthCount = 8,
        Maintenance = 200,
        DecreasingMentalityOnPregnant = 0.3f,
    };

    public static readonly SpeciesCoreData Gnome = new("Gnome", ERace.Dwarf, typeof(Dwarf))
    {
        SkinColors = [new Color(0.78f, 0.7f, 0.59f)],
        HairColorMigration = new Color(0.54f, 0.37f, 0.13f),
        HairColorMigrationPower = 0.6f,
        Figure = EFigure.Girl,
        EyeBallType = 1,
        EarType = 3,
        HairBodyType = 2,
        TitsTypes = [2, 3],
        UpperBody = 1,
        LowerBody = 2,
        HealthRegeneration = 1f,
        MilkQuality = 100,
        MilkProductionRate = 0.8f,
        MaxMilkSizeChange = 1f,
        ConceptionRate = 50,
        MaxHealth = 240,
        GrowthTime = 110,
        MaxBirthCount = 12,
        Maintenance = 200,
        DecreasingMentalityOnPregnant = 0.4f,
    };

    public static readonly SpeciesCoreData Human = new("Human", ERace.Human, typeof(Human))
    {
        SkinColors = [new Color(1f, 0.92f, 0.89f)],
        Figure = EFigure.Woman,
        EyeBallType = 1,
        EarType = 1,
        TitsTypes = [1, 2, 3],
        UpperBody = 1,
        LowerBody = 1,
        HealthRegeneration = 1f,
        MilkQuality = 200,
        MilkProductionRate = 0.8f,
        MaxMilkSizeChange = 1f,
        ConceptionRate = 50,
        MaxHealth = 120,
        GrowthTime = 90,
        MaxBirthCount = 8,
        Maintenance = 120,
        DecreasingMentalityOnPregnant = 0.4f,
    };

    public static readonly SpeciesCoreData Imp = new("Imp", ERace.Neko, typeof(Neko))
    {
        SkinColors = [new Color(1f, 1f, 1f)],
        HairColorMigration = new Color(0.33f, 0.24f, 0.13f),
        HairColorMigrationPower = 0.3f,
        Figure = EFigure.Woman,
        EyeBallType = 3,
        EarType = 4,
        EarHairType = 1,
        HornDragonType = 0,
        TailType = 1,
        HairBodyType = 1,
        TitsTypes = [0, 2],
        UpperBody = 1,
        LowerBody = 1,
        HealthRegeneration = 0.8f,
        MilkQuality = 50,
        MilkProductionRate = 0.8f,
        MaxMilkSizeChange = 1f,
        ConceptionRate = 60,
        MaxHealth = 120,
        GrowthTime = 90,
        MaxBirthCount = 8,
        Maintenance = 40,
        DecreasingMentalityOnPregnant = 0.3f,
    };

    public static readonly SpeciesCoreData Lizard = new("Lizard", ERace.Dragonian, typeof(Dragonian))
    {
        SkinColors = [new Color(1f, 0.92f, 0.89f)],
        HairColorMigration = new Color(0.24f, 0.5f, 0.31f),
        HairColorMigrationPower = 0.6f,
        ScaleColors = [new Color(0.23f, 0.46f, 0.24f), new Color(0.4f, 0.46f, 0.27f), new Color(0.46f, 0.44f, 0.37f)],
        Figure = EFigure.Girl,
        EyeBallType = 3,
        EarType = 2,
        TailDragonType = 2,
        HairBodyType = 2,
        TitsTypes = [0, 2],
        UpperBody = 3,
        LowerBody = 1,
        HealthRegeneration = 1.5f,
        MilkQuality = 100,
        MilkProductionRate = 0.8f,
        MaxMilkSizeChange = 0.8f,
        ConceptionRate = 50,
        MaxHealth = 160,
        GrowthTime = 70,
        MaxBirthCount = 8,
        Maintenance = 80,
        DecreasingMentalityOnPregnant = 0.5f,
    };

    public static readonly SpeciesCoreData Rabbit = new("Rabbit", ERace.Usagi, typeof(Usagi))
    {
        SkinColors = [new Color(1f, 0.92f, 0.89f)],
        HairColors = [new Color(0.45f, 0.31f, 0.28f), new Color(0.55f, 0.55f, 0.49f)],
        HairColorMigrationPower = 0.4f,
        Figure = EFigure.Girl,
        EyeBallType = 1,
        EarType = 4,
        EarHairType = 4,
        TailType = 3,
        HairBodyType = 1,
        TitsTypes = [0],
        UpperBody = 3,
        LowerBody = 1,
        HealthRegeneration = 0.8f,
        MilkQuality = 100,
        MilkProductionRate = 0.8f,
        MaxMilkSizeChange = 0.6f,
        ConceptionRate = 60,
        MaxHealth = 80,
        GrowthTime = 70,
        MaxBirthCount = 16,
        Maintenance = 80,
        DecreasingMentalityOnPregnant = 0.4f,
    };

    public static readonly SpeciesCoreData Sheep = new("Sheep", ERace.Hitsuji, typeof(Hitsuji))
    {
        SkinColors = [new Color(1f, 0.92f, 0.89f)],
        HairColors = [new Color(0.93f, 0.93f, 0.88f)],
        HairColorMigrationPower = 0.7f,
        Figure = EFigure.Girl,
        EyeBallType = 1,
        EarType = 4,
        HornType = 1,
        TailType = 4,
        HairBodyType = 1,
        TitsTypes = [3, 4],
        UpperBody = 3,
        LowerBody = 2,
        HealthRegeneration = 0.8f,
        MilkQuality = 200,
        MilkProductionRate = 1f,
        MaxMilkSizeChange = 1.5f,
        ConceptionRate = 30,
        MaxHealth = 160,
        GrowthTime = 90,
        MaxBirthCount = 12,
        Maintenance = 200,
        DecreasingMentalityOnPregnant = 0.4f,
    };

    public static readonly SpeciesCoreData Wolf = new("Wolf", ERace.Inu, typeof(Inu))
    {
        SkinColors = [new Color(1f, 0.92f, 0.89f)],
        HairColors = [new Color(0.45f, 0.45f, 0.42f), new Color(0.45f, 0.39f, 0.2f)],
        HairColorMigrationPower = 0.4f,
        Figure = EFigure.Girl,
        EyeBallType = 3,
        EarType = 4,
        EarHairType = 1,
        TailType = 2,
        HairBodyType = 1,
        TitsTypes = [0, 2],
        UpperBody = 3,
        LowerBody = 1,
        HealthRegeneration = 1f,
        MilkQuality = 100,
        MilkProductionRate = 1f,
        MaxMilkSizeChange = 0.8f,
        ConceptionRate = 30,
        MaxHealth = 160,
        GrowthTime = 90,
        MaxBirthCount = 16,
        Maintenance = 80,
        DecreasingMentalityOnPregnant = 0.4f,
    };

    public static readonly List<ISpeciesCoreData> All =
    [
        Elf, Gnome, Human, Imp, Lizard, Rabbit, Sheep, Wolf,
    ];
}
