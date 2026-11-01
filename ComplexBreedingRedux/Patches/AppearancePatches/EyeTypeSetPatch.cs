#nullable enable
using HarmonyLib;
using MBMScripts;
using UnityEngine;

namespace ComplexBreedingRedux.Patches.AppearancePatches;

[HarmonyPatch(typeof(Character), "EyeType", MethodType.Setter)]
internal static class EyeTypeSetPatch
{
    private static readonly int[] AllowedEyeTypes = { 1, 2, 4 };

    public static void Prefix(Character __instance, ref int value)
    {
        if (__instance.RawFigure == EFigure.Woman && value == 3)
            value = AllowedEyeTypes[Random.Range(0, AllowedEyeTypes.Length)];
    }
}
