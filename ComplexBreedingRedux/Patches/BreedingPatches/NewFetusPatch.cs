#nullable enable
using ComplexBreedingRedux.Features;
using HarmonyLib;
using MBMScripts;

namespace ComplexBreedingRedux.Patches.BreedingPatches;

[HarmonyPatch(typeof(PlayData), "NewFetus", new[] { typeof(Character), typeof(Character) })]
internal static class NewFetusPatch
{
    public static void Postfix(Character female, Character male, ref Unit __result)
        => BreedingSystem.ApplyFetus(female, male, ref __result);
}
