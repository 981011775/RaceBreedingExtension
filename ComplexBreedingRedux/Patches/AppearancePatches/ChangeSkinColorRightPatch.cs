#nullable enable
using HarmonyLib;
using MBMScripts;

namespace ComplexBreedingRedux.Patches.AppearancePatches;

[HarmonyPatch(typeof(InteractionCustomize), "ChangeSkinColorRight")]
internal static class ChangeSkinColorRightPatch
{
    public static bool Prefix(InteractionCustomize __instance)
    {
        TargetUnit? targetUnit = __instance.GetComponentInParent<TargetUnit>();
        return targetUnit != null && !(targetUnit.Unit is Female);
    }
}
