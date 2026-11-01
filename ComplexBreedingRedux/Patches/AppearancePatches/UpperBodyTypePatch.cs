#nullable enable
using ComplexBreedingRedux.Data;
using ComplexBreedingRedux.Data.Species;
using ComplexBreedingRedux.Data.SpeciesCore;
using HarmonyLib;
using MBMScripts;

namespace ComplexBreedingRedux.Patches.AppearancePatches;

[HarmonyPatch(typeof(Character), "UpperBodyType", MethodType.Getter)]
internal static class UpperBodyTypePatch
{
    private static readonly AccessTools.FieldRef<Character, int> UpperBodyRef =
        AccessTools.FieldRefAccess<Character, int>("m_UpperBodyType");

    [HarmonyPriority(Priority.Last)]
    public static void Postfix(Character __instance, ref int __result)
    {
        if (__instance.IsChild || (__instance.Figure != EFigure.Girl && __instance.Figure != EFigure.Woman))
            return;

        // Dwarf/Usagi/Hitsuji — не трогаем (совместимость с TitsMod)
        ERace race = __instance.Race;
        if (race == ERace.Dwarf || race == ERace.Usagi || race == ERace.Hitsuji)
            return;

        // Inu/Dragonian/Neko — всегда UpperBodyType = 1 (совместимость с TitsMod)
        if (race == ERace.Inu || race == ERace.Dragonian || race == ERace.Neko)
        {
            __result = 1;
            return;
        }

        // Если поле задано (ручное переключение, TitsMod, или наша инициализация) —
        // возвращаем сырое значение
        int raw = UpperBodyRef(__instance);
        if (raw > 0)
        {
            __result = raw;
            return;
        }

        // Поле не задано (0) — применяем значение по умолчанию
        if (GameData.TryGetSpecies(__instance, out ISpeciesData? species) && species != null)
        {
            __result = species.UpperBody;
            return;
        }

        // Core species — деривируем из LowerBodyType
        if (__instance.LowerBodyType == 1)
        {
            __result = 1;
        }
        else if (__instance.LowerBodyType == 2)
        {
            __result = __instance.Figure == EFigure.Girl ? 3 : 2;
        }

    }
}
