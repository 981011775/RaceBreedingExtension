#nullable enable
using System.Collections.Generic;
using ComplexBreedingRedux.Utilities;

namespace ComplexBreedingRedux.Features.SlaveMarket;

/// <summary>Баланс экономики витрины рабынь (покупка и продажа).</summary>
internal static class SlaveMarketBalance
{
    /// <summary>Базовая цена тира вида/профессии в монетах.</summary>
    public static readonly Dictionary<int, int> TierPrice = CsvLoader.LoadIntIntDictionary(
        "ComplexBreedingRedux.Features.SlaveMarket.Data.slave_market_tier_prices.csv");

    /// <summary>
    /// Наценка к FinalBuyingPrice (%) по RawTitsType. 0 = без наценки.
    /// </summary>
    public static readonly Dictionary<int, int> BreastBuyingMarkupPercent = CsvLoader.LoadIntIntDictionary(
        "ComplexBreedingRedux.Features.SlaveMarket.Data.slave_market_breast_markup.csv");
}
