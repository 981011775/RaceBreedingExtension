#nullable enable
using ComplexBreedingRedux.Features;
using HarmonyLib;
using MBMScripts;

namespace ComplexBreedingRedux.Patches.PlayDataPatches;

[HarmonyPatch(typeof(PlayData), "NielStartCustomize")]
internal static class NielStartCustomizePatch
{
    public static bool Prefix(PlayData __instance)
    {
        PlayDataSystem.ApplyNielStartCustomize(__instance);
        return true;
    }
}
