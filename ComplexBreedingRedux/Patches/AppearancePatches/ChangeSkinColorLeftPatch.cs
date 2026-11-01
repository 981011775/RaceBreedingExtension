#nullable enable
using HarmonyLib;
using MBMScripts;

namespace ComplexBreedingRedux.Patches.AppearancePatches;

[HarmonyPatch(typeof(InteractionCustomize), "ChangeSkinColorLeft")]
internal static class ChangeSkinColorLeftPatch
{
    public static bool Prefix(InteractionCustomize __instance)
    {
        TargetUnit? targetUnit = __instance.GetComponentInParent<TargetUnit>();
        return targetUnit != null && !(targetUnit.Unit is Female);
    }
}
