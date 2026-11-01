#nullable enable
using MBMScripts;

namespace ComplexBreedingRedux.Data.Species;

internal interface ISpeciesStats
{
    ERace Race { get; }
    EFigure Figure { get; set; }
    int EyeBallType { get; set; }
    int EarType { get; set; }
    int EarHairType { get; set; }
    int HornType { get; set; }
    int HornDragonType { get; set; }
    int TailType { get; set; }
    int TailDragonType { get; set; }
    int HairBodyType { get; set; }
    float HealthRegeneration { get; set; }
    int MilkQuality { get; set; }
    float MilkProductionRate { get; set; }
    float MaxMilkSizeChange { get; set; }
    int ConceptionRate { get; set; }
    int MaxHealth { get; set; }
    int GrowthTime { get; set; }
    int MaxBirthCount { get; set; }
    int Maintenance { get; set; }
    float DecreasingMentalityOnPregnant { get; set; }
    int UpperBody { get; set; }
    int LowerBody { get; set; }
}
