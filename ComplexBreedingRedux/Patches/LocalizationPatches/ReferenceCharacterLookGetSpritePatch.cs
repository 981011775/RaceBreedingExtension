#nullable enable
using ComplexBreedingRedux.Features;
using HarmonyLib;
using MBMScripts;
using UnityEngine;

namespace ComplexBreedingRedux.Patches.LocalizationPatches;

[HarmonyPatch(typeof(ReferenceCharacterLook), "GetSprite")]
internal static class ReferenceCharacterLookGetSpritePatch
{
    public static void Postfix(ReferenceCharacterLook __instance, ref Sprite __result)
    {
        Character? character = __instance.Updater.TargetUnit?.Unit as Character;
        if (character == null) return;
        LocalizationSystem.ApplyRaceSprite(character, ref __result);
    }
}
