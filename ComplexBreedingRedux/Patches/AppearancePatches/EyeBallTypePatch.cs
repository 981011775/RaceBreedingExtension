#nullable enable
using HarmonyLib;
using MBMScripts;

namespace ComplexBreedingRedux.Patches.AppearancePatches;

[HarmonyPatch(typeof(Character), "EyeBallType", MethodType.Getter)]
internal static class EyeBallTypePatch
{
    public static void Postfix(int ___m_EyeBallType, ref int __result)
    {
        __result = ___m_EyeBallType;
    }
}
