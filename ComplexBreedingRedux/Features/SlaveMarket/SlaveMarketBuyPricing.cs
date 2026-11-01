#nullable enable
using MBMScripts;

namespace ComplexBreedingRedux.Features.SlaveMarket;

/// <summary>Модификаторы FinalBuyingPrice — цена покупки рабыни на витрине.</summary>
internal static class SlaveMarketBuyPricing
{
    /// <summary>Наценка по RawTitsType (Flat = 0%, …).</summary>
    public static void ApplyBreastBuyingMarkup(Female female, ref int price)
    {
        if (female.Figure != EFigure.Girl && female.Figure != EFigure.Woman)
            return;

        int raw = female.RawTitsType;
        if (!SlaveMarketBalance.BreastBuyingMarkupPercent.TryGetValue(raw, out int markupPercent)
            || markupPercent <= 0)
        {
            return;
        }

        price = (int)(price * (1m + markupPercent / 100m));
    }
}
