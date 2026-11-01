#nullable enable
using ComplexBreedingRedux.Features;
using HarmonyLib;
using MBMScripts;

namespace ComplexBreedingRedux.Patches.LocalizationPatches;

/// <summary>Баг-фикс: инвалидация кешей локализации при смене языка.</summary>
[HarmonyPatch(typeof(SeqLocalization), "Load")]
internal static class SetLanguagePatch
{
    public static void Postfix() { LocalizationSystem.InvalidateCaches(); LocalizationSystem.RefreshLocalizedStrings(); }
}
