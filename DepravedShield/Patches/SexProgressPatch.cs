using DepravedShield.Helpers;
using DepravedShield.Properties;
using HarmonyLib;
using MBMScripts;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DepravedShield.Patches
{
    // ========== 原有 SexProgressPatch（处理性行为进度） ==========
    [HarmonyPatch(typeof(Male), "Sex")]
    public static class SexProgressPatch
    {
        private static Dictionary<int, bool> _wasActive = new();
        private static Dictionary<int, EState> _malePrevState = new();
        private static Dictionary<int, bool> _isIncreasing = new();

        static void Postfix(Male __instance, float deltaTime)
        {
            if (!ModConfig.Enable) return;

            Character partner = __instance.Partner;
            if (partner == null) return;
            if (RoomCheckHelper.IsInKokiRoom(__instance) || RoomCheckHelper.IsInKokiRoom(partner)) return;

            // 活动状态重置
            bool isActive = __instance.State == EState.Sex || __instance.State == EState.SexDrained
                            || __instance.State == EState.CumShotInside || __instance.State == EState.CumShotOutside;
            int id = __instance.UnitId;
            bool wasActive = _wasActive.TryGetValue(id, out bool w) && w;

            if (isActive && !wasActive)
                SexProgressManager.ResetProgress(partner);
            if (!isActive && wasActive)
                SexProgressManager.ResetProgress(partner);
            _wasActive[id] = isActive;

            // 检测状态切换至 CumShot
            EState curMaleState = __instance.State;
            EState prevMaleState = _malePrevState.TryGetValue(id, out EState prev) ? prev : EState.None;

            if (curMaleState != prevMaleState)
            {
                if (curMaleState == EState.CumShotInside || curMaleState == EState.CumShotOutside)
                {
                    int partnerId = partner.UnitId;
                    if (!_isIncreasing.TryGetValue(partnerId, out bool inc) || !inc)
                    {
                        _isIncreasing[partnerId] = true;
                        float duration = ModConfig.IncreaseDuration;
                        float totalPercent = ModConfig.TotalPercent;
                        ModCoroutineHelper.StartCoroutine(SmoothIncreaseProgress(partner, duration, totalPercent / 100f));
                    }
                    // --- 注：血脉（Bloodline）怪物 cumshot 效果由 Bloodline/Patches/BloodlineMonsterCumshotPatch
                    //     独立触发（不依赖堕落护盾开关），此处不再处理。 ---
                }
                _malePrevState[id] = curMaleState;
            }

            // 持续累加（仅 Sex/SexDrained）
            if (isActive)
                SexProgressManager.UpdateSexProgress(partner, deltaTime);
        }

        // ★ 修改后的协程：使用动态速度 * 6
        private static IEnumerator SmoothIncreaseProgress(Character partner, float duration, float totalAmount)
        {
            if (partner == null) yield break;
            float elapsed = 0f;
            float remaining = totalAmount;
            int partnerId = partner.UnitId;

            while (elapsed < duration && partner != null && remaining > 0f)
            {
                float delta = Time.deltaTime;
                if (delta > 0.1f) delta = 0.1f;

                // 获取当前角色的实时速度并乘以 6
                float speed = SexProgressManager.GetSexProgressSpeed(partner);
                float step = speed * 6f * delta;
                if (step > remaining) step = remaining;

                bool orgasm = SexProgressManager.AddProgress(partner, step);
                remaining -= step;
                elapsed += delta;

                if (orgasm)
                    break;
                yield return null;
            }

            if (partner != null)
                _isIncreasing[partnerId] = false;
        }
    }

    // ========== 补丁1：封禁受孕率（辅助，将女性方受孕率设为0） ==========
    [HarmonyPatch(typeof(Character), "ConceptionRate", MethodType.Getter)]
    public static class Patch_ConceptionRate
    {
        static void Postfix(Character __instance, ref float __result)
        {
            if (!ModConfig.Enable) return;
            if (SexProgressManager.GetDepravedShield(__instance) > 0)
                __result = 0f;
        }
    }

    // ========== 补丁2：彻底阻止怀孕（拦截 BeImpregnated） ==========
    [HarmonyPatch(typeof(Character), "BeImpregnated", new[] { typeof(Character) })]
    [HarmonyPriority(Priority.First)]
    public static class Patch_BeImpregnated
    {
        static bool Prefix(Character __instance)
        {
            if (!ModConfig.Enable) return true;
            if (SexProgressManager.GetDepravedShield(__instance) > 0)
            {
                // 修复“空怀孕”：护盾阻止受孕时，若角色处于“怀孕但没有任何胎儿”的异常状态
                // （PregnantType>0 而 FetusUnitIdList 为空），会导致永久怀孕且无法生育，这里清理掉。
                if (__instance.FetusUnitIdList.Count == 0 && __instance.PregnantType > 0)
                {
                    __instance.PregnantType = 0;
                    __instance.IsJustPregnant = false;
                    ModEntry.LogDebug($"{__instance.DisplayName} 检测到空怀孕状态，已清理（护盾阻止受孕）");
                }

                ModEntry.LogDebug($"{__instance.DisplayName} 因护盾存在，阻止怀孕");
                return false;
            }
            return true;
        }
    }

    // ========== 补丁3：阻止生育药水（Item126）使用 ==========
    [HarmonyPatch(typeof(Item), "Use", new[] { typeof(Unit) })] // 指定带 Unit 参数的重载
    public static class Patch_ItemUse
    {
        static bool Prefix(Item __instance, Unit unit)
        {
            if (!ModConfig.Enable) return true;
            // 检查是否为生育药水，且目标是拥有护盾的角色
            if (__instance.ItemType == EItemType.Item_FertilityMedication && unit is Character character)
            {
                if (SexProgressManager.GetDepravedShield(character) > 0)
                {
                    ModEntry.LogDebug($"阻止 {character.DisplayName} 使用生育药水（护盾生效）");
                    // ★ 使用本地化弹窗文本
                    character.PopUpMessage(Strings.Popup_ShieldBlockedFertility, null, 0.5f);
                    return false; // 阻止使用，物品不消耗
                }
            }
            return true;
        }
    }

    // ========== 补丁4：最后防线——阻止直接创建胎儿（NewFetus） ==========
    [HarmonyPatch(typeof(PlayData), "NewFetus", new[] { typeof(Character), typeof(Character) })]
    public static class Patch_NewFetus
    {
        static bool Prefix(Character female, Character male)
        {
            if (!ModConfig.Enable) return true;
            if (SexProgressManager.GetDepravedShield(female) > 0)
            {
                ModEntry.LogDebug($"{female.DisplayName} 因护盾存在，阻止 NewFetus（可能由物品或脚本触发）");
                return false;
            }
            return true;
        }
    }

    // ========== 补丁5：阻止堕落值增加（Training Setter） ==========
    [HarmonyPatch(typeof(Character), "Training", MethodType.Setter)]
    public static class Patch_TrainingSetter
    {
        static bool Prefix(Character __instance, int value)
        {
            if (!ModConfig.Enable) return true;
            if (SexProgressManager.GetDepravedShield(__instance) > 0 && value > __instance.Training)
            {
                ModEntry.LogDebug($"{__instance.DisplayName} 因护盾存在，无法获得堕落值（当前 {__instance.Training}，尝试设为 {value}）");
                return false;
            }
            return true;
        }
    }
}