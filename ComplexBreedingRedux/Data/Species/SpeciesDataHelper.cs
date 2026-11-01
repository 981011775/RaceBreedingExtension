#nullable enable
using System.Collections.Generic;
using MBMScripts;

namespace ComplexBreedingRedux.Data.Species;

internal static class SpeciesDataHelper
{
    public static string GetSummary(this ISpeciesStats species) =>
        string.Format("{0}%/{1}, {2}/{3}, {4}/{5}",
            species.ConceptionRate,
            species.MaxBirthCount,
            (int)(300f * species.MilkProductionRate / 200f * species.MilkQuality),
            species.Maintenance,
            species.HealthRegeneration,
            species.MaxHealth);

    public static int GetAttributesPoints(this ISpeciesStats species, ICollection<ETrait> traits)
    {
        int n = 0;
        n += HealthRegenerationPoints(species.HealthRegeneration);
        n += MilkQualityPoints(species.MilkQuality);
        n += MilkProductionRatePoints(species.MilkProductionRate);
        n += MaxMilkSizeChangePoints(species.MaxMilkSizeChange);
        n += ConceptionRatePoints(species.ConceptionRate);
        n += MaxHealthPoints(species.MaxHealth);
        n += GrowthTimePoints(species.GrowthTime);
        n += MaxBirthCountPoints(species.MaxBirthCount);
        n += MaintenancePoints(species.Maintenance);
        n += DecreasingMentalityOnPregnantPoints(species.DecreasingMentalityOnPregnant);
        foreach (ETrait t in traits)
            n += t == ETrait.Trait108 ? 2 : 1;
        return n + (species.Race == ERace.Elf ? 3 : 0);
    }

    private static int HealthRegenerationPoints(float v) => v switch
    {
        0.6f => -3, 0.8f => -1, 1.5f => 2, 2f => 4, 3f => 5, _ => 0
    };

    private static int MilkQualityPoints(int v) => v switch
    {
        50 => -3, 100 => -1, 200 => 1, 400 => 4, 600 => 6, 1000 => 9, _ => 0
    };

    private static int MilkProductionRatePoints(float v) => v switch
    {
        0.5f => -4, 0.8f => -2, 1.5f => 3, 2f => 5, 3f => 7, _ => 0
    };

    private static int MaxMilkSizeChangePoints(float v) => v switch
    {
        0.6f => -2, 0.8f => -1, 1.5f => 1, 2f => 2, 3f => 3, _ => 0
    };

    private static int ConceptionRatePoints(int v) => v switch
    {
        5 => -6, 30 => -2, 50 => 2, 60 => 4, 70 => 6, 80 => 8, _ => 0
    };

    private static int MaxHealthPoints(int v) => v switch
    {
        80 => -2, 120 => -1, 160 => 0, 240 => 1, 320 => 2, 500 => 3, _ => 0
    };

    private static int GrowthTimePoints(int v) => 0;

    private static int MaxBirthCountPoints(int v) => v switch
    {
        4 => -2, 8 => -1, 12 => 0, 16 => 1, 24 => 2, 36 => 3, _ => 0
    };

    private static int MaintenancePoints(int v) => v switch
    {
        40 => 3, 80 => 2, 120 => 1, 200 => 0, 300 => -2, 400 => -4, _ => 0
    };

    private static int DecreasingMentalityOnPregnantPoints(float v) => v switch
    {
        0.1f => 2, 0.2f => 1, 0.3f => 0, 0.4f => -1, 0.5f => -2, _ => 0
    };
}
