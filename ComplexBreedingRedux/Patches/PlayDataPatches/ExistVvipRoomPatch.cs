#nullable enable
using HarmonyLib;
using MBMScripts;

namespace ComplexBreedingRedux.Patches.PlayDataPatches;

[HarmonyPatch(typeof(PlayData), "ExistVvipRoom", MethodType.Getter)]
internal static class ExistVvipRoomPatch
{
    public static bool Prefix(ref bool __result)
    {
        __result = false;
        return false;
    }
}
