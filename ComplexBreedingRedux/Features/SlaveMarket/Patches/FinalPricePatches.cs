#nullable enable
using HarmonyLib;
using MBMScripts;

namespace ComplexBreedingRedux.Features.SlaveMarket.Patches;

[HarmonyPatch(typeof(Female), "FinalPrice", MethodType.Getter)]
internal static class FemaleFinalPricePatch
{
    public static bool Prefix(Female __instance, ref int __result)
        => SlaveMarketSellPricing.FinalPricePrefix(__instance, ref __result);
}

[HarmonyPatch(typeof(Anna), "FinalPrice", MethodType.Getter)]
internal static class AnnaFinalPricePatch
{
    public static bool Prefix(Anna __instance, ref int __result)
        => SlaveMarketSellPricing.FinalPricePrefix(__instance, ref __result);
}

[HarmonyPatch(typeof(Aure), "FinalPrice", MethodType.Getter)]
internal static class AureFinalPricePatch
{
    public static bool Prefix(Aure __instance, ref int __result)
        => SlaveMarketSellPricing.FinalPricePrefix(__instance, ref __result);
}

[HarmonyPatch(typeof(Bella), "FinalPrice", MethodType.Getter)]
internal static class BellaFinalPricePatch
{
    public static bool Prefix(Bella __instance, ref int __result)
        => SlaveMarketSellPricing.FinalPricePrefix(__instance, ref __result);
}

[HarmonyPatch(typeof(Claire), "FinalPrice", MethodType.Getter)]
internal static class ClaireFinalPricePatch
{
    public static bool Prefix(Claire __instance, ref int __result)
        => SlaveMarketSellPricing.FinalPricePrefix(__instance, ref __result);
}

[HarmonyPatch(typeof(Karen), "FinalPrice", MethodType.Getter)]
internal static class KarenFinalPricePatch
{
    public static bool Prefix(Karen __instance, ref int __result)
        => SlaveMarketSellPricing.FinalPricePrefix(__instance, ref __result);
}

[HarmonyPatch(typeof(Nero), "FinalPrice", MethodType.Getter)]
internal static class NeroFinalPricePatch
{
    public static bool Prefix(Nero __instance, ref int __result)
        => SlaveMarketSellPricing.FinalPricePrefix(__instance, ref __result);
}

[HarmonyPatch(typeof(Sylvia), "FinalPrice", MethodType.Getter)]
internal static class SylviaFinalPricePatch
{
    public static bool Prefix(Sylvia __instance, ref int __result)
        => SlaveMarketSellPricing.FinalPricePrefix(__instance, ref __result);
}

[HarmonyPatch(typeof(Vivi), "FinalPrice", MethodType.Getter)]
internal static class ViviFinalPricePatch
{
    public static bool Prefix(Vivi __instance, ref int __result)
        => SlaveMarketSellPricing.FinalPricePrefix(__instance, ref __result);
}
