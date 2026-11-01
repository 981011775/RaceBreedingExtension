#nullable enable
using System;
using ComplexBreedingRedux.Features;
using HarmonyLib;
using MBMScripts;

namespace ComplexBreedingRedux.Patches.LocalizationPatches;

[HarmonyPatch(typeof(SeqLocalization), "Localize", new[] { typeof(string) })]
internal static class SeqLocalizationLocalizePatch
{
    public static void Postfix(string text, ref string __result)
        => LocalizationSystem.ApplyLocalize(text, ref __result);
}
