#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using ComplexBreedingRedux.Data;
using ComplexBreedingRedux.Data.Species;
using ComplexBreedingRedux.Data.SpeciesCore;
using MBMScripts;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ComplexBreedingRedux.Features;

internal static class PlayDataSystem
{
    private static readonly Dictionary<ERace, EUnitType> RaceToUnitType = new Dictionary<ERace, EUnitType>
    {
        { ERace.Human,     EUnitType.Human     },
        { ERace.Elf,       EUnitType.Elf       },
        { ERace.Dwarf,     EUnitType.Dwarf     },
        { ERace.Neko,      EUnitType.Neko      },
        { ERace.Inu,       EUnitType.Inu       },
        { ERace.Usagi,     EUnitType.Usagi     },
        { ERace.Hitsuji,   EUnitType.Hitsuji   },
        { ERace.Dragonian, EUnitType.Dragonian },
    };

    /// <summary>进化物种的胸部尺寸池：BaseMother → 核心物种，否则按种族取核心物种。</summary>
    public static bool TryGetBreastSizePool(ISpeciesData species, out int[] pool)
    {
        if (species.BaseMother != null)
        {
            ISpeciesCoreData? motherCore = GameData.SpeciesCoreDataList
                .FirstOrDefault(x => x.GetReplacedType == species.BaseMother);
            if (motherCore != null && motherCore.TitsTypes.Length > 0)
            {
                pool = motherCore.TitsTypes;
                return true;
            }
        }

        if (GameData.TryGetSpeciesCore(species.Race, out ISpeciesCoreData? raceCore)
            && raceCore != null
            && raceCore.TitsTypes.Length > 0)
        {
            pool = raceCore.TitsTypes;
            return true;
        }

        pool = Array.Empty<int>();
        return false;
    }

    /// <summary>固定物种尺寸或从池中加权选择。Extra (5) 仅在 allowExtraSize（市场）时允许。</summary>
    public static void ApplyBreastSize(Female female, ISpeciesData? species, ISpeciesCoreData? core, bool allowExtraSize, string source)
    {
        if (species != null)
        {
            if (species.TitsType.HasValue)
            {
                female.TitsType = species.TitsType.Value;
                return;
            }

            if (TryGetBreastSizePool(species, out int[] speciesPool))
            {
                ApplyWeightedBreastSize(female, speciesPool, allowExtraSize, $"{source}/species:{species.InternalName}");
                return;
            }
        }

        if (core != null)
        {
            ApplyWeightedBreastSize(female, core.TitsTypes, allowExtraSize, $"{source}/core:{core.InternalName}");
            return;
        }
    }

    /// <summary>根据核心 InternalName 为市场/新角色生成时使用的加权尺寸。</summary>
    public static void ApplyMarketSlaveBreastSize(Female female, string coreInternalName, ESector sector)
    {
        if (!GameData.TryGetSpeciesCoreByInternalName(coreInternalName, out ISpeciesCoreData? core) || core == null)
            return;

        bool allowExtra = sector == ESector.Market;
        ApplyWeightedBreastSize(
            female,
            core.TitsTypes,
            allowExtraSize: allowExtra,
            $"NewCharacter/{core.InternalName}/{sector}");
    }

    public static void ApplyWeightedBreastSize(Female female, int[] speciesPool, bool allowExtraSize, string source)
    {
        int[] pool = ExpandBreastSizePool(speciesPool, allowExtraSize, source);

        if (pool.Length == 0)
            return;

        if (pool.Length == 1)
        {
            female.TitsType = pool[0];
            return;
        }

        int total = 0;
        foreach (int titsType in pool)
            total += GetWeight(titsType);

        int roll = Random.Range(0, total);
        int cumulative = 0;
        foreach (int titsType in pool)
        {
            cumulative += GetWeight(titsType);
            if (roll < cumulative)
            {
                female.TitsType = titsType;
                return;
            }
        }

        female.TitsType = pool[pool.Length - 1];
    }

    /// <summary>Extra (5) 仅限市场且有 TitsMod 时启用。</summary>
    private static int[] ExpandBreastSizePool(int[] speciesPool, bool allowExtraSize, string source)
    {
        if (!allowExtraSize || !ModCompatibility.IsTitsModLoaded() || speciesPool.Length == 0)
            return speciesPool;

        if (speciesPool.Max() < 4 || speciesPool.Contains(5))
            return speciesPool;

        return speciesPool.Append(5).ToArray();
    }


    private static int GetWeight(int titsType)
        => GameBalance.MarketBreastSizeWeights.TryGetValue(titsType, out int weight)
            ? weight
            : GameBalance.DefaultMarketBreastSizeWeight;

    /// <summary>出生后 ±1 抖动 — 不属于加权选择的一部分。</summary>
    public static void LogBirthBreastJitter(Female female, int before, int delta)
    {
        // 日志记录已禁用
    }

    public static void ApplyNewCharacterRace(PlayData __instance, ref EUnitType unitType)
    {
        string speciesName = ModConfig.StartingSlaveSpecies;
        if (__instance.Days == 1
            && __instance.GetUnitList(ESector.Female).Count == 0
            && unitType == EUnitType.Human
            && !string.IsNullOrWhiteSpace(speciesName))
        {
            if (GameData.TryGetSpeciesCoreByInternalName(speciesName, out ISpeciesCoreData? coreData)
                && coreData != null
                && RaceToUnitType.TryGetValue(coreData.Race, out EUnitType mapped))
                unitType = mapped;
        }
    }

    public static void ApplyNielStartCustomize(PlayData __instance)
    {
        Character? character = __instance.GetCustomizeUnit() as Character;
        if (character == null) return;

        character.ClearTrait();
    }

    public static void ApplyOpenReport(PlayData __instance)
    {
        var items = GameManager.Instance.PlayerData
            .GetUnitList(ESector.ReportItem).OfType<Item>().ToList();
        if (items.All(x => x.ItemType != EItemType.Item_TraitUpgradePill) && items.Count <= 2)
        {
            __instance.NewItem(EItemType.Item_TraitUpgradePill, ESector.ReportItem,
                new System.ValueTuple<int, int>(0, 0), -1, 1, true);
        }
    }
}