#nullable enable
using System.Linq;
using ComplexBreedingRedux.Data;
using ComplexBreedingRedux.Data.Species;
using ComplexBreedingRedux.Data.SpeciesCore;
using ComplexBreedingRedux.Data.Traits;
using ComplexBreedingRedux.Properties;
using HarmonyLib;
using MBMScripts;

namespace ComplexBreedingRedux.Features.SlaveMarket;

internal enum EAutoSellModeOption
{
    SellAll,
    DoNotSellAtLeastEqualOffspring,
    DoNotSellBetterOffspring,
    DoNotSellAnyone,
}

internal enum EAutoSellEvoFilter
{
    NoFilter,
    DoNotSellEvolutions,
    Custom,
}

[HarmonyPatch(typeof(Unit), "SoldSoon", MethodType.Getter)]
internal class SoldSoonPatch
{
    public static string[] GetLocalizedOptions() => new[]
    {
        Strings.AutoSell_SellAll,               // 0 = EAutoSellModeOption.SellAll
        Strings.AutoSell_DoNotSellAtLeastEqual, // 1 = EAutoSellModeOption.DoNotSellAtLeastEqualOffspring
        Strings.AutoSell_DoNotSellBetter,       // 2 = EAutoSellModeOption.DoNotSellBetterOffspring
        Strings.AutoSell_DoNotSellAnyone,       // 3 = EAutoSellModeOption.DoNotSellAnyone
    };

    public static string[] GetLocalizedEvoFilterOptions() => new[]
    {
        Strings.AutoSell_EvoNoFilter,   // 0 = EAutoSellEvoFilter.NoFilter
        Strings.AutoSell_EvoDoNotSell,  // 1 = EAutoSellEvoFilter.DoNotSellEvolutions
        Strings.AutoSell_EvoCustom,     // 2 = EAutoSellEvoFilter.Custom
    };

    public static void Postfix(Unit __instance, ref bool __result)
    {
        if (!__result)
            return;

        // ★ 出售标记（SoldSoon）**只由游戏原版在出生时写入**（源码 22121-22131）；本 mod 从不写标记，
        //   只在读取层做减法（下面这段就是全部逻辑）。
        // ★ 恶性 bug 修复：以下单位绝不能被妖精搬走出售（读取层面直接返回 false，★ 不修改字段本身，
        //   因此「动作中 / 婴儿期」只是「延后」而不是「取消」）：
        //     · 正在动作（性行为 / 射精 / 生产 / 移动 / 挤奶…）—— 原版妖精流程只要求 StateTime ≥ 3 秒、
        //       不判断动作，会在动作中途被搬走卖掉；
        //     · 仍在婴儿期（IsBaby）—— 原版会把 SoldSoon 的婴儿从产房 / 观赏房直接送 Depot 卖掉；
        //       这里改成「让妖精先把婴儿搬进育婴笼养大，**成长完成后**才卖」；
        //     · 猎手营地俘虏 —— 永不自动出售；
        //     · 不是「在本设施出生长大」的单位（无父母记录：买来的奴隶 / 搬进来的成体）—— 永不自动出售。
        //   ⚠ **不再按房间做保护**：奴笼 / 怪物笼 / 产房 / 观赏房都只是妖精的搬运目的地
        //     （游戏默认把雌性安置到 SlaveCage、雄性安置到 MonsterCage / MonsterCage2、婴儿安置到
        //     母亲所在的产房），把这些房间列为保护区就等于「所有子代都卖不掉」（上一版就是这样炸的）。
        if (PixySoldSoonRefresh.IsProtectedFromPixySale(__instance as Character))
        {
            __result = false;
            return;
        }

        // ★ 子代战斗力过滤出售（模组菜单第一行开关，默认关闭，需玩家自行开启）：
        //   · 女性子代的战斗力 > 母系奴隶 → 妖精不出售；
        //   · 怪物（雄性）子代的战斗力 > 父系怪物 → 妖精不出售。
        //   与上面的保护层一样只做读取层减法，不改写任何存档字段。
        if (OffspringPowerSellFilter.ShouldProtectFromSale(__instance as Character))
        {
            __result = false;
            return;
        }

        if (__instance is Female female)
            __result = ShouldSellFemale(female);
        else if (__instance is Male male)
            __result = ShouldSellMale(male);
    }

    /// <summary>确定后代是否可以出售，考虑自动出售过滤器。</summary>
    internal static bool ShouldSellFemale(Female female)
    {
        EAutoSellModeOption statMode = ModConfig.AutoSellFemaleModeEnum;
        if (statMode == EAutoSellModeOption.DoNotSellAnyone)
            return false;

        EAutoSellEvoFilter evoFilter = ModConfig.AutoSellFemaleEvoFilterEnum;
        bool sellDegradation = ModConfig.AutoSellDegradation;

        bool anyFilterActive = statMode != EAutoSellModeOption.SellAll
                            || evoFilter != EAutoSellEvoFilter.NoFilter
                            || !sellDegradation;
        if (!anyFilterActive) return true;

        var mother = GameManager.Instance.PlayerData.GetUnit(female.MotherUnitId) as Character;
        if (mother == null)
            return false;

        GameData.TryGetSpecies(female, out ISpeciesData? offSpecies);
        GameData.TryGetSpecies(mother, out ISpeciesData? parSpecies);

        if (!sellDegradation && IsDegradation(offSpecies, parSpecies))
            return false;

        if (evoFilter != EAutoSellEvoFilter.NoFilter && IsEvolution(offSpecies, parSpecies, mother))
        {
            bool protect = evoFilter == EAutoSellEvoFilter.DoNotSellEvolutions
                        || (evoFilter == EAutoSellEvoFilter.Custom
                            && offSpecies != null
                            && ModConfig.IsSpeciesEvoProtected(offSpecies.InternalName));
            if (protect)
                return false;
        }

        if (statMode != EAutoSellModeOption.SellAll && IsCorrect(female, mother))
            return false;

        return true;
    }

    /// <summary>确定怪物是否可以出售，考虑自动出售过滤器。</summary>
    internal static bool ShouldSellMale(Male male)
    {
        EAutoSellModeOption mode = ModConfig.AutoSellMaleModeEnum;
        if (mode == EAutoSellModeOption.DoNotSellAnyone)
            return false;
        if (mode == EAutoSellModeOption.SellAll) return true;

        var father = GameManager.Instance.PlayerData.GetUnit(male.FatherUnitId) as Character;
        if (father == null || IsCorrect(male, father))
            return false;

        return true;
    }

    /// <summary>确定后代是否为父母的进化形态。</summary>
    internal static bool IsEvolution(ISpeciesData? offspringSpecies, ISpeciesData? parentSpecies, Character parent)
    {
        if (offspringSpecies == null) return false;

        if (parentSpecies != null)
            return offspringSpecies.PossiblyMothers != null
                && offspringSpecies.PossiblyMothers.Contains(parentSpecies.InternalName);

        if (offspringSpecies.BaseMother == null) return false;
        return GameData.TryGetSpeciesCore(parent, out ISpeciesCoreData? core)
            && core != null
            && offspringSpecies.BaseMother == core.GetReplacedType;
    }

    /// <summary>确定后代是否为父母的退化形态。</summary>
    internal static bool IsDegradation(ISpeciesData? offspringSpecies, ISpeciesData? parentSpecies)
    {
        if (parentSpecies == null) return false;
        if (offspringSpecies == null) return true;
        if (offspringSpecies.InternalName == parentSpecies.InternalName) return false;
        return offspringSpecies.Tier < parentSpecies.Tier;
    }

    private static bool IsCorrect(Character offspring, Character parent)
    {
        EAutoSellModeOption mode = offspring is Female
            ? ModConfig.AutoSellFemaleModeEnum
            : ModConfig.AutoSellMaleModeEnum;

        if (mode == EAutoSellModeOption.SellAll || mode == EAutoSellModeOption.DoNotSellAnyone)
            return false;

        int offspringTier = 1;
        int parentTier = 1;

        if (GameData.TryGetSpecies(offspring, out ISpeciesData? offSpecies) && offSpecies != null)
            offspringTier = offSpecies.Tier;
        if (GameData.TryGetSpecies(parent, out ISpeciesData? parSpecies) && parSpecies != null)
            parentTier = parSpecies.Tier;

        if (parentTier > offspringTier) return false;
        if (parentTier < offspringTier) return true;

        // 精华比较已删除，仅比较普通特质（正面/负面）
        bool better = false;

        // 比较正面特质
        foreach (ITraitData trait in GameData.TraitsDataList.Where(x => x.IsPositive))
        {
            if (!offspring.TraitContains(trait.ReplacedTrait) && parent.TraitContains(trait.ReplacedTrait)) return false;
            if (offspring.TraitContains(trait.ReplacedTrait) && !parent.TraitContains(trait.ReplacedTrait)) better = true;
        }

        // 比较负面特质
        foreach (ITraitData trait in GameData.TraitsDataList.Where(x => !x.IsPositive))
        {
            if (offspring.TraitContains(trait.ReplacedTrait) && !parent.TraitContains(trait.ReplacedTrait)) return false;
            if (!offspring.TraitContains(trait.ReplacedTrait) && parent.TraitContains(trait.ReplacedTrait)) better = true;
        }

        return (mode == EAutoSellModeOption.DoNotSellBetterOffspring && better)
            || mode == EAutoSellModeOption.DoNotSellAtLeastEqualOffspring;
    }
}