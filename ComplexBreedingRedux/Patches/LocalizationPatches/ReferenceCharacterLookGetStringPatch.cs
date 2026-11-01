#nullable enable
using ComplexBreedingRedux.Features;
using HarmonyLib;
using MBMScripts;

namespace ComplexBreedingRedux.Patches.LocalizationPatches;

[HarmonyPatch(typeof(ReferenceCharacterLook), "GetString")]
internal static class ReferenceCharacterLookGetStringPatch
{
    public static void Postfix(ReferenceCharacterLook __instance, int ___m_DataType, ref string __result)
    {
        Character? character = __instance.Updater.TargetUnit?.Unit as Character;
        if (character == null) return;
        LocalizationSystem.ApplyRaceString(character, ___m_DataType, ref __result);
    }
}
