#nullable enable
using System;
using HarmonyLib;
using MBMScripts;

namespace ComplexBreedingRedux.Patches.PlayDataPatches;

[HarmonyPatch(typeof(PlayData), "MaxCountOfCondom", MethodType.Getter)]
internal static class MaxCountOfCondomPatch
{
    public static void Postfix(ref int __result)
        => __result = (int)Math.Round((double)(__result * 4f), 0);
}
