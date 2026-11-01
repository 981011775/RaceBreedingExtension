#nullable enable
using System.Linq;
using ComplexBreedingRedux.Data;
using ComplexBreedingRedux.Properties;
using HarmonyLib;
using MBMScripts;

namespace ComplexBreedingRedux.Patches.UnitPatches;

[HarmonyPatch(typeof(Unit), "DisplayName", MethodType.Getter)]
internal static class DisplayNamePatch
{
    public static void Postfix(Unit __instance, string ___m_DisplayName, ref string __result)
    {
        if (__instance is not Character character)
            return;

        // 如果已经设置了显示名称（非空），则保留
        if (!string.IsNullOrWhiteSpace(___m_DisplayName))
            return;

        // 男性角色不处理（保留原样）
        if (character is Male)
            return;

        // 女性角色：优先使用进化物种名称，否则使用基础种族名称
        if (GameData.TryGetSpecies(character, out var species) && species != null)
        {
            __result = species.Name;
            return;
        }

        if (GameData.TryGetSpeciesCore(character, out var speciesCore) && speciesCore != null)
        {
            __result = speciesCore.Name;
            return;
        }
    }
}