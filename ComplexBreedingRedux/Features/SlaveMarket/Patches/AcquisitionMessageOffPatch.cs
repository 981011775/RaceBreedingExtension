#nullable enable
using HarmonyLib;
using MBMScripts;

namespace ComplexBreedingRedux.Features.SlaveMarket.Patches;

/// <summary>Подавляет ванильные popup при покупке рабыни на витрине.</summary>
[HarmonyPatch(typeof(Setting), "AcquisitionMessageOff", MethodType.Getter)]
internal static class AcquisitionMessageOffPatch
{
    public static void Postfix(ref bool __result) => __result = true;
}
