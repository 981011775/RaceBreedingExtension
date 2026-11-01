#nullable enable
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ComplexBreedingRedux.Utilities;

internal static class PatchUtils
{
    public static MethodBase? FindNpcMethod(string fullTypeName, string methodName)
    {
        var type = AccessTools.TypeByName(fullTypeName);
        if (type == null)
        {
            Debug.Log($"[CBR] NPC patch skipped: type '{fullTypeName}' not found — game version mismatch?");
            return null;
        }
        var method = AccessTools.Method(type, methodName);
        if (method == null)
            Debug.Log($"[CBR] NPC patch skipped: method '{methodName}' not found on '{fullTypeName}' — game version mismatch?");
        return method;
    }
}
