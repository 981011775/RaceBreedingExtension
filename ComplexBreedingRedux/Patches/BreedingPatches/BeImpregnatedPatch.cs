#nullable enable
using ComplexBreedingRedux.Features;
using HarmonyLib;
using MBMScripts;

namespace ComplexBreedingRedux.Patches.BreedingPatches;

[HarmonyPatch(typeof(Character), "BeImpregnated", new[] { typeof(Character) })]
internal static class BeImpregnatedPatch
{
    public static bool Prefix(Character monster) => false;

    public static void Postfix(Character __instance, Character? monster, ref bool __result)
        => BreedingSystem.Apply(__instance, monster, ref __result);
}
