#nullable enable
using System;
using HarmonyLib;
using MBMScripts;

namespace ComplexBreedingRedux.Patches.PlayDataPatches;

[HarmonyPatch(typeof(PlayData), "CondomPrice", MethodType.Getter)]
internal static class CondomPricePatch
{
    public static void Postfix(ref int __result)
        => __result = (int)Math.Round((double)(__result / 2f), 0);
}
