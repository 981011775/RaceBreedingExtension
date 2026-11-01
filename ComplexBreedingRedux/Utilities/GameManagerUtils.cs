#nullable enable
using MBMScripts;
using UnityEngine;

namespace ComplexBreedingRedux.Utilities;

internal static class GameManagerUtils
{
    public static bool DEBUG_MODE = false;

    public static void AddSystemMessage(string message)
        => GameManager.Instance.AddSystemMessage(message);

    public static void AddLogMessage(string message)
        => GameManager.Instance.AddLogMessage(message);

    public static void AddDebugLogMessage(string message)
    {
        if (DEBUG_MODE)
            Debug.Log($"[CBR] {message}");
    }
}
