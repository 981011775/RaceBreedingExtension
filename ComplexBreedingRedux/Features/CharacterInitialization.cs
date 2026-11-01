#nullable enable
using System.Collections.Generic;
using System.Linq;
using ComplexBreedingRedux.Data;
using ComplexBreedingRedux.Data.Professions;
using ComplexBreedingRedux.Data.Species;
using ComplexBreedingRedux.Data.SpeciesCore;
using HarmonyLib;
using MBMScripts;
using Random = UnityEngine.Random;

namespace ComplexBreedingRedux.Features;

/// <summary>
/// 补丁（无参数）：<c>InitializeTrait()</c>
/// 让原版方法正常执行（恢复一般特性分配），然后在 Postfix 中添加职业和 GuaranteedTraits。
/// </summary>
[HarmonyPatch(typeof(Character), "InitializeTrait", new System.Type[] { })]
internal static class InitializeTraitPatch
{
    // 1. 不阻止原方法执行
    public static bool Prefix() => true;

    // 2. 原方法执行后，添加我们的自定义内容
    public static void Postfix(Character __instance)
    {
        // 仅对女性角色生效（男性角色保持原版）
        if (__instance is not Female)
            return;

        // 添加物种核心的 GuaranteedTraits（如 Ghoul 的 Trait108 等）
        ISpeciesCoreData? speciesCoreData = GameData.SpeciesCoreDataList
            .FirstOrDefault(x => x.Race == __instance.Race);
        if (speciesCoreData?.GuaranteedTraits != null)
        {
            foreach (ETrait trait in speciesCoreData.GuaranteedTraits)
            {
                if (!__instance.TraitContains(trait))
                    __instance.AddRaceTrait(trait);
            }
        }

        // 分配职业（基于随机 Tier）
        SetProfession(__instance);

        // ★ 血脉（Bloodline）：按物种补发（取代原高等种族特质 HighRaceTrait）
        ApplySpeciesBloodline(__instance);

        __instance.SortTrait();
        __instance.Health = __instance.MaxHealth;

    }

    /// <summary>
    /// 根据角色的物种（SpeciesBloodlineMap）分配血脉。
    /// 仅在角色当前没有血脉时生效（避免覆盖已迁移/已继承的血脉）。
    /// </summary>
    private static void ApplySpeciesBloodline(Character character)
        => RaceBreedingExtension.Bloodline.BloodlineSystem.ApplySpeciesBloodline(character);

    // 职业分配逻辑（从原版移植，无改动）
    private static void SetProfession(Character character)
    {
        int roll = Random.Range(1, 101);
        int tier = 1;
        if (roll <= 20)
        {
            if (roll > 10)
                tier = 3;
            else if (roll > 3)
                tier = 4;
            else
                tier = 5;
        }
        else if (roll <= 40)
        {
            tier = 2;
        }

        List<IProfessionData> candidates = GameData.ProfessionDataList
            .Where(x => (x.ForbiddenStartingSpecies == null || !x.ForbiddenStartingSpecies.Contains(character.Race))
                        && x.Tier == tier)
            .ToList();

        if (GameManager.Instance.PlayerData.GetUnitList(ESector.Female).Count == 0)
            candidates = new List<IProfessionData> { ProfessionRegistry.Cultist };

        if (!candidates.Any())
            return;

        IProfessionData profession = candidates[Random.Range(0, candidates.Count)];
        character.AddTrait(GameBalance.ProfessionTraitId);
        character.SetTraitValue(GameBalance.ProfessionTraitId, (float)profession.Id);

        // 职业保证特质（如 Paladin 的 Trait108）保留
        if (profession.GuaranteedTraits != null)
        {
            foreach (ETrait trait in profession.GuaranteedTraits)
            {
                if (!character.TraitContains(trait))
                    character.AddRaceTrait(trait);
            }
        }

        character.SortTrait();
    }
}

/// <summary>
/// 补丁 Player.InitializeTrait()：为玩家设置 SexTime 和 ConceptionRate。
/// 注意：不再调用 OnEnableTrait / ClearRaceTrait / ClearTrait ——
/// 原实现会清空玩家自身的种族特质与一般特质（玩家原有属性），导致玩家属性丢失。
/// </summary>
[HarmonyPatch(typeof(Player), "InitializeTrait", new System.Type[] { })]
internal static class PlayerInitializeTraitPatch
{
    public static void Postfix(Player __instance)
    {
        // 仅补充这两项数值，保留玩家原有属性/特质
        __instance.SexTime = 120f;
        __instance.ConceptionRate = 0.5f;
    }
}