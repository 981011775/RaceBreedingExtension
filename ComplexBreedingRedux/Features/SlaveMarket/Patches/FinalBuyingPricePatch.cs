#nullable enable
using System;
using ComplexBreedingRedux.Data;
using ComplexBreedingRedux.Data.Professions;
using HarmonyLib;
using MBMScripts;

namespace ComplexBreedingRedux.Features.SlaveMarket.Patches;

[HarmonyPatch(typeof(Female), "FinalBuyingPrice", MethodType.Getter)]
internal static class FinalBuyingPricePatch
{
    public static void Postfix(Female __instance, ref int __result)
    {
        SlaveMarketBuyPricing.ApplyBreastBuyingMarkup(__instance, ref __result);

        if (GameData.TryGetProfession(__instance, out IProfessionData? profession) && profession != null)
        {
            __result *= (int)Math.Pow(3.0, profession.Tier - 1);
            if (GameManager.Instance.PlayerData.Difficulty == EDifficulty.Easy)
                __result = (int)(__result * 0.2f);
        }
    }
}
