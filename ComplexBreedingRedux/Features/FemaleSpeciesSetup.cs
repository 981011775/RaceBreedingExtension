#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using ComplexBreedingRedux.Data;
using ComplexBreedingRedux.Data.Species;
using ComplexBreedingRedux.Data.SpeciesCore;
using HarmonyLib;
using MBMScripts;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ComplexBreedingRedux.Features;

internal static class FemaleSpeciesSetup
{
    public static void SetSpecies(Female female, ISpeciesData species)
    {
        female.OnEnableTrait();
        female.ClearRaceTrait();
        female.ClearTrait();
        female.Figure = species.Figure;
        female.EyeType = Random.Range(1, 5);
        female.SkinColor = SkinColorSystem.PickRandom(species.SkinColors);
        female.ScaleColor = species.ScaleColors != null
            ? SkinColorSystem.PickRandom(species.ScaleColors)
            : Color.white;
        if (species.HairColors is { Length: > 0 })
            female.HairColor = SkinColorSystem.PickRandom(species.HairColors);
        female.EyeColor = species.EyeColor ?? female.EyeColor;
        female.HornColor = species.HornColor ?? female.HornColor;
        PlayDataSystem.ApplyBreastSize(female, species, core: null, allowExtraSize: false, source: "SetSpecies");
        if (species.HairColorSameAsSkin)
            female.HairColor = female.SkinColor;
        if (species.GuaranteedTraits != null)
        {
            foreach (ETrait trait in species.GuaranteedTraits)
            {
                if (!female.TraitContains(trait))
                    female.AddRaceTrait(trait);
            }
        }
        SetSpeciesAllStats(female, species);
        GameData.SetSpeciesIdentity(female, species.InternalName);
        // ★ 独特服色初始值（天使=金 / 堕天使=深紫 等；仅在该角色还没有服色时写入）
        RaceBreedingExtension.UniqueLookColor.UniqueLookColorSystem.ApplySpeciesClothesColor(female);
        // ★ 血脉（Bloodline）：按物种补发（SetSpecies 开头已 ClearTrait，需在此重新写入）
        RaceBreedingExtension.Bloodline.BloodlineSystem.ApplySpeciesBloodline(female);
        female.SortTrait();
    }

    /// <summary>
    /// 成人化专用（喂母乳 / 切换体型 → <c>Character.ChangeToAdult</c>）：
    /// 只重新套用物种的体型/骨架与数值，**绝不清空任何特质**。
    ///
    /// 与 <see cref="SetSpecies"/> 的区别（重要）：
    ///   SetSpecies 开头会执行 OnEnableTrait → ClearRaceTrait → ClearTrait，
    ///   那是「新建角色/替换物种」时才需要的清空；若在成人化时调用，
    ///   会把职业、血脉、Trait93~Trait100、一般特质等全部清掉
    ///   （虎人 Weretiger / 雪狼人 ArcticWerewolf 等带 RigPrefix 的物种必然触发）。
    /// 因此成人化改用本方法：保留肤色/发色/瞳色/胸围等既有外观，
    /// 只重设 Figure（成人骨架）、身体部件与物种数值，并补齐缺失的物种特质与物种血脉。
    /// </summary>
    public static void RefreshSpeciesAppearance(Female female, ISpeciesData species)
    {
        // 1) 外观：成人 Figure + 身体部件（其余颜色/胸围保持原样，不再重新随机）
        female.Figure = species.Figure;

        // 2) 物种独立特质：只补齐缺失的，绝不删除已有
        if (species.GuaranteedTraits != null)
        {
            foreach (ETrait trait in species.GuaranteedTraits)
            {
                if (!female.TraitContains(trait))
                    female.AddRaceTrait(trait);
            }
        }

        // 3) 物种数值（Race / 身体部件类型 / 生命 / 成长 / 维护等）
        SetSpeciesAllStats(female, species);
        GameData.SetSpeciesIdentity(female, species.InternalName);
        // ★ 独特服色初始值（若该角色已自定义服色则保持不变）
        RaceBreedingExtension.UniqueLookColor.UniqueLookColorSystem.ApplySpeciesClothesColor(female);

        // 4) 物种血脉：已有血脉时不覆盖（ApplySpeciesBloodline 内部判定）
        RaceBreedingExtension.Bloodline.BloodlineSystem.ApplySpeciesBloodline(female);

        female.SortTrait();
    }

    public static void SetSpeciesAllStats(Female female, ISpeciesStats stats)
    {
        female.SkinColorType = 0;
        female.Race = stats.Race;
        female.Figure = stats.Figure;
        female.EyeBallType = stats.EyeBallType;
        female.EarType = stats.EarType;
        female.EarHairType = stats.EarHairType;
        female.HornType = stats.HornType;
        female.HornDragonType = stats.HornDragonType;
        female.TailType = stats.TailType;
        female.TailDragonType = stats.TailDragonType;
        female.HairBodyType = stats.HairBodyType;
        female.UpperBodyType = stats.UpperBody;
        female.LowerBodyType = stats.LowerBody;
        SetSpeciesGameplayStats(female, stats);
    }

    public static void SetSpeciesGameplayStats(Female female, ISpeciesStats stats)
    {
        female.DecreasingMentalityOnHunger = stats.MilkQuality;
        female.ConceptionRate = (float)stats.ConceptionRate / 100f;
        female.MaxHealth = (float)stats.MaxHealth;
        female.GrowthTime = (float)stats.GrowthTime;
        female.MaxBirthCount = stats.MaxBirthCount;
        female.Maintenance = stats.Maintenance;
        female.DecreasingMentalityOnPregnant = stats.DecreasingMentalityOnPregnant;
        female.Health = female.MaxHealth;
    }

    internal static void HairColorMigration(Character character, Color? color, Color[]? baseColors, float? power)
    {
        if (power == null || !(power.GetValueOrDefault() > 0f))
            return;
        if (color != null)
        {
            character.HairColor = Color.Lerp(character.HairColor, color.Value, power.Value);
            return;
        }
        if (baseColors != null && baseColors.Length > 0)
            character.HairColor = Color.Lerp(character.HairColor, baseColors[0], power.Value);
    }
}

[HarmonyPatch(typeof(Female), "Initialize", new Type[] { })]
internal static class FemaleInitializePatch
{
    public static void Postfix(Female __instance)
    {
        // ==== 方案A：跳过对基础核心种族的修改 ====
        // 如果角色是基础核心种族（TryGetSpeciesCore 成功）且没有进化身份（SpeciesIdentity 为空），
        // 则直接返回，保留原版外观生成逻辑。
        if (GameData.TryGetSpeciesCore(__instance, out ISpeciesCoreData? core) && core != null)
        {
            string? identity = GameData.GetSpeciesIdentity(__instance);
            if (string.IsNullOrEmpty(identity))
                return; // 纯基础种族，不做任何修改
        }
        // ==========================================

        if ((__instance.Figure != EFigure.Girl && __instance.Figure != EFigure.Woman)
            || !GameData.TryGetSpeciesCore(__instance, out ISpeciesCoreData? speciesCoreData)
            || speciesCoreData == null)
            return;

        __instance.DisplayName = null;
        __instance.Figure = speciesCoreData.Figure;
        __instance.SkinColorType = 0;
        __instance.SkinColor = speciesCoreData.SkinColors[Random.Range(0, speciesCoreData.SkinColors.Length)];
        __instance.HairColor = speciesCoreData.HairColors != null
            ? speciesCoreData.HairColors[Random.Range(0, speciesCoreData.HairColors.Length)]
            : __instance.HairColor;
        __instance.ScaleColor = speciesCoreData.ScaleColors != null
            ? SkinColorSystem.PickRandom(speciesCoreData.ScaleColors)
            : __instance.SkinColor;
        __instance.HornColor = speciesCoreData.HornColors != null
            ? speciesCoreData.HornColors[Random.Range(0, speciesCoreData.HornColors.Length)]
            : __instance.HornColor;
        __instance.EyeType = Random.Range(1, 5);
        __instance.EyeBallType = speciesCoreData.EyeBallType;
        __instance.EarType = speciesCoreData.EarType;
        __instance.EarHairType = speciesCoreData.EarHairType;
        __instance.HornType = speciesCoreData.HornType;
        __instance.HornDragonType = speciesCoreData.HornDragonType;
        __instance.TailType = speciesCoreData.TailType;
        __instance.TailDragonType = speciesCoreData.TailDragonType;
        PlayDataSystem.ApplyWeightedBreastSize(
            __instance,
            speciesCoreData.TitsTypes,
            allowExtraSize: false,
            $"FemaleInitialize/{speciesCoreData.InternalName}");
        __instance.HairBodyType = speciesCoreData.HairBodyType;
        __instance.UpperBodyType = speciesCoreData.UpperBody;
        __instance.LowerBodyType = speciesCoreData.LowerBody;
        __instance.DecreasingMentalityOnHunger = speciesCoreData.MilkQuality;
        __instance.ConceptionRate = (float)speciesCoreData.ConceptionRate / 100f;
        __instance.MaxHealth = (float)speciesCoreData.MaxHealth;
        __instance.GrowthTime = (float)speciesCoreData.GrowthTime;
        __instance.MaxBirthCount = speciesCoreData.MaxBirthCount;
        __instance.Maintenance = speciesCoreData.Maintenance;
        __instance.DecreasingMentalityOnPregnant = speciesCoreData.DecreasingMentalityOnPregnant;
        __instance.Health = (float)speciesCoreData.MaxHealth;
        FemaleSpeciesSetup.HairColorMigration(
            __instance,
            speciesCoreData.HairColorMigration,
            speciesCoreData.HairColors,
            (float?)Math.Min(1f, speciesCoreData.HairColorMigrationPower ?? 0f));
        if (speciesCoreData.HairColorSameAsSkin)
            __instance.HairColor = __instance.SkinColor;
    }
}