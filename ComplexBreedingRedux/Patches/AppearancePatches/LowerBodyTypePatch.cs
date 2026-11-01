#nullable enable
using ComplexBreedingRedux.Data;
using ComplexBreedingRedux.Data.Species;
using ComplexBreedingRedux.Data.SpeciesCore;
using HarmonyLib;
using MBMScripts;

namespace ComplexBreedingRedux.Patches.AppearancePatches;

[HarmonyPatch(typeof(Character), "LowerBodyType", MethodType.Getter)]
internal static class LowerBodyTypePatch
{
    private static readonly AccessTools.FieldRef<Character, int> LowerBodyRef =
        AccessTools.FieldRefAccess<Character, int>("m_LowerBodyType");

    [HarmonyPriority(Priority.Last)]
    public static void Postfix(Character __instance, ref int __result)
    {
        if (__instance.IsChild || (__instance.Figure != EFigure.Girl && __instance.Figure != EFigure.Woman))
            return;

        // Если поле задано (ручное переключение, TitsMod, или наша инициализация) —
        // возвращаем сырое значение (геттер игры может вычислить другое)
        int raw = LowerBodyRef(__instance);
        if (raw > 0)
        {
            __result = raw;
            return;
        }

        // Поле не задано (0) — применяем значение по умолчанию из данных вида
        if (GameData.TryGetSpecies(__instance, out ISpeciesData? species) && species != null)
        {
            __result = species.LowerBody;
            return;
        }

        if (GameData.TryGetSpeciesCore(__instance, out ISpeciesCoreData? core) && core != null)
            __result = core.LowerBody;
    }
}
