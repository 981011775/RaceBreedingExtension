#nullable enable
using ComplexBreedingRedux.Features;
using HarmonyLib;
using MBMScripts;

namespace ComplexBreedingRedux.Patches.PlayDataPatches;

[HarmonyPatch(typeof(PlayData), "OpenReport")]
internal static class OpenReportPatch
{
    public static void Postfix(PlayData __instance)
        => PlayDataSystem.ApplyOpenReport(__instance);
}
