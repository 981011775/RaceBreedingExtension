#nullable enable
using System.Collections.Generic;
using MBMScripts;

namespace ComplexBreedingRedux.Data;

/// <summary>平衡常数：品阶、胸部、最小/最大值。</summary>
internal static class GameBalance
{
    // --- 品阶 ---
    public static readonly Dictionary<int, string> TierNames = new Dictionary<int, string>
    {
        { 1, "Common"    },
        { 2, "Uncommon"  },
        { 3, "Rare"      },
        { 4, "Epic"      },
        { 5, "Legendary" },
    };

    /// <summary>
    /// 繁殖时后代「降级」的概率（%）。降级 = 非「同族」的结果，依次可能是
    /// 「更低（或同阶）品阶的进化物种」或「基础种族（BaseMother）」。
    /// ★ 品阶 1 也必须有降级几率：否则链条会在品阶 1 物种处彻底断开，
    ///   基础种族永远生不出来（例：Goblin（品阶 1，BaseMother=Human）
    ///   需要这 30% 的降级判定才有机会生出人类）。
    /// </summary>
    public static readonly Dictionary<int, int> TierLowerEvoBirths = new Dictionary<int, int>
    {
        { 1, 30 },
        { 2, 30 },
        { 3, 40 },
        { 4, 50 },
        { 5, 60 },
    };

    /// <summary>
    /// 职业遗传几率（0~1）：75%（原先为 100%）。
    /// 作用于 <c>BreedingSystem.SetTraitsAndProfession</c>：雌性后代继承母亲职业、
    /// 怪物（雄性）后代继承父母双方「怪物可遗传」职业时统一使用该几率。
    /// </summary>
    public const float ProfessionInheritChance = 0.75f;

    /// <summary>
    /// 奴隶 × 怪物（非人类父本）时，后代为「同族女性 / 降级物种女性（含基础种族）」的几率（%）。
    /// 其余概率：先由进化判定产出「进化物种女胎」，仍未命中时产出「怪物男胎」——
    /// 即 奴隶×怪物 = 进化物种女胎（可能） + 同族/降级女胎（10%） + 怪物男胎（剩余大部分）。
    /// </summary>
    public const int MonsterSameOrLowerFemaleChance = 10;

    // --- 胸部（仅用于外观，不涉及产奶） ---
    public static readonly Dictionary<int, string> BreastsSizes = new Dictionary<int, string>
    {
        { 0, "Flat"    },
        { 1, "Small"   },
        { 2, "Average" },
        { 3, "Big"     },
        { 4, "Huge"    },
        { 5, "Extra"   },
    };

    /// <summary>市场随机选择胸部尺寸时的相对权重（键 = TitsType 原始值）。</summary>
    public static readonly Dictionary<int, int> MarketBreastSizeWeights = new Dictionary<int, int>
    {
        { 0, 100 },
        { 1, 100 },
        { 2,  40 },
        { 3,  10 },
        { 4,   4 },
        { 5,   1 },
    };

    public const int DefaultMarketBreastSizeWeight = 100;

    // --- 游戏常量 ---
    public const ETrait ProfessionTraitId = (ETrait)10;   // 职业特质ID（保留）
    // public const ETrait PersonalityTraitId = (ETrait)9; // 性格已删除，已注释
}