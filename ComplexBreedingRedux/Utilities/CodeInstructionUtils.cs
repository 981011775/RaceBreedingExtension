#nullable enable
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace ComplexBreedingRedux.Utilities;

internal static class CodeInstructionUtils
{
    public static void Log(this CodeInstruction instruction)
    {
        string operandStr = instruction.operand is Label lbl
            ? $"label: {lbl.GetHashCode()}"
            : instruction.operand?.ToString() ?? string.Empty;

        string labels = string.Join(", ", System.Linq.Enumerable.Select(instruction.labels, (Label x) => x.GetHashCode()));
        Debug.Log($"[CBR] {instruction.opcode} {operandStr} - labels: {labels}");
    }

    public static int GetIndex(this List<CodeInstruction> instructions, OpCode opCode, object? operand = null, int offset = 0)
    {
        int count = 0;
        foreach (CodeInstruction x in instructions)
        {
            if (x.opcode == opCode && x.operand == operand) break;
            count++;
        }
        return count + offset;
    }

    public static CodeInstruction GetInstruction(this List<CodeInstruction> instructions, OpCode opCode, object? operand = null, int offset = 0)
        => instructions[instructions.GetIndex(opCode, operand, offset)];

    public static IEnumerable<CodeInstruction> GetInstructionsUntil(this List<CodeInstruction> instructions, OpCode opCode, object? operand = null, int offset = 0)
    {
        int index = instructions.GetIndex(opCode, operand, offset);
        for (int i = 0; i < index; i++)
            yield return instructions[i];
    }
}
