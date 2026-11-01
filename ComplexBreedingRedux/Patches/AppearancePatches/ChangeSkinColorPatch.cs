#nullable enable
using HarmonyLib;
using MBMScripts;
using UnityEngine;

namespace ComplexBreedingRedux.Patches.AppearancePatches;

[HarmonyPatch(typeof(InteractionCustomize), "ChangeSkinColor")]
internal static class ChangeSkinColorPatch
{
    public static bool Prefix(InteractionCustomize __instance, Color color)
    {
        TargetUnit? targetUnit = __instance.GetComponentInParent<TargetUnit>();
        return targetUnit != null && !(targetUnit.Unit is Female);
    }
}
