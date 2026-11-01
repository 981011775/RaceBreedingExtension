#nullable enable
using ComplexBreedingRedux.Features;
using HarmonyLib;
using MBMScripts;
using UnityEngine;
namespace ComplexBreedingRedux.Patches.AppearancePatches;

[HarmonyPatch(typeof(Character), "TitsType", MethodType.Getter)]
internal static class TitsTypePatch
{
    public static void Postfix(Character __instance, ref int __result)
    {
        if (__instance.IsChild || (__instance.Figure != EFigure.Girl && __instance.Figure != EFigure.Woman))
            return;

        int clamped = Mathf.Clamp(__instance.RawTitsType, 1, ModCompatibility.GetMaxBreastSize());
        __result = (__instance.RawFigure == EFigure.Girl && clamped == 1) ? 0 : clamped;
    }
}
