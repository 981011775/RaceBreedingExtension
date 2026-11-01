#nullable enable
using System;
using ComplexBreedingRedux.Data.Factions;
using ComplexBreedingRedux.Features;
using HarmonyLib;
using MBMScripts;
using UnityEngine;

namespace ComplexBreedingRedux.Patches.ClientPatches;

/// <summary>
/// 派系访客相关的 Harmony 补丁：
/// - Client.InitializeTrait：生成时赋予随机“已解锁派系”（Prefix）+ 品阶/财富 Buff（Postfix）；
/// - ClientData.GetPossibilityOfVenerealDisease：改用派系自身的性病几率；
/// - Client.PayFee：派系访客按“品阶×声誉”的财富值付款并提升派系声誉，公主职业 ×10。
/// </summary>
internal static class FactionSystemPatches
{
    // ══════════════════════ Client.InitializeTrait ══════════════════════

    /// <summary>在原版 InitializeTrait 之前分配派系 DataType；之后原版仍会随机 EPreferenceType（原版偏好系统）。</summary>
    [HarmonyPatch(typeof(Client), nameof(Client.InitializeTrait), new Type[0])]
    internal static class FactionClientInitializeTraitPatch
    {
        public static void Prefix(Client __instance)
        {
            try { FactionSystem.AssignFactionToClient(__instance); }
            catch (Exception e) { Debug.LogError($"[CBR][Faction] InitializeTrait prefix failed: {e}"); }
        }

        public static void Postfix(Client __instance)
        {
            try { FactionSystem.ApplyVisitorRankAndWealth(__instance); }
            catch (Exception e) { Debug.LogError($"[CBR][Faction] InitializeTrait postfix failed: {e}"); }
        }
    }

    // ══════════════════════ 性病几率 ═══════════════════════════════════

    /// <summary>把访客的性病几率换成所属派系配置（取代原版 DataType 数组）。</summary>
    [HarmonyPatch(typeof(ClientData), nameof(ClientData.GetPossibilityOfVenerealDisease))]
    internal static class FactionClientVenerealDiseasePatch
    {
        public static void Postfix(int index, ref int __result)
        {
            if (FactionSystem.TryGetFaction(index, out FactionData? faction) && faction != null)
                __result = faction.VenerealDiseaseChance;
        }
    }

    // ══════════════════════ PayFee（派系财富 + 派系声誉 + 公主 ×10） ═══════════

    /// <summary>
    /// 派系访客结账（<c>Client.PayFee</c>）：
    /// - Prefix：按「品阶 + 派系声誉」支付派系财富，并按品阶提升该派系声誉（公主 ×10）；
    /// - Postfix：仅当服务者是「公主（Princess）」职业时，把**派系财富**再补发 9 倍（合计 ×10）。
    ///
    /// ★ 与原版营业额的关系（重要）：
    ///   原版 <c>Client.PayFee</c> 的营业额（基于奴隶价格与 Trait44 / Trait71 的特质加成）
    ///   以及原版声望一律**不受本模组倍率影响**（保持原样）。
    ///   旧实现把「整笔差额（含原版营业额）」再 ×9，且另有 PrincessPayFeePatch 对整笔金额
    ///   ×10、对原版声望也 ×10 —— 两者叠加使一笔结账放大到 ×100，
    ///   高品阶客户一次出现几十万金钱、原版声望也被抬高 10 倍。现全部修正为：
    ///   公主倍率只作用于「派系财富」与「派系声誉」。
    /// </summary>
    [HarmonyPatch(typeof(Client), nameof(Client.PayFee))]
    internal static class FactionPayFeePatch
    {
        /// <summary>本次结账支付的「派系财富」（供 Postfix 计算公主加成；0 = 未结算）。</summary>
        private static int _factionWealthPaid;

        public static bool Prefix(Client __instance, Character partner)
        {
            // 每次结账先清零，避免异常路径把上一次的金额残留到本次加成
            _factionWealthPaid = 0;

            if (__instance == null || partner == null || __instance.Data is not ClientData)
                return true;
            if (!FactionSystem.TryGetFaction(__instance.DataType, out FactionData? faction) || faction == null)
                return true;

            try
            {
                int tier = FactionSystem.GetVisitorTier(__instance);
                bool isPrincess = FactionSystem.IsPrincessProfession(partner);

                // 派系财富 = 客户 UI 上显示的财富值（不含公主倍率）
                int wealth = FactionSystem.GetVisitorWealth(__instance);
                _factionWealthPaid = wealth;

                // 派系声誉：品阶决定基础值（原来的十分之一）→ 再乘「相乘倍率」
                //   · 公主（Princess）职业：×10
                //   · 血脉（堕落 + 血脉所属派系 == 结算派系）：×（1 + 0.5 × 等级）→ +1 = ×1.5、+10 = ×6
                //   两者「相乘」：血脉 +10 时 = 6 × 10 = 60 倍（原为加算 16 倍）。
                int reward = FactionSystem.ComputeReputationReward(faction.Id, tier);
                int depravedBloodlineLevel = FactionSystem.GetDepravedBloodlineLevelForFaction(partner, faction);
                float repMultiplier = FactionSystem.CombineFactionReputationMultiplier(isPrincess, depravedBloodlineLevel);
                FactionSystem.AddReputation(faction.Id, Mathf.Max(1, Mathf.RoundToInt(reward * repMultiplier)));

                // 派系营业额入账（原版营业额由原版方法自行结算，二者互不放大）
                GameManager.Instance.PlayerData.Gold += wealth;
                partner.PopUpMessage($"+{wealth}", "Object[resource_gold_128]", 0.25f);
            }
            catch (Exception e)
            {
                Debug.LogError($"[CBR][Faction] PayFee prefix failed: {e}");
            }
            return true;
        }

        /// <summary>
        /// 公主加成：只把「派系财富」补发到 10 倍（额外 +9 倍）。
        /// 不使用「结账前后金币差额」（差额里含原版营业额，会把原版营业额也放大 10 倍）。
        /// </summary>
        public static void Postfix(Client __instance, Character partner)
        {
            int wealth = _factionWealthPaid;
            _factionWealthPaid = 0;

            if (wealth <= 0 || partner == null || !FactionSystem.IsPrincessProfession(partner))
                return;

            try
            {
                int extra = PrincessPayFeePatch.GetExtraWealth(wealth);
                if (extra <= 0)
                    return;

                GameManager.Instance.PlayerData.Gold += extra;
                partner.PopUpMessage($"+{extra}", "Object[resource_gold_128]", 0.25f);
            }
            catch (Exception e)
            {
                Debug.LogError($"[CBR][Faction] PayFee postfix failed: {e}");
            }
        }
    }
}
