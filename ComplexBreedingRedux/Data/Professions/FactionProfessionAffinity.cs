#nullable enable
using System.Collections.Generic;
using ComplexBreedingRedux.Data.Species;
using ComplexBreedingRedux.Features;
using MBMScripts;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ComplexBreedingRedux.Data.Professions;

/// <summary>
/// 派系 ↔ 职业 关联表与「声望加权」职业抽取（猎手营地生成奴隶、访客等生成流程使用）。
///
/// 需求对应关系：
/// 1) 关联派系与猎手营地：每个生成单位先随机一个「供应派系」（已解锁派系中随机），
///    该派系的声望决定本次生成时各职业的出现权重；
/// 2) 各派系声望越高 → 该派系列表中的职业出现几率越高（<see cref="FactionAffinityBonus"/>）；
/// 3) 基础权值按品阶：传说(Legendary) 0.5 / 史诗(Epic) 1 / 稀有(Rare) 2 / 不凡(Uncommon) 12 / 普通(Common) 30
///    （百分比刻度，实际概率 = 权值 ÷ 权值总和）；
/// 4) 声望为 0 时所有职业概率都不为 0（<see cref="MinWeight"/> 兜底 + 所有品阶基础权值 &gt; 0）；
/// 5) 随着声望提升，初始低概率职业的权值向「全体平均权值」靠拢（<see cref="RarityFlatten"/>），
///    即「低概率职业会增长到平均出现」；
/// 6) 派系等级越高，稀有及以上职业占比越高（<see cref="RareReputationBonus"/>）。
///
/// 无派系归属、任何时候都可能出现的职业见 <see cref="UnaffiliatedProfessionIds"/>
/// （Maid / Farmhand / Blacksmith / Warrior / Hunter）。
/// </summary>
internal static class FactionProfessionAffinity
{
    // ============================ 派系 → 关联职业 ============================

    /// <summary>
    /// 键 = 派系 Id（见 <see cref="ComplexBreedingRedux.Data.Factions.FactionRegistry"/>），
    /// 值 = 该派系关联的职业 Id。
    /// </summary>
    private static readonly Dictionary<int, int[]> AffiliatedProfessions = new Dictionary<int, int[]>
    {
        // 王国派系 Kingdom：骑士 / 工匠 / 商人 / 公主
        {
            0, new[]
            {
                ProfessionRegistry.KnightId, ProfessionRegistry.ArtificerId,
                ProfessionRegistry.MerchantId, ProfessionRegistry.PrincessId,
            }
        },
        // 宗教教团 Sect：牧师 / 圣骑士 / 勇者
        {
            1, new[]
            {
                ProfessionRegistry.PriestId, ProfessionRegistry.PaladinId, ProfessionRegistry.HeroId,
            }
        },
        // 恶魔派系 Demonic：邪教徒 / 刺客 / 黑暗祭司 / 调教师
        {
            2, new[]
            {
                ProfessionRegistry.CultistId, ProfessionRegistry.AssassinId,
                ProfessionRegistry.DarkPriestId, ProfessionRegistry.TrainerId,
            }
        },
        // 奥秘议会 Conclave：法师 / 药剂师 / 女巫 / 炼金术师
        {
            3, new[]
            {
                ProfessionRegistry.WizardId, ProfessionRegistry.ApothecaryId,
                ProfessionRegistry.WitchId, ProfessionRegistry.AlchemistId,
            }
        },
        // 怪物派系 Monster：强盗 / 盗贼 / 掠夺者 / 劫掠者
        {
            4, new[]
            {
                ProfessionRegistry.BrigandId, ProfessionRegistry.ThiefId,
                ProfessionRegistry.MarauderId, ProfessionRegistry.PlundererId,
            }
        },
    };

    /// <summary>无派系归属、任何派系声望下都可能出现的职业：女仆 / 农夫 / 铁匠 / 战士 / 猎人。</summary>
    public static readonly int[] UnaffiliatedProfessionIds =
    {
        ProfessionRegistry.MaidId, ProfessionRegistry.FarmhandId, ProfessionRegistry.BlacksmithId,
        ProfessionRegistry.WarriorId, ProfessionRegistry.HunterId,
    };

    // ============================ 权值参数 ============================

    /// <summary>各品阶的基础权值（索引 = 职业品阶）：普通30 / 不凡12 / 稀有2 / 史诗1 / 传说0.5。</summary>
    public static readonly float[] TierBaseWeight = { 0f, 30f, 12f, 2f, 1f, 0.5f };

    /// <summary>任何职业的最小权值：保证「即使声望为 0，所有职业出现概率也不为 0」。</summary>
    public const float MinWeight = 0.05f;

    /// <summary>声望满级时，基础权值向「池内平均权值」靠拢的比例（0.75 = 靠拢 75%）。</summary>
    public const float RarityFlatten = 0.75f;

    /// <summary>声望满级时，派系关联职业的权值倍率加成（8 → 最高 ×9）。</summary>
    public const float FactionAffinityBonus = 8f;

    /// <summary>声望满级时，稀有及以上职业的权值倍率加成（2 → 最高 ×3），使高品阶占比随声望提高。</summary>
    public const float RareReputationBonus = 2f;

    /// <summary>「稀有及以上」的品阶门槛（3 = 稀有 / 4 = 史诗 / 5 = 传说）。</summary>
    public const int RareTierThreshold = 3;

    // ============================ 查询辅助 ============================

    /// <summary>该职业是否与指定派系关联（factionId = -1 表示无派系 → 永远 false）。</summary>
    public static bool IsAffiliated(int factionId, int professionId)
    {
        if (factionId < 0)
            return false;
        if (!AffiliatedProfessions.TryGetValue(factionId, out int[]? ids) || ids == null)
            return false;
        for (int i = 0; i < ids.Length; i++)
        {
            if (ids[i] == professionId)
                return true;
        }
        return false;
    }

    /// <summary>该职业是否属于「无派系归属」职业（任何声望下都会出现）。</summary>
    public static bool IsUnaffiliated(int professionId)
    {
        for (int i = 0; i < UnaffiliatedProfessionIds.Length; i++)
        {
            if (UnaffiliatedProfessionIds[i] == professionId)
                return true;
        }
        return false;
    }

    /// <summary>派系综合声望的归一化值（0 = 无声望，1 = 最高声望等级）。</summary>
    public static float GetReputationNormalized(int factionId)
    {
        if (factionId < 0)
            return 0f;
        return Mathf.Clamp01(FactionSystem.GetEffectiveReputationIndex(factionId)
                             / (float)FactionSystem.MaxReputationIndex);
    }

    /// <summary>池内各职业「基础权值」的平均值（低概率职业向其靠拢的目标值）。</summary>
    public static float GetAverageBaseWeight(List<IProfessionData> pool)
    {
        if (pool == null || pool.Count == 0)
            return TierBaseWeight[TierBaseWeight.Length - 1];

        float total = 0f;
        for (int i = 0; i < pool.Count; i++)
            total += GetBaseWeight(pool[i]);
        return total / pool.Count;
    }

    private static float GetBaseWeight(IProfessionData profession)
    {
        int tier = Mathf.Clamp(profession.Tier, 1, TierBaseWeight.Length - 1);
        return TierBaseWeight[tier];
    }

    /// <summary>
    /// 单个职业的抽取权值：
    ///   基础品阶权值 → 按声望向池内平均权值靠拢 → 稀有及以上额外加成 → 派系关联职业额外加成 → 最小权值兜底。
    /// </summary>
    public static float ComputeWeight(IProfessionData profession, int factionId, float reputationNormalized,
        float averageBaseWeight)
    {
        float rep = Mathf.Clamp01(reputationNormalized);
        int tier = Mathf.Clamp(profession.Tier, 1, TierBaseWeight.Length - 1);

        float weight = TierBaseWeight[tier];

        // 声望越高，低概率职业越向「平均出现」靠拢
        weight = Mathf.Lerp(weight, averageBaseWeight, RarityFlatten * rep);

        // 声望越高，稀有及以上职业占比越高
        if (tier >= RareTierThreshold)
            weight *= 1f + RareReputationBonus * rep;

        // 声望越高，该派系关联职业出现几率越高
        if (IsAffiliated(factionId, profession.Id))
            weight *= 1f + FactionAffinityBonus * rep;

        return Mathf.Max(MinWeight, weight);
    }

    // ============================ 抽取 ============================

    /// <summary>
    /// 按「派系声望加权」抽取一个职业：
    /// 先按种族禁用职业过滤，再按 <see cref="ComputeWeight"/> 加权随机；池为空时返回 null。
    /// <paramref name="baseRace"/> 供「无物种的基础种族奴隶」使用：
    /// 此时 <paramref name="species"/> 为 null，用核心种族的 ERace 参与禁用判定
    /// （例如 Neko 核心 = Imp，同样不能担任禁用 Neko 的职业）。
    /// </summary>
    public static IProfessionData? Roll(ISpeciesData? species, int factionId,
        IReadOnlyList<IProfessionData>? professionPool, ERace? baseRace = null)
    {
        if (professionPool == null || professionPool.Count == 0)
            return null;

        ERace? effectiveRace = species?.Race ?? baseRace;
        List<IProfessionData> candidates = new List<IProfessionData>(professionPool.Count);
        for (int i = 0; i < professionPool.Count; i++)
        {
            IProfessionData profession = professionPool[i];
            if (effectiveRace.HasValue
                && profession.ForbiddenStartingSpecies != null
                && profession.ForbiddenStartingSpecies.Contains(effectiveRace.Value))
                continue;
            candidates.Add(profession);
        }
        if (candidates.Count == 0)
            return null;

        float rep = factionId >= 0 ? GetReputationNormalized(factionId) : 0f;
        float average = GetAverageBaseWeight(candidates);
        return WeightedPick(candidates, p => ComputeWeight(p, factionId, rep, average));
    }

    /// <summary>加权随机（权重 ≤ 0 的条目按 0 处理；总和为 0 时退回均匀随机）。</summary>
    public static T WeightedPick<T>(List<T> source, System.Func<T, float> weight)
    {
        float total = 0f;
        for (int i = 0; i < source.Count; i++)
            total += Mathf.Max(0f, weight(source[i]));

        if (total <= 0f)
            return source[Random.Range(0, source.Count)];

        float roll = Random.Range(0f, total);
        float cumulative = 0f;
        for (int i = 0; i < source.Count; i++)
        {
            cumulative += Mathf.Max(0f, weight(source[i]));
            if (roll < cumulative)
                return source[i];
        }
        return source[source.Count - 1];
    }
}
