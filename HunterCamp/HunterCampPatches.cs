#nullable enable
using System;
using HarmonyLib;
using MBMScripts;

namespace RaceBreedingExtension.HunterCamp
{
    /// <summary>
    /// 猎手营地（个人领地地面营地）补丁。
    ///
    /// 旧版“房间升级(RoomUpgrades) → 猎手营地技能 / 每日入夜捕捉 / 房间技能文字 / 奴隶房间限制”
    /// 的全部补丁已按需求 9 删除：原版房间升级行为完全回归原版，
    /// 营地的功能现在全部由 <see cref="HunterCampSystem"/> 建立在
    /// 个人领地地面的「猎手营地 / 大型猎手营地」房间上。
    ///
    /// 现存补丁：
    ///  - <c>Room.OnTickUpdate</c>：营地自动生成奴隶 / 逃跑判定 / 拖动锁 / 与怪物配对 / 显示层级；
    ///  - <c>PlayData.TickUpdate</c>：每秒一次的全局维护（俘虏离开营地 → 解除标记）；
    ///  - <c>UnitComponent.Reposition</c>：非 sex 状态下让俘虏显示在怪物前面（需求 8）；
    ///  - <c>PlayData.DraggingUnit</c>（setter）：兜底阻止拖动被锁定的俘虏（需求 5）；
    ///  - <c>InteractionPrivateEstate.*Estate()</c>（4 个）与 <c>PlayData.MoveTo*Estate()</c>（4 个）：
    ///    ★ 阻止「Move To Estate」键位把营地俘虏搬去黑市（复用原版「不是你拥有的」提示）。
    /// </summary>
    internal static class HunterCampPatches
    {
        // ============================ 营地主循环 ============================

        [HarmonyPatch(typeof(Room), nameof(Room.OnTickUpdate))]
        internal static class RoomTickPatch
        {
            public static void Postfix(Room __instance, float tickTime)
            {
                try
                {
                    HunterCampSystem.ProcessRoom(__instance, tickTime);
                }
                catch (Exception e)
                {
                    HunterCampSystem.LogError("Room.OnTickUpdate failed: " + e);
                }
            }
        }

        // ============================ 全局维护（每秒） ============================

        [HarmonyPatch(typeof(PlayData), nameof(PlayData.TickUpdate))]
        internal static class PlayDataTickPatch
        {
            public static void Postfix(PlayData __instance, float tickTime)
            {
                try
                {
                    HunterCampSystem.SweepCaptives(__instance, tickTime);
                }
                catch (Exception e)
                {
                    HunterCampSystem.LogError("PlayData.TickUpdate failed: " + e);
                }
            }
        }

        // ============================ 显示层级（需求 8） ============================

        [HarmonyPatch(typeof(UnitComponent), nameof(UnitComponent.Reposition))]
        internal static class RepositionLayerPatch
        {
            public static void Postfix(UnitComponent __instance)
            {
                try
                {
                    TargetUnit? target = __instance.GetComponent<TargetUnit>();
                    if (target?.Unit is Female female)
                        HunterCampSystem.RefreshLayerOrder(female);
                }
                catch (Exception e)
                {
                    HunterCampSystem.LogError("UnitComponent.Reposition failed: " + e);
                }
            }
        }

        // ============================ 拖动兜底（需求 5） ============================

        [HarmonyPatch(typeof(PlayData), "set_DraggingUnit")]
        internal static class DraggingUnitPatch
        {
            public static bool Prefix(Unit value)
            {
                try
                {
                    return HunterCampSystem.CanStartDrag(value);
                }
                catch
                {
                    return true;
                }
            }
        }
        // ============================ 俘虏体力/心态变动修正 ============================

        /// <summary>
        /// 原版 <c>Female.UpdateMentalityVariation</c> 在“槽位 Y &gt; 0 或 个人领地(IsPrivate)”时
        /// 会**丢弃**一切体力下降（个人领地不消耗资源的规则），导致营地俘虏的体力永远不变、
        /// 也永远不会进入乏力(Drained)状态。
        /// 这里只对“营地俘虏”重写该方法：保留原版全部判定，仅去掉个人领地/地上槽位的丢弃分支。
        /// </summary>
        [HarmonyPatch(typeof(Female), "UpdateMentalityVariation")]
        internal static class CaptiveMentalityVariationPatch
        {
            public static bool Prefix(Female __instance)
            {
                try
                {
                    if (!HunterCampSystem.IsCaptive(__instance))
                        return true;    // 非俘虏 → 完全走原版

                    if (__instance.TraitContains(ETrait.Trait107)
                        && __instance.MentalityVariation >= 0f && __instance.MentalityVariation < 1f)
                    {
                        __instance.MentalityVariation = 1f;
                    }

                    if (__instance.MentalityVariation == 0f)
                        return false;

                    if (__instance.MentalityVariation > 0f
                        && (__instance.Health >= __instance.MaxHealth || __instance.Hunger || __instance.IsDead))
                    {
                        __instance.MentalityVariation = 0f;
                        return false;
                    }

                    if (__instance.MentalityVariation < 0f)
                    {
                        // 原版此处还有 `Slot.Item2 > 0 || IsPrivate` 的丢弃条件（个人领地不消耗），
                        // 对营地俘虏去掉该条件，使其体力像普通奴隶一样下降。
                        if (__instance.Health <= 0f || __instance.Training >= 50)
                        {
                            __instance.MentalityVariation = 0f;
                            return false;
                        }

                        if (__instance.TraitContains(ETrait.Trait72))
                            __instance.MentalityVariation *= GameManager.GetTraitValue(ETrait.Trait72, 0);
                    }

                    __instance.Health += __instance.MentalityVariation;
                    __instance.MentalityVariation = 0f;
                    return false;
                }
                catch (Exception e)
                {
                    HunterCampSystem.LogError("Female.UpdateMentalityVariation failed: " + e);
                    return true;
                }
            }
        }

        // ====== 阻止「Move To Estate / 移动到领地」键位搬走营地俘虏（不涉及任何 UI 修改） ======

        /// <summary>
        /// 个人领地界面中央下方的「移动到领地 / 移动到黑市」键位（本地化键
        /// <c>#MoveToEstateFormat</c> / <c>#MoveToManagementEstate</c> / <c>#MoveToPrivateEstate</c>）
        /// 走的是 <c>InteractionPrivateEstate.SelectedUnitMoveToManagementEstate()</c> 等 4 个方法：
        /// 它们只判断「单位所在 Sector」（不在 Female/Male/Baby 时按"不是你拥有的"拦截），
        /// 而营地俘虏位于个人领地的 Female Sector → 会被直接搬进黑市（管理区）的笼子，
        /// 绕过本模组的拖动锁（IsNotDraggable）。
        ///
        /// 这里对营地俘虏复用原版表现：弹 <c>#Not_Your_Slave</c>（雄性为 <c>#Not_Your_Monster</c>）
        /// 并阻止移动，等价于原版"不是自己拥有的奴隶"的处理。
        /// </summary>
        private static bool BlockCaptiveEstateMove(bool useSelectedUnit)
        {
            try
            {
                GameManager? manager = GameManager.Instance;
                PlayData? playerData = manager?.PlayerData;
                if (playerData == null || manager == null)
                    return true;

                Unit? unit = useSelectedUnit ? playerData.SelectedUnit : playerData.DraggingUnit;
                Character? character = unit as Character;
                if (HunterCampSystem.IsCaptive(character))
                {
                    manager.AddSystemMessage(character is Female ? "#Not_Your_Slave" : "#Not_Your_Monster");
                    return false;   // 阻止移动
                }
            }
            catch (Exception e)
            {
                HunterCampSystem.LogError("BlockCaptiveEstateMove failed: " + e);
            }
            return true;
        }

        /// <summary>俘虏一律不允许被「移动到领地 / 黑市」的底层方法搬走（兜底，静默阻止）。</summary>
        private static bool IsCaptiveUnit(Unit? unit)
        {
            try
            {
                return HunterCampSystem.IsCaptive(unit as Character);
            }
            catch (Exception e)
            {
                HunterCampSystem.LogError("IsCaptiveUnit failed: " + e);
                return false;
            }
        }

        /// <summary>批量移动前把俘虏从多选列表里剔除（多选分支是内联 Allocate，只能在此拦截）。</summary>
        private static void RemoveCaptivesFromMultiSelection(PlayData? playerData)
        {
            try
            {
                if (playerData == null)
                    return;

                SeqList<Unit> list = playerData.MultiSelectedUnitSeqList;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    if (HunterCampSystem.IsCaptive(list[i] as Character))
                        list.RemoveAt(i);
                }
            }
            catch (Exception e)
            {
                HunterCampSystem.LogError("RemoveCaptivesFromMultiSelection failed: " + e);
            }
        }

        /// <summary>「移动到黑市（管理区）」键位：俘虏 → 提示并阻止。</summary>
        [HarmonyPatch(typeof(InteractionPrivateEstate), "SelectedUnitMoveToManagementEstate")]
        internal static class CaptiveSelectedMoveToManagementEstatePatch
        {
            public static bool Prefix() => BlockCaptiveEstateMove(useSelectedUnit: true);
        }

        /// <summary>「移动到个人领地」键位：俘虏 → 提示并阻止。</summary>
        [HarmonyPatch(typeof(InteractionPrivateEstate), "SelectedUnitMoveToPrivateEstate")]
        internal static class CaptiveSelectedMoveToPrivateEstatePatch
        {
            public static bool Prefix() => BlockCaptiveEstateMove(useSelectedUnit: true);
        }

        /// <summary>拖动状态下按「移动到黑市」键位：俘虏 → 提示并阻止。</summary>
        [HarmonyPatch(typeof(InteractionPrivateEstate), "DraggingUnitMoveToManagementEstate")]
        internal static class CaptiveDraggingMoveToManagementEstatePatch
        {
            public static bool Prefix() => BlockCaptiveEstateMove(useSelectedUnit: false);
        }

        /// <summary>拖动状态下按「移动到个人领地」键位：俘虏 → 提示并阻止。</summary>
        [HarmonyPatch(typeof(InteractionPrivateEstate), "DraggingUnitMoveToPrivateEstate")]
        internal static class CaptiveDraggingMoveToPrivateEstatePatch
        {
            public static bool Prefix() => BlockCaptiveEstateMove(useSelectedUnit: false);
        }

        /// <summary>底层移动方法兜底（任何调用方都不允许把俘虏搬去黑市笼子）。</summary>
        [HarmonyPatch(typeof(PlayData), nameof(PlayData.MoveToManagementEstate))]
        internal static class CaptivePlayDataMoveToManagementEstatePatch
        {
            public static bool Prefix(Unit unitToMove) => !IsCaptiveUnit(unitToMove);
        }

        /// <summary>底层移动方法兜底（反向同理）。</summary>
        [HarmonyPatch(typeof(PlayData), nameof(PlayData.MoveToPrivateEstate))]
        internal static class CaptivePlayDataMoveToPrivateEstatePatch
        {
            public static bool Prefix(Unit unitToMove) => !IsCaptiveUnit(unitToMove);
        }

        /// <summary>多选批量「移动到黑市」：先把俘虏移出多选列表。</summary>
        [HarmonyPatch(typeof(PlayData), nameof(PlayData.MoveMultiSelectedUnitToManagementEstate))]
        internal static class CaptiveMultiMoveToManagementEstatePatch
        {
            public static void Prefix(PlayData __instance) => RemoveCaptivesFromMultiSelection(__instance);
        }

        /// <summary>多选批量「移动到个人领地」：先把俘虏移出多选列表。</summary>
        [HarmonyPatch(typeof(PlayData), nameof(PlayData.MoveMultiSelectedUnitToPrivateEstate))]
        internal static class CaptiveMultiMoveToPrivateEstatePatch
        {
            public static void Prefix(PlayData __instance) => RemoveCaptivesFromMultiSelection(__instance);
        }
    }
}
