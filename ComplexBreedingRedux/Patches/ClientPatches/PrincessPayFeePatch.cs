#nullable enable
using ComplexBreedingRedux.Features;

namespace ComplexBreedingRedux.Patches.ClientPatches;

/// <summary>
/// 公主（Princess）职业的「派系营业额 / 派系声誉」倍率。
///
/// ★ 历史问题（已修正）：
///   本类过去是一个 <c>[HarmonyPatch(typeof(Client), nameof(Client.PayFee))]</c>，
///   它把**原版整笔营业额**与**原版声望**都 ×10（<c>pd.Gold</c> / <c>pd.Reputation</c>），
///   而 <see cref="FactionSystemPatches.FactionPayFeePatch"/> 又对结账前后金币差额 ×10，
///   两者叠加使一笔结账被放大到 ×100：高品阶客户一次获得几十万金钱，原版声望也被抬高 10 倍。
///
/// ★ 现在的规则（只影响模组自己的派系系统）：
///   · 派系营业额（访客财富值）×10 —— 由 FactionSystemPatches.FactionPayFeePatch 结算；
///   · 派系声誉 ×10                 —— 同一处结算，且与血脉（Bloodline）效果**相乘**：
///         Mult = 10（公主）×（1 + 0.5 × 血脉等级）（堕落 + 血脉所属派系一致）
///         血脉+10 时 = 6 × 10 = 60 倍（原为加算 16 倍），见 FactionSystem.CombineFactionReputationMultiplier；
///   · 原版 <c>Client.PayFee</c> 的营业额（含 Trait44 / Trait71 特质加成）与原版声望
///     完全不参与任何倍率。
///
/// 本类不再注册任何 Harmony 补丁，只提供倍率计算，避免两处补丁重复结算同一笔金额。
/// </summary>
internal static class PrincessPayFeePatch
{
    /// <summary>公主的派系营业额倍率。</summary>
    public static int RevenueMultiplier => FactionSystem.PrincessRevenueMultiplier;

    /// <summary>在基础派系财富之上，公主需要额外补发的金额（金额 ≤ 0 时返回 0）。</summary>
    public static int GetExtraWealth(int factionWealth)
        => factionWealth <= 0 ? 0 : factionWealth * (RevenueMultiplier - 1);
}
