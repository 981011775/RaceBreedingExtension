#nullable enable
using ComplexBreedingRedux.Features;
using HarmonyLib;
using MBMScripts;
using UnityEngine;

namespace ComplexBreedingRedux.Patches.AppearancePatches;

/// <summary>
/// Гарантирует alpha = 1 для SkinColor и однократно применяет палитру вида.
/// Вся логика делегируется в <see cref="SkinColorSystem"/>.
/// </summary>
[HarmonyPatch(typeof(Character), "SkinColor", MethodType.Getter)]
internal static class SkinColorPatch
{
    public static void Postfix(Character __instance, ref Color __result)
    {
        __result.a = 1f;
        SkinColorSystem.EnforcePalette(__instance);
    }
}
