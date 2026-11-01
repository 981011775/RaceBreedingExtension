#nullable enable
using ComplexBreedingRedux.Features;
using HarmonyLib;
using MBMScripts;
using UnityEngine;
namespace ComplexBreedingRedux.Patches.AppearancePatches;

[HarmonyPatch(typeof(Character), "TitsType", MethodType.Setter)]
internal static class TitsTypeSetPatch
{
    public static void Prefix(Character __instance, ref int value)
    {
        if (__instance.Figure != EFigure.Girl && __instance.Figure != EFigure.Woman)
            return;

        int maxKey = ModCompatibility.GetMaxBreastSize();
        int dir = (__instance.RawTitsType < value) ? 1 : -1;

        if (__instance.Figure == EFigure.Girl)
        {
            if (value == 1)
                value = Mathf.Clamp(value + dir, 0, maxKey);
        }
        else
        {
            value = Mathf.Clamp(value, 1, maxKey);
        }
    }
}
