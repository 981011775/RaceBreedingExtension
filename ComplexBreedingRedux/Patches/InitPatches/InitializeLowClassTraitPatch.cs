#nullable enable
using HarmonyLib;
using MBMScripts;

namespace ComplexBreedingRedux.Patches.InitPatches;

[HarmonyPatch(typeof(Character), "InitializeLowClassTrait")]
internal static class InitializeLowClassTraitPatch
{
    public static bool Prefix() => false;
}
