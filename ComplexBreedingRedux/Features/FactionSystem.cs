#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ComplexBreedingRedux.Data;
using ComplexBreedingRedux.Data.Config;
using ComplexBreedingRedux.Data.Factions;
using ComplexBreedingRedux.Data.Professions;
using MBMScripts;
using RaceBreedingExtension.Bloodline;
using RaceBreedingExtension.Bloodline.Data;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ComplexBreedingRedux.Features;

/// <summary>
/// 派系系统（FactionSystem）。
///
/// 已按精简要求改造：
/// - 保留声誉系统（声誉分数保存在阿米莉亚的 Buff 中）；
/// - 删除全部“精华系统 / 访客职业 / 访客自定义偏好”；
/// - 访客只带派系 + 品阶（1~5，与职业品阶同步）；声望越高，高阶品阶访客概率越大、财富越多；
/// - 访客携带所属派系的血脉（见 FactionRegistry.Bloodlines），等级只由「模组派系声誉等级」决定：
///   Unknown 1-3 / Obscure 1-4 / Insignificant 1-5 / Noteworthy 1-6 / Reputable 1-7 /
///   Significant 1-8 / Famous 1-9 / Fabled 1-10（★ 不参考原版声望等级、不受品阶影响）；
/// - 访客的偏好使用原游戏 EPreferenceType（不再屏蔽、不再自定义匹配）；
/// - PayFeePatch 保留：访客支付能力（财富值）由品阶与派系声誉共同决定。
///
/// ★ 经济平衡（本轮调整，目标值）：
///   · 品阶财富曲线改为「平滑」：tier1 → tier5 仅约 2.6 倍（旧值 80→3000 相差 37 倍，
///     任何倍率都会瞬间爆表）；
///   · 声誉财富倍率同样平滑：1.00 → 2.00（每级约 +10%，无跳变）；
///   · 公主（Princess）的 ×10 **只作用于模组自己的派系系统**（派系营业额 + 派系声誉），
///     原版金钱（Client.PayFee 的营业额）与原版声望完全不受影响；
///   · 因此「最高声誉 + 最高品阶客户 + 公主」一次结账的派系营业额 = 420 × 10 = 4200，
///     叠加原版营业额（含 Trait44 / Trait71 特质加成）后可达 5000~12000；
///   · 派系声誉：基础值 = 品阶 1/2/3/4/5（原 6/12/20/32/50 的十分之一量级）；
///     实际入账 = 基础值 ×「相乘倍率」= 公主 ×10、堕落且血脉所属派系一致 ×（1 + 0.5 × 血脉等级），
///     血脉+10 + 公主 = 6 × 10 = 60 倍，见 <see cref="CombineFactionReputationMultiplier"/>。
/// </summary>
internal static class FactionSystem
{
    /// <summary>派系声誉分数上限（Fabled 为 30000~99999）。</summary>
    public const int MaxReputationScore = 99999;

    /// <summary>综合声望等级的最大索引（0~7，对应 <see cref="EFactionReputation"/> 的 Unknown~Fabled）。</summary>
    public const int MaxReputationIndex = 7;

    /// <summary>
    /// 公主（Princess）职业倍数：10 倍派系营业额 / 10 倍派系声誉。
    /// ★ 仅作用于模组自己的派系系统（访客财富值 + 派系隐藏声誉），
    ///   原版金钱（Client.PayFee 的营业额）与原版声望不参与该倍率。
    /// </summary>
    public const int PrincessRevenueMultiplier = 10;

    // ─── 品阶（1~5，与职业品阶同步）经济平衡 ──────────────────────────────

    /// <summary>
    /// 各品阶访客的基准财富（索引 = 品阶，不含声誉倍率与公主倍率）。
    /// 平滑曲线：品阶 1→5 仅约 2.6 倍（旧值 80→3000 相差 37 倍，任何倍率都会瞬间爆表）。
    /// </summary>
    public static readonly int[] TierWealthBase = { 0, 80, 110, 145, 175, 210 };

    /// <summary>
    /// 各品阶访客每次消费为所属派系带来的**基础**声誉（索引 = 品阶）= 1 / 2 / 3 / 4 / 5。
    /// （原基础奖励为 6/12/20/32/50；本轮已按「原来的十分之一」量级校准为 1/2/3/4/5。）
    /// 实际入账 = 本值（高声誉时按 <see cref="ReputationRewardDiminishPerLevel"/> 递减，至少 1）
    ///           ×「相乘倍率」（公主 ×10、堕落血脉 ×（1 + 0.5 × 等级）），
    /// 见 <see cref="ComputeReputationReward"/> 与 <see cref="CombineFactionReputationMultiplier"/>。
    /// </summary>
    public static readonly int[] TierReputationReward = { 0, 1, 2, 3, 4, 5 };

    /// <summary>
    /// 声誉等级每高一级，单次基础奖励减少 0.1（等价于旧写法 (原值 − 声誉等级) × 0.1 的递减幅度）。
    /// </summary>
    public const float ReputationRewardDiminishPerLevel = 0.1f;

    /// <summary>
    /// 各声誉等级（索引 = (int)EFactionReputation）可出现的最高客户品阶。
    /// 与 <see cref="TierDistributionByReputation"/> 表的非零列保持一致（RollVisitorTier 会以此上限兜底）。
    /// </summary>
    public static readonly int[] ReputationMaxTier = { 1, 2, 2, 3, 4, 4, 5, 5 };

    /// <summary>
    /// 按声誉等级加权的品阶分布（每行 5 个数对应品阶 1~5 的概率百分数）。
    /// 平滑过渡：每一级只把概率向高品阶推移一点（旧表存在 0→45% 的突变），
    /// 且不会超过 <see cref="ReputationMaxTier"/> 给出的上限。
    /// </summary>
    public static readonly int[][] TierDistributionByReputation =
    {
        new[] { 100,  0,  0,  0,  0 }, // Unknown       (    0) → 只有品阶 1
        new[] {  85, 15,  0,  0,  0 }, // Obscure       (   30)
        new[] {  70, 30,  0,  0,  0 }, // Insignificant (  100)
        new[] {  48, 36, 16,  0,  0 }, // Noteworthy    (  250) → 出现品阶 3
        new[] {  32, 34, 26,  8,  0 }, // Reputable     (  600) → 出现品阶 4
        new[] {  18, 28, 33, 21,  0 }, // Significant   ( 1400)
        new[] {   6, 16, 28, 32, 18 }, // Famous        ( 3000) → 出现品阶 5
        new[] {   0,  8, 20, 34, 38 }, // Fabled        ( 6000) → 极高品阶占多数
    };

    /// <summary>声誉分数对财富的放大倍率（索引 = 声誉等级；平滑 1.00 → 2.00，每级约 +10%）。</summary>
    public static readonly float[] ReputationWealthScale =
        { 1.00f, 1.10f, 1.21f, 1.33f, 1.46f, 1.61f, 1.78f, 2.00f };

    // ─── Buff 数值辅助 ─────────────────────────────────────────────────────

    public static int GetBuffInt(EBuffType type, Character unit)
    {
        Buff? buff = unit?.GetBuff(type);
        if (buff == null || string.IsNullOrWhiteSpace(buff.SpriteName))
            return 0;
        return int.TryParse(buff.SpriteName, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? value
            : 0;
    }

    public static void SetBuffInt(EBuffType type, int value, Character unit)
    {
        if (unit == null)
            return;
        if (unit.GetBuff(type) != null)
            unit.RemoveBuff(type);
        if (value > 0)
            unit.AddBuff(type, 0f, true, false, value.ToString(CultureInfo.InvariantCulture));
    }

    // ─── 派系查找 ──────────────────────────────────────────────────────────

    public static bool TryGetFaction(int id, out FactionData? faction) => GameData.TryGetFaction(id, out faction);

    // ─── 声誉系统 ──────────────────────────────────────────────────────────

    /// <summary>
    /// 声誉分数 → 声誉等级（分数区间）：
    ///   Unknown        0 ~ 199
    ///   Obscure      200 ~ 499
    ///   Insignificant 500 ~ 999
    ///   Noteworthy  1000 ~ 1999
    ///   Reputable   2000 ~ 4999
    ///   Significant 5000 ~ 9999
    ///   Famous     10000 ~ 29999
    ///   Fabled     30000 ~ 99999
    /// </summary>
    public static EFactionReputation GetReputationTier(int reputation)
    {
        if (reputation < 200)    return EFactionReputation.Unknown;
        if (reputation < 500)    return EFactionReputation.Obscure;
        if (reputation < 1000)   return EFactionReputation.Insignificant;
        if (reputation < 2000)   return EFactionReputation.Noteworthy;
        if (reputation < 5000)   return EFactionReputation.Reputable;
        if (reputation < 10000)  return EFactionReputation.Significant;
        if (reputation < 30000)  return EFactionReputation.Famous;
        return EFactionReputation.Fabled;
    }

    /// <summary>
    /// 声誉 Buff 的宿主列表：ESector.Special 中所有 Amilia / Amilia2
    /// （阿米莉亚剧情后期会变成 Amilia2，且场景可能同时存在多个变体），
    /// 一个都没有时回退到玩家角色（任何时期都存在于 ESector.Npc）。
    /// 读取/写入会遍历全部宿主并按“最大值”合并，避免“老档分数存在旧阿米莉亚身上、
    /// 读取却命中复制体”导致声望数值丢失。
    /// </summary>
    private static List<Character> GetReputationHosts()
    {
        var hosts = new List<Character>();
        PlayData? pd = GameManager.Instance?.PlayerData;
        if (pd == null)
            return hosts;

        foreach (Unit unit in pd.GetUnitList(ESector.Special))
        {
            if (unit is Amilia || unit is Amilia2)
                hosts.Add(unit as Character);
        }
        if (hosts.Count == 0)
        {
            foreach (Unit unit in pd.GetUnitList(ESector.Npc))
            {
                if (unit is Player)
                    hosts.Add(unit as Character);
            }
        }
        return hosts;
    }

    /// <summary>把“id,分;...”字符串并入字典（同一派系取更大值，天然兼容多宿主副本）。</summary>
    private static void MergeReputationTextInto(string? text, Dictionary<int, int> scores)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        string raw = text!;
        string[] entries = raw.Contains(';') ? raw.Split(';') : new[] { raw };
        foreach (string entry in entries)
        {
            string[] kv = entry.Split(',');
            if (kv.Length < 2)
                continue;
            if (!int.TryParse(kv[0], out int id) || !int.TryParse(kv[1], out int score))
                continue;

            if (scores.TryGetValue(id, out int current))
            {
                if (score > current)
                    scores[id] = score;
            }
            else
            {
                scores[id] = score;
            }
        }
    }

    /// <summary>读取某派系的声誉分数（遍历所有宿主取最大值；不存在则 0）。</summary>
    public static int GetReputation(int factionId)
    {
        foreach (Character host in GetReputationHosts())
        {
            if (host == null)
                continue;
            Buff? buff = host.GetBuff(ConfigBuffs.FactionsReputations);
            if (buff == null || string.IsNullOrWhiteSpace(buff.SpriteName))
                continue;

            var scores = new Dictionary<int, int>();
            MergeReputationTextInto(buff.SpriteName, scores);
            if (scores.TryGetValue(factionId, out int score))
                return score;
        }
        return 0;
    }

    /// <summary>为某派系增加声誉（合并全部宿主上的现存分数后累加，并写回所有宿主）。</summary>
    public static void AddReputation(int factionId, int value)
    {
        if (value <= 0)
            return;

        List<Character> hosts = GetReputationHosts();
        if (hosts.Count == 0)
            return;

        var scores = new Dictionary<int, int>();
        foreach (Character host in hosts)
        {
            if (host == null)
                continue;
            Buff? buff = host.GetBuff(ConfigBuffs.FactionsReputations);
            if (buff != null)
                MergeReputationTextInto(buff.SpriteName, scores);
        }

        if (scores.TryGetValue(factionId, out int current))
            scores[factionId] = Mathf.Min(MaxReputationScore, current + value);
        else
            scores[factionId] = Mathf.Min(MaxReputationScore, value);

        string payload = string.Join(";", scores.OrderBy(x => x.Key).Select(x => $"{x.Key},{x.Value}"));
        foreach (Character host in hosts)
        {
            if (host == null)
                continue;
            if (host.GetBuff(ConfigBuffs.FactionsReputations) != null)
                host.RemoveBuff(ConfigBuffs.FactionsReputations);
            host.AddBuff(ConfigBuffs.FactionsReputations, 0f, true, false, payload);
        }
    }

    // ─── 综合声誉（派系隐藏声望 与 玩家原版声望等级 取高者） ──────────────

    /// <summary>
    /// 综合声誉等级索引 = max(该派系隐藏声誉等级, 玩家原版可见声望等级)（0~7）。
    /// 用于解锁、品阶分布与财富倍率，保证“声望提高必然能解锁/出现更高品阶访客”。
    /// </summary>
    public static int GetEffectiveReputationIndex(int factionId)
    {
        int factionIdx = (int)GetReputationTier(GetReputation(factionId));
        int playerLevel = GameManager.Instance?.PlayerData?.ReputationLevel ?? 0;
        return Mathf.Clamp(Mathf.Max(factionIdx, playerLevel), 0, 7);
    }

    public static EFactionReputation GetEffectiveReputationTier(int factionId)
        => (EFactionReputation)GetEffectiveReputationIndex(factionId);

    /// <summary>所有派系中最高的综合声誉等级（用于解锁判断）。</summary>
    public static int GetMaxEffectiveReputationIndex()
    {
        int max = 0;
        foreach (FactionData faction in GameData.FactionDataList)
            max = Mathf.Max(max, GetEffectiveReputationIndex(faction.Id));
        return max;
    }

    // ─── 模组派系声誉（不含原版声望）：血脉等级 / 猎手营地生成专用 ──────────

    /// <summary>
    /// 该派系自身的声誉分数（仅模组派系系统；不含玩家原版声望）。
    /// </summary>
    public static int GetFactionReputationScore(int factionId) => GetReputation(factionId);

    /// <summary>
    /// 该派系自身的声誉等级索引（0~7）——★ 仅由模组派系声誉决定，**不参考原版声望等级**。
    /// 用于访客血脉等级、猎手营地生成奴隶的属性/血脉强度。
    /// </summary>
    public static int GetFactionReputationIndex(int factionId)
        => Mathf.Clamp((int)GetReputationTier(GetReputation(factionId)), 0, MaxReputationIndex);

    /// <summary>
    /// 该派系自身声誉的归一化值（0~1）= 声誉分数 / <see cref="MaxReputationScore"/>。
    /// 用于「声望越高 → 生成的奴隶属性越接近最优端」的连续插值（99999 → 1.0）。
    /// </summary>
    public static float GetFactionReputationNormalized(int factionId)
        => Mathf.Clamp01(GetReputation(factionId) / (float)MaxReputationScore);

    // ─── 品阶 / 财富 / 声誉奖励 ────────────────────────────────────────────

    /// <summary>按声誉等级掷访客品阶（1~5，且不超过该声誉等级的 <see cref="ReputationMaxTier"/> 上限）。</summary>
    public static int RollVisitorTier(EFactionReputation repTier)
    {
        int repIdx = Mathf.Clamp((int)repTier, 0, TierDistributionByReputation.Length - 1);
        int[] distribution = TierDistributionByReputation[repIdx];
        int maxTier = ReputationMaxTier[Mathf.Clamp(repIdx, 0, ReputationMaxTier.Length - 1)];
        int roll = Random.Range(0, 100);
        int cumulative = 0;
        for (int tier = 1; tier <= 5; tier++)
        {
            cumulative += distribution[tier - 1];
            if (roll < cumulative)
                return Mathf.Clamp(tier, 1, maxTier);
        }
        return 1;
    }

    /// <summary>
    /// 访客的支付能力（派系财富值）= 品阶基准财富 × 综合声誉倍率（两者都是平滑曲线）。
    /// 公主（Princess）的 ×10 不在这里体现 —— 它在结账时只对本值生效（见 FactionPayFeePatch），
    /// 因此客户 UI 上显示的财富值 = 非公主时的实际支付额。
    /// 参考上限：品阶 5 + Fabled 声誉 = 210 × 2.00 = 420；公主一次结账 ×10 = 4200。
    /// </summary>
    public static int ComputeVisitorWealth(int factionId, int tier)
    {
        int clampedTier = Mathf.Clamp(tier, 1, 5);
        int repIdx = GetEffectiveReputationIndex(factionId);
        float baseWealth = TierWealthBase[clampedTier];
        return Mathf.RoundToInt(baseWealth * ReputationWealthScale[repIdx]);
    }

    /// <summary>
    /// 一次消费为该派系增加的**基础**声誉：品阶 1~5 → 1 / 2 / 3 / 4 / 5
    /// （原基础奖励 6/12/20/32/50 的十分之一量级）；综合声誉越高时按
    /// <see cref="ReputationRewardDiminishPerLevel"/> 递减，且至少为 1。
    /// 实际的派系声誉入账还需再乘「相乘倍率」：公主 ×10、堕落血脉 ×（1 + 0.5 × 等级），
    /// 见 <see cref="CombineFactionReputationMultiplier"/>。
    /// </summary>
    public static int ComputeReputationReward(int factionId, int tier)
    {
        int clampedTier = Mathf.Clamp(tier, 1, 5);
        int repIdx = GetEffectiveReputationIndex(factionId);
        float value = TierReputationReward[clampedTier] - repIdx * ReputationRewardDiminishPerLevel;
        return Mathf.Max(1, Mathf.RoundToInt(value));
    }

    // ─── 血脉的派系归属 & 堕落时的派系声望倍率（本轮新增） ──────────────────

    /// <summary>
    /// 某血脉所属的派系（以 FactionRegistry 为唯一依据；没有被任何派系列出时返回 null）：
    ///   王国派系 → 自然 / 野性；恶魔派系 → 恶魔；怪物派系 → 怪物 / 龙之；
    ///   宗教教团 → 神圣；奥秘议会 → 魔力 / 元素。
    /// </summary>
    public static FactionData? GetFactionForBloodline(EBloodline bloodline)
    {
        foreach (FactionData faction in FactionRegistry.All)
        {
            if (faction.Bloodlines.Contains(bloodline))
                return faction;
        }
        return null;
    }

    /// <summary>
    /// 服务者（女性）处于堕落状态、且其血脉属于「正在结算的派系」时返回其血脉等级（0 = 该效果不生效）。
    /// </summary>
    public static int GetDepravedBloodlineLevelForFaction(Character? server, FactionData? faction)
    {
        if (server == null || faction == null || !server.IsDepraved)
            return 0;

        BloodlineData? data = BloodlineSystem.TryGetBloodline(server, out int level);
        if (data == null)
            return 0;   // 无血脉 → 该效果不生效

        return faction.Bloodlines.Contains(data.Id) ? level : 0;
    }

    /// <summary>
    /// 派系营业声望获取倍率（★ 各项「相乘」，不再相加）：
    ///   · 公主（Princess）职业      → ×<see cref="PrincessRevenueMultiplier"/>（单独存在时即 ×10）
    ///   · 血脉（堕落 + 所属派系一致）→ ×（1 + 0.5 × 等级）= +1 → ×1.5、+10 → ×6
    ///   两者同时存在：血脉 +10 + 公主 = 6 × 10 = **60 倍**（原为加算 16 倍）；
    ///   两者都没有 → 1 倍（原样）。
    /// </summary>
    public static float CombineFactionReputationMultiplier(bool isPrincess, int depravedBloodlineLevel)
    {
        float multiplier = 1f;
        if (isPrincess)
            multiplier *= PrincessRevenueMultiplier;

        if (depravedBloodlineLevel > 0)
            multiplier *= BloodlineSystem.GetFactionReputationSpeedMultiplier(depravedBloodlineLevel);

        return multiplier;
    }

    /// <summary>角色是否拥有“公主（Princess）”职业（派系营业额 / 派系声誉 ×10）。</summary>
    public static bool IsPrincessProfession(Character? character)
    {
        if (character == null)
            return false;
        if (!GameData.TryGetProfession(character, out IProfessionData? profession) || profession == null)
            return false;
        return string.Equals(profession.InternalName, "Princess", StringComparison.OrdinalIgnoreCase);
    }

    // ─── 访客初始化（Client.InitializeTrait）──────────────────────────────

    /// <summary>
    /// Prefix：在原版 Client.InitializeTrait() 之前把 DataType 改为派系 Id。
    /// 之后原版方法会按该 DataType 写入基础特质，并随机一个原版 EPreferenceType
    /// （保留原版偏好系统，不做任何屏蔽）。
    /// </summary>
    public static void AssignFactionToClient(Client instance)
    {
        if (instance == null || GameData.FactionDataList.Count == 0)
            return;

        FactionData? faction = RollUnlockedFaction();
        if (faction != null)
            instance.DataType = faction.Id;
    }

    /// <summary>
    /// 从「已解锁派系」中随机抽取一个（供访客与猎手营地俘虏共用）。
    /// 若没有任何派系解锁（理论上不会发生，Kingdom/Sect 始终解锁），退回全部派系列表；
    /// 列表为空时返回 null。
    /// </summary>
    public static FactionData? RollUnlockedFaction()
    {
        if (GameData.FactionDataList.Count == 0)
            return null;

        List<FactionData> unlocked = GameData.FactionDataList.Where(x => x.IsUnlocked()).ToList();
        if (unlocked.Count == 0)
            unlocked = GameData.FactionDataList.ToList();

        return unlocked[Random.Range(0, unlocked.Count)];
    }

    /// <summary>
    /// Postfix：在原版方法之后为访客写入“品阶 + 财富（支付能力）” Buff，以及所属派系的血脉。
    /// 注意：不覆盖 DisplayName —— 客人名字行保持原版访客类型名（如 #Client0Name）。
    /// </summary>
    public static void ApplyVisitorRankAndWealth(Client instance)
    {
        if (instance == null || !TryGetFaction(instance.DataType, out FactionData? faction) || faction == null)
            return;

        int tier = RollVisitorTier(GetEffectiveReputationTier(faction.Id));
        SetBuffInt(ConfigBuffs.FactionVisitorTier, tier, instance);

        // ★ 派系财富：与品阶、派系声誉严格对应（不再随机浮动，保证 UI 显示 = 实际支付额，
        //   也避免出现“同品阶客户金额忽高忽低”的跳跃感）
        int wealth = Mathf.Max(5, ComputeVisitorWealth(faction.Id, tier));
        SetBuffInt(ConfigBuffs.FactionVisitorWealth, wealth, instance);

        // ★ 派系血脉：从该派系允许的血脉中随机一种。
        //   等级只由「模组派系声誉等级」决定（Unknown 1-3 … Fabled 1-10），
        //   ★ 不参考玩家原版声望等级，也受访客品阶影响（品阶只影响人数/财富/声誉奖励）。
        ApplyVisitorBloodline(instance, faction, GetFactionReputationIndex(faction.Id));
    }

    // ─── 派系血脉（访客） ────────────────────────────────────────────────────

    /// <summary>
    /// 访客血脉等级上限（索引 = 模组派系声誉等级 <see cref="EFactionReputation"/>）：
    ///   Unknown 1-3 / Obscure 1-4 / Insignificant 1-5 / Noteworthy 1-6 /
    ///   Reputable 1-7 / Significant 1-8 / Famous 1-9 / Fabled 1-10。
    /// ★ 只与「模组派系声誉等级」相关：既不参考原版声望等级，也不受访客品阶影响。
    /// </summary>
    public static readonly int[] MaxVisitorBloodlineLevelByReputation = { 3, 4, 5, 6, 7, 8, 9, 10 };

    /// <summary>按派系声誉等级（0~7）取访客血脉等级上限。</summary>
    public static int GetMaxVisitorBloodlineLevel(int reputationIndex)
        => MaxVisitorBloodlineLevelByReputation[Mathf.Clamp(reputationIndex, 0, MaxReputationIndex)];

    /// <summary>掷访客血脉等级：在 [1, 上限] 内均匀取值（上限只由派系声誉等级决定）。</summary>
    public static int RollVisitorBloodlineLevel(int reputationIndex)
        => Random.Range(BloodlineSystem.MinLevel, GetMaxVisitorBloodlineLevel(reputationIndex) + 1);

    /// <summary>
    /// 为访客写入所属派系的血脉（随机取该派系 Bloodlines 中的一种；派系未配置血脉时不处理）。
    /// 等级只由该派系的「模组派系声誉等级」决定（见 <see cref="RollVisitorBloodlineLevel"/>）。
    /// </summary>
    public static void ApplyVisitorBloodline(Client? client, FactionData? faction, int reputationIndex)
    {
        if (client == null || faction == null || faction.Bloodlines.Count == 0)
            return;

        EBloodline bloodline = faction.Bloodlines[Random.Range(0, faction.Bloodlines.Count)];
        BloodlineSystem.SetBloodline(client, bloodline, RollVisitorBloodlineLevel(reputationIndex));
    }

    /// <summary>读取访客品阶（默认 1）。</summary>
    public static int GetVisitorTier(Client client)
        => Mathf.Clamp(GetBuffInt(ConfigBuffs.FactionVisitorTier, client), 1, 5);

    /// <summary>读取访客财富值（支付能力，无 Buff 时按品阶 1 重算）。</summary>
    public static int GetVisitorWealth(Client client)
    {
        int wealth = GetBuffInt(ConfigBuffs.FactionVisitorWealth, client);
        if (wealth > 0)
            return wealth;
        if (TryGetFaction(client.DataType, out FactionData? faction) && faction != null)
            return ComputeVisitorWealth(faction.Id, GetVisitorTier(client));
        return 10;
    }
}

