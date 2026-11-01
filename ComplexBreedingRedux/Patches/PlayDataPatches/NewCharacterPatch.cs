#nullable enable

using ComplexBreedingRedux.Data;
using ComplexBreedingRedux.Features;
using HarmonyLib;
using MBMScripts;

namespace ComplexBreedingRedux.Patches.PlayDataPatches;

[HarmonyPatch(typeof(PlayData), "NewCharacter")]
internal static class NewCharacterPatch
{
    public static void Prefix(PlayData __instance, ref EUnitType unitType)
        => PlayDataSystem.ApplyNewCharacterRace(__instance, ref unitType);

    public static void Postfix(PlayData __instance, EUnitType unitType, ESector sector, Character __result)
    {
        if (__result is not Female female)
            return;

        // 商人（奴隶市场）出售的奴隶只保留原版 13 种职业；猎手营地可生成所有职业，不受此限制。
        if (sector == ESector.Market)
            ProfessionSystem.EnforceMarketProfessions(female);

        if (!GameData.TryGetCoreInternalNameByUnitType(unitType, out string? coreInternalName)
            || string.IsNullOrEmpty(coreInternalName))
            return;

        PlayDataSystem.ApplyMarketSlaveBreastSize(female, coreInternalName!, sector);
    }
}
