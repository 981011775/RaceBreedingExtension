#nullable enable
using ComplexBreedingRedux.Data;
using HarmonyLib;
using MBMScripts;

namespace ComplexBreedingRedux.Patches.UnitPatches;

[HarmonyPatch(typeof(Unit), "RaceName", MethodType.Getter)]
internal static class RaceNamePatch
{
    public static void Postfix(Unit __instance, ref string __result)
    {
        if (__instance is not Character character)
            return;

        if (GameData.TryGetSpecies(character, out var species) && species != null)
        {
            __result = species.Name ?? "";
            return;
        }
        if (GameData.TryGetSpeciesCore(character, out var speciesCore) && speciesCore != null)
            __result = speciesCore.Name ?? "";
    }
}
