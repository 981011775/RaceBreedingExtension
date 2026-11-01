#nullable enable
using System.Collections.Generic;
using MBM.ModLoader.Settings;
using MBMScripts;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RaceBreedingExtension.HunterCamp;

/// <summary>
/// 猎手营地（个人领地地面营地）的“自动捕获奴隶”系统配置。
///
/// 旧版“房间升级(RoomUpgrades)获得猎手营地技能”的机制已按需求整体删除：
/// 现在营地的功能只建立在个人领地地面的「猎手营地 / 大型猎手营地」房间之上：
///   1) 营地里没有奴隶时，等待 SpawnDelaySeconds 秒后自动生成一名奴隶（消耗金币）；
///   2) 生成的奴隶 Training = 0；
///   3) 房里没有怪物 → EscapeWithoutMonsterSeconds 秒后逃跑；
///      有怪物 → 由原版 AI 触发性行为，累计 SexEscapeBaseCount 次后逃跑；
///   4) 逃跑会损失金币；进入乏力/堕落/死亡状态的奴隶不再逃跑、并可被拖动。
/// </summary>
internal static class HunterCampConfig
{
    private const string KEY_ENABLE = "HunterCamp_Enable";
    private const string KEY_SPAWN_COST_DIVISOR = "HunterCamp_SpawnCostPowerDivisor";
    private const string KEY_ESCAPE_PENALTY_MULT = "HunterCamp_EscapePenaltyPowerMultiplier";
    private const string KEY_SEX_ESCAPE_COUNT = "HunterCamp_SexEscapeCount";
    private const string KEY_SPAWN_DELAY = "HunterCamp_SpawnDelaySeconds";
    private const string KEY_ESCAPE_SECONDS = "HunterCamp_EscapeSeconds";
    private const string KEY_AUTO_PAIR = "HunterCamp_AutoPairWithMonster";
    private const string KEY_ENABLE_LOGGING = "HunterCamp_EnableLogging";

    /// <summary>主开关。</summary>
    public static bool Enable { get; private set; }

    /// <summary>每次自动生成奴隶消耗的金币 = 该奴隶战斗力 ÷ 除数（向下取整）。</summary>
    public static int SpawnCostPowerDivisor { get; private set; } = DefaultSpawnCostPowerDivisor;

    /// <summary>奴隶逃跑时损失的金币 = 该奴隶战斗力 × 倍率。</summary>
    public static float EscapePenaltyPowerMultiplier { get; private set; } = DefaultEscapePenaltyPowerMultiplier;

    /// <summary>触发多少次性行为后奴隶逃跑（基础值）。</summary>
    public static int SexEscapeBaseCount { get; private set; } = DefaultSexEscapeBaseCount;

    /// <summary>营地空置多久后自动生成奴隶（秒）。</summary>
    public static float SpawnDelaySeconds { get; private set; } = DefaultSpawnDelaySeconds;

    /// <summary>房内没有怪物时，奴隶逃跑所需时间（秒）。</summary>
    public static float EscapeWithoutMonsterSeconds { get; private set; } = DefaultEscapeWithoutMonsterSeconds;

    /// <summary>是否自动把奴隶移动到怪物所在座位（触发原版性行为 AI）。</summary>
    public static bool AutoPairWithMonster { get; private set; }

    public static bool EnableLogging { get; private set; }

    // ─── 默认值 ───

    /// <summary>自动生成奴隶的等待时间默认值（秒）。</summary>
    public const float DefaultSpawnDelaySeconds = 5f;

    /// <summary>房内无怪物时的逃跑时间默认值（秒）。</summary>
    public const float DefaultEscapeWithoutMonsterSeconds = 60f;

    /// <summary>生成奴隶金额除数默认值：生成金额 = 战斗力 ÷ 30（不为整数直接取整）。</summary>
    public const int DefaultSpawnCostPowerDivisor = 30;

    /// <summary>逃跑损失倍率默认值：逃跑损失 = 奴隶战斗力 × 1.0（即等于战斗力）。</summary>
    public const float DefaultEscapePenaltyPowerMultiplier = 1f;

    /// <summary>
    /// 生成该奴隶所需金币 = 战斗力 ÷ <see cref="SpawnCostPowerDivisor"/>（整数除法，直接取整）。
    /// </summary>
    public static int ComputeSpawnCost(Character? slave)
        => GetCombatPower(slave) / Clamp(SpawnCostPowerDivisor, 1, 1000, DefaultSpawnCostPowerDivisor);

    /// <summary>该奴隶逃跑时损失的金币 = 战斗力 × <see cref="EscapePenaltyPowerMultiplier"/>（四舍五入）。</summary>
    public static int ComputeEscapePenalty(Character? slave)
        => Mathf.Max(0, Mathf.RoundToInt(GetCombatPower(slave) * EscapePenaltyPowerMultiplier));

    private static int GetCombatPower(Character? slave)
        => slave == null ? 0 : RaceBreedingExtension.CombatPower.CombatPowerSystem.GetCombatPower(slave);

    /// <summary>逃跑所需性行为次数的默认值。</summary>
    public const int DefaultSexEscapeBaseCount = 5;

    /// <summary>
    /// 怪物特性对“逃跑所需性行为次数”的加成表（键 = 怪物身上的特性，值 = 次数加成）。
    /// 目前为空表（特性规则待定）；后续只需在此登记即可自动生效。
    /// </summary>
    public static readonly Dictionary<ETrait, int> SexEscapeTraitBonus = new Dictionary<ETrait, int>();

    // ─── 生成奴隶的种族/职业/属性随机（沿用原猎手营地规则） ───

    /// <summary>生成奴隶最大生命的随机区间（× 种族基础最大生命）。</summary>
    public const float HealthRollMin = 0.75f;
    public const float HealthRollMax = 1.35f;

    /// <summary>
    /// 生成奴隶的属性区间（Trait93~Trait100）：值 = (Worst, Best)
    ///   · Worst = 派系声望为 0 时的端值（Trait93 / 94 / 95 / 97 在该端为负面属性）
    ///   · Best  = 派系声望满值（99999）时的最优端
    /// 声望归一化 q = 派系声誉分数 / 99999，实际取值 = Lerp(Worst, Best, q ± <see cref="StatReputationJitter"/>)：
    ///   q = 0 → 贴近最差端（可能出现负面属性）；q = 1 → 落在最优端最后 10% 区间：
    ///     Trait93 → 约 89~99.99（该槽位区间 -9.99~99.99）
    ///     Trait94 → 约 -8900~-9999    Trait95 → 约 -8900~-9999
    ///     Trait96 → 约 9049~9999      Trait97 → 约 9000~9999      Trait98 → 约 899~999
    /// Trait99 / Trait100 不随声望变动（在各自完整区间内随机）。
    /// 最终属性仍由游戏按 ConfigData 的 Min/Max 上限做二次钳制。
    /// </summary>
    public static readonly Dictionary<ETrait, (float Worst, float Best)> ProducedStatTraitRanges =
        new Dictionary<ETrait, (float, float)>
    {
        { ETrait.Trait93,  (-9.99f,   99.99f) },   // 受孕率加成（越高越好；最优端由 9.99 上调至 99.99）
        { ETrait.Trait94,  (  999f,  -9999f) },    // 成长时间缩短（越低越好 → 最优端 -9999）
        { ETrait.Trait95,  (  999f,  -9999f) },    // 维护费降低（越低越好 → 最优端 -9999）
        { ETrait.Trait96,  (  500f,    9999f) },   // 额外生命（越高越好）
        { ETrait.Trait97,  (   -5f,    9999f) },   // 生育上限（越高越好）
        { ETrait.Trait98,  (    0f,     999f) },   // 多胎数（越高越好）
        { ETrait.Trait99,  (  -99f,      99f) },   // 交配时间（随机，不随声望变动）
        { ETrait.Trait100, (-0.5f,      0.5f) },   // 交配体力消耗（随机，不随声望变动）
    };

    /// <summary>不随派系声望变动的属性槽位（在各自完整区间内随机）。</summary>
    private static readonly HashSet<ETrait> ReputationIndependentStatTraits = new HashSet<ETrait>
    {
        ETrait.Trait99, ETrait.Trait100,
    };

    /// <summary>该属性是否随派系声望变动（Trait99 交配时间 / Trait100 交配体力消耗 → false）。</summary>
    public static bool IsReputationScaledStat(ETrait trait) => !ReputationIndependentStatTraits.Contains(trait);

    /// <summary>
    /// 种族品阶带来的「声望质量」加成（每高一阶 +0.03，最多 +0.12）：
    /// 只把起点略微抬高，不会让满声望时的取值超出 <see cref="ProducedStatTraitRanges"/> 的最优端。
    /// </summary>
    public static float GetStatTraitTierQualityBonus(int speciesTier)
        => System.Math.Max(0, speciesTier - 1) * 0.03f;

    // ─── 派系声望对生成奴隶属性的影响（关联派系与猎手营地） ───

    /// <summary>声望满级时，生成奴隶最大生命随机倍率的上限加成（0.5 = 最高 +50%）。</summary>
    public const float HealthReputationBoost = 0.5f;

    /// <summary>
    /// 声望向最优端靠拢时的随机抖动幅度（±0.10）：
    /// 满声望（q = 1）时取值落在最优端最后 10% 区间内，
    /// 即 Trait97 ≈ 9000~9999、Trait98 ≈ 899~999、Trait96 ≈ 9049~9999。
    /// </summary>
    public const float StatReputationJitter = 0.10f;

    /// <summary>
    /// 生成一条属性值：按「派系声望向最优端插值 + ±<see cref="StatReputationJitter"/> 抖动」取值。
    /// <paramref name="reputationNormalized"/> = 派系声誉分数 / 99999（0 = 最差端，1 = 最优端附近的窄区间）。
    /// Trait99 交配时间 / Trait100 交配体力消耗 不参与声望插值（完整区间随机）。
    /// </summary>
    public static float RollProducedStatValue(ETrait trait, float speciesTierQualityBonus, float reputationNormalized)
    {
        if (!ProducedStatTraitRanges.TryGetValue(trait, out (float Worst, float Best) range))
            return 0f;

        // 不随声望变动：在完整区间内随机（区间可能反向书写，这里统一成 [lo, hi]）
        if (!IsReputationScaledStat(trait))
            return Random.Range(Mathf.Min(range.Worst, range.Best), Mathf.Max(range.Worst, range.Best));

        float quality = Mathf.Clamp01(Mathf.Clamp01(reputationNormalized)
                                      + speciesTierQualityBonus
                                      + Random.Range(-StatReputationJitter, StatReputationJitter));
        return Mathf.Lerp(range.Worst, range.Best, quality);
    }

    /// <summary>
    /// 生成奴隶的血脉等级：按「派系声望向 10 级插值」（与属性同一套质量模型）。
    /// 声望 99999 时落在 [Lerp(1,10,0.9), 10] = 9.1~10 → 取整为 9~10 级（用户给定值）；
    /// 声望 0 时贴近 1~2 级。
    /// </summary>
    public static int RollProducedBloodlineLevel(float reputationNormalized, int maxLevel, int minLevel = 1)
    {
        int ceiling = System.Math.Max(minLevel, maxLevel);
        float quality = Mathf.Clamp01(Mathf.Clamp01(reputationNormalized)
                                      + Random.Range(-StatReputationJitter, StatReputationJitter));
        int level = Mathf.RoundToInt(Mathf.Lerp(minLevel, ceiling, quality));
        return Mathf.Clamp(level, minLevel, ceiling);
    }

    /// <summary>生成时跳过的人工物种（Golem 是魔法造物，不作为猎捕对象）。</summary>
    public static readonly string[] ExcludedProducedSpecies =
    {
        "Golem",
    };

    // ─── 注册与刷新 ───

    public static void Register()
    {
        RegisterBool(KEY_ENABLE, true, HclLoc.Config_Enable);
        RegisterInt(KEY_SPAWN_COST_DIVISOR, DefaultSpawnCostPowerDivisor, HclLoc.Config_SpawnCostDivisor);
        RegisterFloat(KEY_ESCAPE_PENALTY_MULT, DefaultEscapePenaltyPowerMultiplier, HclLoc.Config_EscapePenaltyMultiplier);
        RegisterInt(KEY_SEX_ESCAPE_COUNT, DefaultSexEscapeBaseCount, HclLoc.Config_SexEscapeCount);
        RegisterFloat(KEY_SPAWN_DELAY, DefaultSpawnDelaySeconds, HclLoc.Config_SpawnDelay);
        RegisterFloat(KEY_ESCAPE_SECONDS, DefaultEscapeWithoutMonsterSeconds, HclLoc.Config_EscapeSeconds);
        RegisterBool(KEY_AUTO_PAIR, true, HclLoc.Config_AutoPairWithMonster);
        RegisterBool(KEY_ENABLE_LOGGING, false, HclLoc.Config_EnableLogging);

        RefreshValues();
    }

    public static void OnLanguageChanged()
    {
        ModSettings.SetDescription(ModEntry.ModName, KEY_ENABLE, HclLoc.Config_Enable);
        ModSettings.SetDescription(ModEntry.ModName, KEY_SPAWN_COST_DIVISOR, HclLoc.Config_SpawnCostDivisor);
        ModSettings.SetDescription(ModEntry.ModName, KEY_ESCAPE_PENALTY_MULT, HclLoc.Config_EscapePenaltyMultiplier);
        ModSettings.SetDescription(ModEntry.ModName, KEY_SEX_ESCAPE_COUNT, HclLoc.Config_SexEscapeCount);
        ModSettings.SetDescription(ModEntry.ModName, KEY_SPAWN_DELAY, HclLoc.Config_SpawnDelay);
        ModSettings.SetDescription(ModEntry.ModName, KEY_ESCAPE_SECONDS, HclLoc.Config_EscapeSeconds);
        ModSettings.SetDescription(ModEntry.ModName, KEY_AUTO_PAIR, HclLoc.Config_AutoPairWithMonster);
        ModSettings.SetDescription(ModEntry.ModName, KEY_ENABLE_LOGGING, HclLoc.Config_EnableLogging);
    }

    private static void RegisterBool(string key, bool defaultValue, string desc)
    {
        ModSettings.RegisterBool(ModEntry.ModName, key, defaultValue, desc);
        ModSettings.OnChanged(ModEntry.ModName, key, _ => RefreshValues());
    }

    private static void RegisterInt(string key, int defaultValue, string desc)
    {
        ModSettings.RegisterInt(ModEntry.ModName, key, defaultValue, desc);
        ModSettings.OnChanged(ModEntry.ModName, key, _ => RefreshValues());
    }

    private static void RegisterFloat(string key, float defaultValue, string desc)
    {
        ModSettings.RegisterFloat(ModEntry.ModName, key, defaultValue, desc);
        ModSettings.OnChanged(ModEntry.ModName, key, _ => RefreshValues());
    }

    public static void RefreshValues()
    {
        Enable = ModSettings.GetBool(ModEntry.ModName, KEY_ENABLE);
        SpawnCostPowerDivisor = Clamp(ModSettings.GetInt(ModEntry.ModName, KEY_SPAWN_COST_DIVISOR), 1, 1000, DefaultSpawnCostPowerDivisor);
        EscapePenaltyPowerMultiplier = ClampFloat(ModSettings.GetFloat(ModEntry.ModName, KEY_ESCAPE_PENALTY_MULT), 0f, 100f, DefaultEscapePenaltyPowerMultiplier);
        SexEscapeBaseCount = Clamp(ModSettings.GetInt(ModEntry.ModName, KEY_SEX_ESCAPE_COUNT), 1, 100, DefaultSexEscapeBaseCount);
        SpawnDelaySeconds = ClampFloat(ModSettings.GetFloat(ModEntry.ModName, KEY_SPAWN_DELAY), 1f, 300f, DefaultSpawnDelaySeconds);
        EscapeWithoutMonsterSeconds = ClampFloat(ModSettings.GetFloat(ModEntry.ModName, KEY_ESCAPE_SECONDS), 5f, 3600f, DefaultEscapeWithoutMonsterSeconds);
        AutoPairWithMonster = ModSettings.GetBool(ModEntry.ModName, KEY_AUTO_PAIR);
        EnableLogging = ModSettings.GetBool(ModEntry.ModName, KEY_ENABLE_LOGGING);
    }

    private static int Clamp(int value, int min, int max, int fallback)
        => value < min || value > max ? fallback : value;

    private static float ClampFloat(float value, float min, float max, float fallback)
        => value < min || value > max ? fallback : value;

    // =========================================================================
    // 生成奴隶的「种族」随机：按供应派系的声望决定品阶概率
    // =========================================================================

    /// <summary>
    /// 传说（品阶 5）物种在**派系声望 0** 时的出现概率（%/每个物种）。
    /// 声望越高概率越高，声望 ≥ <see cref="SpeciesHighTierReputationFull"/> 时达到
    /// <see cref="SpeciesTier5BaseChance"/> × <see cref="SpeciesHighTierChanceMultiplier"/> = 20%。
    /// </summary>
    public const float SpeciesTier5BaseChance = 2f;

    /// <summary>
    /// 史诗（品阶 4）物种在**派系声望 0** 时的出现概率（%/每个物种）。
    /// 声望满级时 = 4% × <see cref="SpeciesHighTierChanceMultiplier"/> = 40%。
    /// </summary>
    public const float SpeciesTier4BaseChance = 4f;

    /// <summary>
    /// 高阶概率达到上限时的派系声望分数（= EFactionReputation.Fabled 的门槛）。
    /// 例：恶魔派系声望 0 → Devil 2%；声望 &gt; 29999（即 ≥ 本值）→ Devil 20%。
    /// </summary>
    public const int SpeciesHighTierReputationFull = 30000;

    /// <summary>声望满级时高阶种族概率的倍数（传说 2% → 20%、史诗 4% → 40%）。</summary>
    public const float SpeciesHighTierChanceMultiplier = 10f;

    /// <summary>
    /// 高阶（品阶 4/5）物种概率之和的上限（%）：超出时按同比例整体缩小，
    /// 保证「品阶 1~3 物种」与「无血脉种族」始终留有出现机会。
    /// </summary>
    public const float SpeciesHighTierTotalCap = 80f;

    /// <summary>
    /// 「无血脉种族」在**剩余概率**中的占比（%）：声望 0 时 = 50%（低声望时基础种族更常见），
    /// 声望满级时 = 10%（但不会归零，基础种族始终可能出现）。
    /// </summary>
    public const float NoBloodlineShareAtZero = 50f;
    public const float NoBloodlineShareAtFull = 10f;

    /// <summary>派系声望 → 高阶系数（0 = 声望 0，1 = 声望 ≥ <see cref="SpeciesHighTierReputationFull"/>）。</summary>
    public static float GetHighTierReputationFactor(int reputationScore)
        => Mathf.Clamp01(reputationScore / (float)SpeciesHighTierReputationFull);

    /// <summary>
    /// 单个「高阶物种」（品阶 5 / 品阶 4）的出现概率（%，未做总和上限缩放前）：
    ///   品阶 5：2%  → 20%（声望 0 → 满级）
    ///   品阶 4：4%  → 40%（声望 0 → 满级）
    /// 品阶 1~3 返回 0（它们走「剩余概率」）。
    /// </summary>
    public static float ComputeSpeciesTierChance(int speciesTier, int reputationScore)
    {
        float baseChance = speciesTier >= 5 ? SpeciesTier5BaseChance
            : speciesTier == 4 ? SpeciesTier4BaseChance
            : 0f;
        return baseChance * Mathf.Lerp(1f, SpeciesHighTierChanceMultiplier, GetHighTierReputationFactor(reputationScore));
    }

    /// <summary>「无血脉种族」在剩余概率中的占比（%）：声望越高占比越低（50% → 10%）。</summary>
    public static float ComputeNoBloodlineShare(int reputationScore)
        => Mathf.Lerp(NoBloodlineShareAtZero, NoBloodlineShareAtFull, GetHighTierReputationFactor(reputationScore));
}
