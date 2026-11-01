#nullable enable
using System.Collections.Generic;
using System.Reflection.Emit;
using ComplexBreedingRedux.Utilities;
using HarmonyLib;
using MBMScripts;

namespace ComplexBreedingRedux.Patches.PlayDataPatches;

[HarmonyPatch(typeof(PlayData), "NielCustomizeInitializeUnit")]
internal static class NielCustomizeInitializeUnitPatch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var list = new List<CodeInstruction>(instructions);
        int stopIndex = list.GetIndex(OpCodes.Callvirt, AccessTools.Method(typeof(Character), "ClearTrait"));

        for (int i = 0; i <= stopIndex; i++)
            yield return list[i];

        yield return new CodeInstruction(OpCodes.Ret);
    }
}
