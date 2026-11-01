#nullable enable
using ComplexBreedingRedux.Data;
using ComplexBreedingRedux.Data.Professions;
using ComplexBreedingRedux.Data.Species;
using MBMScripts;

namespace ComplexBreedingRedux.Features.SlaveMarket;

/// <summary>Расчёт FinalPrice — цена продажи рабыни игроком.</summary>
internal static class SlaveMarketSellPricing
{
    public static int GetFinalPrice(Female female)
    {
        if (female.IsDead)
            return GameManager.ConfigData.CostOfDisposingCorpse;

        GameData.TryGetSpecies(female, out ISpeciesData? species);
        int tier = species?.Tier ?? 1;
        decimal price = SlaveMarketBalance.TierPrice[tier];

        if (GameData.TryGetProfession(female, out IProfessionData? profession) && profession != null)
            price += SlaveMarketBalance.TierPrice[profession.Tier] / 4m;

        if (female.IsInfertile)
            price *= 0.5m;

        if (female.IsDepraved)
            price *= 0.8m;

        return (int)price;
    }

    internal static bool FinalPricePrefix(Female instance, ref int result)
    {
        result = GetFinalPrice(instance);
        return false;
    }
}
