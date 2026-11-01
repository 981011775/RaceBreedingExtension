#nullable enable
using HarmonyLib;
using MBMScripts;

namespace ComplexBreedingRedux.Patches.AppearancePatches;

[HarmonyPatch(typeof(Character), "EyeType", MethodType.Getter)]
internal static class EyeTypePatch
{
    public static void Postfix(Character __instance, int ___m_EyeType, ref int __result)
    {
        if (__instance.RawFigure == EFigure.Woman && ___m_EyeType == 3)
            __result = 1;
    }
}
