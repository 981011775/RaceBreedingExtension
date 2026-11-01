#nullable enable
using System;
using HarmonyLib;
using MBMScripts;

namespace ComplexBreedingRedux.Patches.PlayDataPatches;

[HarmonyPatch(typeof(PlayData), "MaxCountOfLoveGel", MethodType.Getter)]
internal static class MaxCountOfLoveGelPatch
{
    public static void Postfix(ref int __result)
        => __result = (int)Math.Round((double)(__result * 4f), 0);
}
