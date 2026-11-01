#nullable enable
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using MBMScripts;

namespace ComplexBreedingRedux.Patches.PlayDataPatches;

[HarmonyPatch(typeof(PlayData), "Pay")]
internal static class PayPatch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.opcode == OpCodes.Ldc_I4_5)
                yield return new CodeInstruction(OpCodes.Ldc_I4_1);
            else
                yield return instruction;
        }
    }
}
