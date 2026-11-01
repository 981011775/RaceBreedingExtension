#nullable enable
using System.Collections.Generic;
using ComplexBreedingRedux.Features;
using HarmonyLib;
using MBMScripts;

namespace RaceBreedingExtension.CombatPower.Patches;

/// <summary>
/// 职业战斗行为触发点：监视「怪物（雄性）」的 sex 状态变化，
/// 从而在 <b>触发 EState.Sex</b>（进入）与 <b>Sex 场景结束</b>（离开）两个时机对女性伙伴各判定一次。
///
/// 与 DepravedShield 的 SexProgressPatch、血脉的 BloodlineMonsterCumshotPatch 相互独立，互不影响。
/// </summary>
[HarmonyPatch(typeof(Male), "Sex")]
internal static class ProfessionCombatSexPatch
{
    /// <summary>上一次记录：该雄性是否处于 sex 场景（键 = 雄性 UnitId）。</summary>
    private static readonly Dictionary<int, bool> _inSex = new();

    /// <summary>上一次记录的伙伴 UnitId（用于在伙伴切换时把旧伙伴视为“场景结束”）。</summary>
    private static readonly Dictionary<int, int> _partnerId = new();

    public static void Postfix(Male __instance)
    {
        if (__instance == null)
            return;

        int id = __instance.UnitId;
        Character? partner = __instance.Partner;
        bool inSex = IsSexState(__instance.State) && partner is Female;

        bool wasInSex = _inSex.TryGetValue(id, out bool prev) && prev;
        int prevPartnerId = _partnerId.TryGetValue(id, out int pid) ? pid : 0;
        int partnerId = partner?.UnitId ?? 0;

        _inSex[id] = inSex;
        _partnerId[id] = partnerId;

        // 未在 sex 场景中：无需判定
        if (!inSex)
        {
            // 场景结束（离开 sex）→ 用上一次的伙伴判定；
            // 注意：此时女性身上的 Partner 可能已被清空，因此直接使用怪物本体（__instance）。
            if (wasInSex)
                NotifyEnd(prevPartnerId, __instance);
            return;
        }

        // 场景开始（进入 sex）或换了伙伴（= 旧场景结束 + 新场景开始）
        if (!wasInSex || partnerId != prevPartnerId)
        {
            if (wasInSex && prevPartnerId != 0)
                NotifyEnd(prevPartnerId, __instance);
            if (partner is Female female)
                ProfessionCombatSystem.OnSexStarted(female, __instance);
        }
    }

    private static void NotifyEnd(int partnerUnitId, Character monster)
    {
        if (partnerUnitId == 0)
            return;
        PlayData? pd = GameManager.Instance?.PlayerData;
        Unit? unit = pd?.GetUnit(partnerUnitId);
        if (unit is Female female)
            ProfessionCombatSystem.OnSexEnded(female, monster);
    }

    private static bool IsSexState(EState state)
    {
        switch (state)
        {
            case EState.Sex:
            case EState.SexDrained:
            case EState.CumShotInside:
            case EState.CumShotOutside:
                return true;
            default:
                return false;
        }
    }
}
