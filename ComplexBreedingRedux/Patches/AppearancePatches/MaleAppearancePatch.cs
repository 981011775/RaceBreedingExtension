#nullable enable
using ComplexBreedingRedux.Data;
using ComplexBreedingRedux.Data.Species;
using ComplexBreedingRedux.Features;
using HarmonyLib;
using MBMScripts;

namespace ComplexBreedingRedux.Patches.AppearancePatches;

/// <summary>
/// Postfix на <c>Male.InitializeAppearance</c> — заменяет ванильный <c>GetRandomColor()</c>
/// цветом из палитры <c>SpeciesRegistry</c>. Ванильная игра использует полностью случайный HSV
/// для SkinColor Male монстров, что приводит к фиолетовым/зелёным оборотням и т.п.
/// Для Werewolf также обновляет HairColor = SkinColor (ванильное поведение).
/// </summary>
[HarmonyPatch(typeof(Male), "InitializeAppearance")]
internal static class MaleAppearancePatch
{
    public static void Postfix(Male __instance)
    {
        string typeName = __instance.GetType().Name;
        if (!GameData.TryGetSpecies(typeName, out ISpeciesData? species) || species == null)
            return;

        if (species.SkinColors is { Length: > 0 })
            __instance.SkinColor = SkinColorSystem.PickRandom(species.SkinColors);

        if (SkinColorSystem.MaleHairPaletteTypes.Contains(typeName) && species.HairColors is { Length: > 0 })
            __instance.HairColor = SkinColorSystem.PickRandom(species.HairColors);
    }
}
