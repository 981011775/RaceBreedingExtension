#nullable enable
using System;
using HarmonyLib;
using MBMScripts;

namespace ComplexBreedingRedux.Patches.ItemPatches;

/// <summary>
/// 特性升级药（<see cref="EItemType.Item_TraitUpgradePill"/>）的「选择数量使用」。
///
/// ■ 原版行为
///   只有母乳类（Milk / LoliMilk … DragonianMilk，共 7 种）在拖到角色身上时会弹出数量条
///   （数量分配窗口 <see cref="EGameWindow.ItemAllocation"/>：滑块范围 1 ~ 物品数量，
///   确定后按次数循环调用 <c>Item.Use</c>，见 <c>GameManager.ItemAllocationOk()</c>）；
///   其它物品（含特性升级药）在 <c>InteractionUnit.Drop()</c> 里直接 <c>item.Use(unit)</c>，
///   一次只用 1 个。
///
/// ■ 本补丁
///   在 <c>InteractionUnit.Drop()</c> 之前拦下「特性升级药 → 角色」的拖放：
///   数量 &gt; 1 时改走与母乳 **完全相同** 的数量分配窗口，拖动数量条并确认后一次性使用 N 个
///   （每个各独立生效一次、共消耗 N 个）。
///
/// ■ 不改动任何既有行为
///   下列情况一律返回 true 交还原版处理：
///     · 拖动的不是特性升级药 / 目标不是角色（房间、空位等）；
///     · 目标不可用（禁用、Client、婴儿 → 原版提示）、目标非法（死亡 / NPC / 无特质）；
///     · 物品数量 = 1（无需数量条）；
///     · 市场购买时的金币校验失败（原版提示 #No_Gold）。
/// </summary>
[HarmonyPatch(typeof(InteractionUnit), "Drop")]
internal static class TraitUpgradePillAllocationPatch
{
    /// <summary>
    /// <c>InteractionUnit</c> 上缓存的拖放目标组件（私有字段；<c>Drop()</c> 内部也使用同一字段，
    /// 因此这里取到的目标与随后原版逻辑使用的完全一致）。
    /// </summary>
    private static readonly AccessTools.FieldRef<InteractionUnit, TargetUnit> TargetUnitRef =
        AccessTools.FieldRefAccess<InteractionUnit, TargetUnit>("m_TargetUnit");

    public static bool Prefix(InteractionUnit __instance)
    {
        try
        {
            GameManager? manager = GameManager.Instance;
            PlayData? playerData = manager?.PlayerData;
            if (manager == null || playerData == null)
                return true;

            // 只处理「正在拖动的特性升级药」
            if (playerData.DraggingUnit is not Item item || item.ItemType != EItemType.Item_TraitUpgradePill)
                return true;

            // 目标必须存在且可用（与 Drop() 的前置判定保持一致）
            TargetUnit? target = TargetUnitRef(__instance);
            Unit? unit = target?.Unit;
            if (unit == null || unit.IsDisabled || unit is Client)
                return true;

            // 只有拖到「角色」身上才需要数量条（拖到房间等交还原版）
            if (unit is not Character character)
                return true;

            // 婴儿 → 原版会提示 #No_Sex，交还原版
            if (character.IsBaby)
                return true;

            // 市场内购买需先通过金币校验（原版会提示 #No_Gold），交还原版
            if (item.Sector == ESector.Market && !item.AbleToBuy())
                return true;

            // 数量为 1：没有数量条可调，交还原版直接使用
            if (item.Count <= 1)
                return true;

            // 目标本身不适用（与 Item.Use 中特性升级药分支的判定一致）：
            // 交还原版，由原版提示一次 #Not_Apply 且不消耗物品
            if (character.IsDead || character.IsNpc || character.IsNpc2)
                return true;
            if (character.TraitList == null || character.TraitList.Count == 0)
                return true;

            // ★ 与母乳完全相同的数量分配流程：
            //   打开数量条窗口 → 拖动滑块选择数量 → 确定 → 循环 Use N 次
            manager.SetItemAllocation(
                EItemAllocationType.Use, item, item.Sector, new ValueTuple<int, int>(0, 0), 0, character);
            manager.OpenWindow(EGameWindow.ItemAllocation);
            return false;   // 跳过原版 Drop()（否则会同时再只用 1 个）
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError(
                "[CBR][TraitUpgradePill] quantity allocation failed (fallback to vanilla): " + e);
            return true;
        }
    }
}
