#nullable enable
using System;
using HarmonyLib;
using MBMScripts;

namespace ComplexBreedingRedux.Patches.PlayDataPatches;

[HarmonyPatch(typeof(PlayData), "ClientRespawnTime", MethodType.Getter)]
internal static class ClientRespawnTimePatch
{
    public static void Postfix(ref float __result)
        => __result = (float)Math.Ceiling(__result / 3f);
}
