#nullable enable
using HarmonyLib;
using MBMScripts;
using System.Collections.Generic;

namespace RaceBreedingExtension.Bloodline.Patches;

/// <summary>
/// 怪物（雄性持有者）血脉的 cumshot 专属效果触发点。
///
/// 与 DepravedShield 的 SexProgressPatch 相互独立（不受其 Enable 开关影响），
/// 自行跟踪雄性角色的 EState 变化：进入 CumShotInside / CumShotOutside 时触发一次。
/// 触发内容见 <see cref="BloodlineSystem.OnMonsterCumshot"/>（防护结界削减 / 特质升级几率 / 体力恢复几率）。
/// </summary>
[HarmonyPatch(typeof(Male), "Sex")]
internal static class BloodlineMonsterCumshotPatch
{
    /// <summary>上一次记录的 EState（键 = UnitId）。</summary>
    private static readonly Dictionary<int, EState> _prevState = new();

    public static void Postfix(Male __instance)
    {
        if (__instance == null) return;

        int id = __instance.UnitId;
        EState current = __instance.State;
        EState previous = _prevState.TryGetValue(id, out EState prev) ? prev : EState.None;

        if (current == previous)
            return;

        _prevState[id] = current;

        if (current != EState.CumShotInside && current != EState.CumShotOutside)
            return;

        if (!(__instance.Partner is Female female))
            return;

        BloodlineSystem.OnMonsterCumshot(__instance, female);
    }
}
