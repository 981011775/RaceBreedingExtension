#nullable enable
using System;
using System.Collections.Generic;
using HarmonyLib;
using MBMScripts;
using ComplexBreedingRedux.Data;
// 注意：不添加 using UnityEngine，以避免 Random 歧义

namespace ComplexBreedingRedux.Patches.InitPatches;

/// <summary>
/// 融合 InheritanceChallenge 的继承逻辑：
/// - 基础种族：完整继承（RaceTrait + 普通 Trait）
/// - 进化种族：仅继承普通 Trait（RaceTrait 留空，由 GuaranteedTraits 手动补充）
/// </summary>
[HarmonyPatch(typeof(Character), "InitializeTrait", new[] { typeof(Character), typeof(Character) })]
internal static class InitializeTraitWithParentsPatch
{
    // ----- 反射访问器（从 InheritanceChallenge 移植）-----
    private static readonly AccessTools.FieldRef<Character, List<ETrait>> RaceTraitListRef =
        AccessTools.FieldRefAccess<Character, List<ETrait>>("m_RaceTraitList");

    private static readonly AccessTools.FieldRef<Character, List<ETrait>> TraitListRef =
        AccessTools.FieldRefAccess<Character, List<ETrait>>("m_TraitList");

    private static readonly Func<Character, SeqDictionary<ETrait, TraitInfo>> GetTraitInfoDict =
        (Func<Character, SeqDictionary<ETrait, TraitInfo>>)Delegate.CreateDelegate(
            typeof(Func<Character, SeqDictionary<ETrait, TraitInfo>>),
            AccessTools.PropertyGetter(typeof(Character), "TraitInfoDictionary")
        );

    // ----- Harmony Prefix（完全替换原方法）-----
    public static bool Prefix(Character __instance, Character female, Character male)
    {
        // 1. 清空子代所有 Trait（原版逻辑）
        __instance.OnEnableTrait();
        __instance.ClearRaceTrait();
        __instance.ClearTrait();

        // 2. 判断子代是否为“进化物种”（使用父本 DisplayName）
        bool isEvolved = !string.IsNullOrEmpty(male.DisplayName) && GameData.TryGetSpecies(male.DisplayName, out _);

        // 3. 构建候选特质列表（父母双方的交集 + 并集）
        var motherDict = GetTraitInfoDict(female);
        var fatherDict = GetTraitInfoDict(male);
        var candidates = GetTraitCandidates(motherDict, fatherDict);

        // 4. 继承 RaceTrait（仅基础种族执行）
        if (!isEvolved)
        {
            var parentRaceTraits = __instance is Female
                ? RaceTraitListRef(female)
                : RaceTraitListRef(male);
            foreach (ETrait trait in parentRaceTraits)
            {
                __instance.AddRaceTrait(trait);
            }
        }

        // 5. 继承普通 Trait（使用 50/50 模式，兼容 Trait67）
        List<(ETrait eTrait, float value)> childTraits = new();
        bool motherHasTrait67 = female.TraitContains(ETrait.Trait67);

        foreach (var (eTrait, motherInfo, fatherInfo) in candidates)
        {
            // ★★★ 关键修复：跳过职业特质，由 SetTraitsAndProfession 单独处理 ★★★
            if (eTrait == GameBalance.ProfessionTraitId)
                continue;

            // ★ 血脉（ETrait.Trait9）：由 BloodlineSystem 单独处理——
            //   Trait9 上存的是「血脉 ID × 1000 + 等级」的单值映射，不参与普通特质继承。
            //   该槽位在游戏 TraitData 数据库中不存在，若在此继承会在 Trait67 分支空引用崩溃。
            if (eTrait == (ETrait)9)
                continue;

            if (motherInfo == null && fatherInfo == null) continue;

            // ★ Trait93~Trait100（普通属性）：父母双方混合继承（取双方数值的平均值），
            //   修正此前“只继承母亲属性 / 只取单亲数值”的问题。
            if (eTrait >= ETrait.Trait93 && eTrait <= ETrait.Trait100)
            {
                float inherited;
                if (motherInfo != null && fatherInfo != null)
                    inherited = (motherInfo.Value + fatherInfo.Value) * 0.5f;
                else if (motherInfo != null)
                    inherited = motherInfo.Value;
                else
                    inherited = fatherInfo!.Value;

                childTraits.Add((eTrait, inherited));

                // 诊断日志（需在模组菜单开启 EnableLogging）
                if (DepravedShield.ModConfig.EnableLogging)
                    UnityEngine.Debug.Log($"[TraitInherit] {eTrait}: 母={motherInfo?.Value.ToString() ?? "-"} 父={fatherInfo?.Value.ToString() ?? "-"} → 子={inherited}");
                continue;
            }

            TraitInfo? selected = null;

            if (motherInfo == null) selected = fatherInfo;
            else if (fatherInfo == null) selected = motherInfo;
            else
            {
                if (motherHasTrait67)
                {
                    // ★ 无 TraitData 元数据的槽位（血脉 Trait9 等模组自用槽位）按“正向”处理，
                    //   避免 Database.GetDataByDataId 返回 null 后访问 IsPositive 抛空引用。
                    bool isPositive = TryGetTraitData(eTrait)?.IsPositive ?? true;
                    bool motherBetter = isPositive
                        ? motherInfo.Value > fatherInfo.Value
                        : motherInfo.Value < fatherInfo.Value;
                    selected = motherBetter ? motherInfo : fatherInfo;
                }
                else
                {
                    // ★★★ 修改此处：显式使用 UnityEngine.Random.value 消除歧义 ★★★
                    selected = UnityEngine.Random.value < 0.5f ? motherInfo : fatherInfo;
                }
            }

            if (selected != null)
                childTraits.Add((eTrait, selected.Value));
        }

        // 将继承的普通 Trait 写入子代
        foreach (var (eTrait, value) in childTraits)
        {
            __instance.AddTrait(eTrait);
            __instance.AddTraitValue(eTrait, value);
        }

        // ----- 6. 保留原版特殊的 Trait76 / Trait90 处理（从 InheritanceChallenge 移植）-----
        // ★ 这两段属于“附加福利”逻辑：整体 try/catch，
        //   任何异常都绝不能中断新生个体初始化流程（否则血脉属性、职业等后续写入会全部失效）。
        try
        {
            ApplyTrait76Bonus(female, __instance);
            ApplyTrait90Flip(male, female, __instance);
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogWarning("[TraitInherit] Trait76/Trait90 附加处理异常（已忽略，不影响继承结果）：" + e);
        }

        // 7. 排序并完成
        __instance.SortTrait();
        return false; // 跳过原方法
    }

    /// <summary>母本拥有 Trait76（母乳特质）时，随机强化子代的一条属性特质（原版行为）。</summary>
    private static void ApplyTrait76Bonus(Character mother, Character child)
    {
        if (!mother.TraitContains(ETrait.Trait76))
            return;

        var instanceTraits = TraitListRef(child);
        if (instanceTraits.Count == 0)
            return;

        ETrait target = instanceTraits[UnityEngine.Random.Range(0, instanceTraits.Count)];
        float val = child.GetTraitValue(target);
        switch (target)
        {
            case ETrait.Trait93:
                child.AddTraitValue(target, val + GameManager.ConfigData.MinIncreaseOrDecreaseConceptionRate);
                break;
            case ETrait.Trait94:
                child.AddTraitValue(target, val - GameManager.ConfigData.MinIncreaseOrDecreaseGrowthTime);
                break;
            case ETrait.Trait95:
                child.AddTraitValue(target, val - GameManager.ConfigData.MinIncreaseOrDecreaseFoodConsumption);
                break;
            case ETrait.Trait96:
                child.AddTraitValue(target, val + GameManager.ConfigData.MinIncreaseOrDecreaseHealth);
                break;
            case ETrait.Trait97:
                child.AddTraitValue(target, val + GameManager.ConfigData.MinIncreaseOrDecreaseBirthCount);
                break;
            case ETrait.Trait98:
                child.AddTraitValue(target, val + GameManager.ConfigData.MinIncreaseOrDecreaseMultiplePregnancyCount);
                break;
            case ETrait.Trait99:
                child.AddTraitValue(target, val - GameManager.ConfigData.MinIncreaseOrDecreaseSexTime);
                break;
            case ETrait.Trait100:
                child.AddTraitValue(target, val - GameManager.ConfigData.MinIncreaseOrDecreaseDecreasingMentalityOnSex);
                break;
        }
    }

    /// <summary>父本拥有 Trait90 时，按概率翻转子代一条“方向错误”的属性特质（原版行为）。</summary>
    private static void ApplyTrait90Flip(Character father, Character mother, Character child)
    {
        if (!father.TraitContains(ETrait.Trait90))
            return;

        if (UnityEngine.Random.Range(0f, 100f) >= GameManager.GetTraitValue(ETrait.Trait90, 0))
            return;

        var instanceTraits = TraitListRef(child);
        List<ETrait> flipCandidates = new();
        foreach (ETrait t in instanceTraits)
        {
            // ★ 必须跳过没有 TraitData 元数据的槽位（血脉 Trait9、职业 Trait91 等模组自用槽位）：
            //   原版 Database.GetDataByDataId 对不存在的 Id 返回 null，直接读 IsPositive 会抛空引用，
            //   从而中断整条新生个体初始化流程。
            TraitData? meta = TryGetTraitData(t);
            if (meta == null)
                continue;

            float val = child.GetTraitValue(t);
            if ((meta.IsPositive && val < 0f) || (!meta.IsPositive && val > 0f))
                flipCandidates.Add(t);
        }

        if (flipCandidates.Count == 0)
            return;

        ETrait target = flipCandidates[UnityEngine.Random.Range(0, flipCandidates.Count)];
        child.SetTraitValue(target, -child.GetTraitValue(target));
        mother.PopUpMessage(ETrait.Trait90.GetName());
    }

    /// <summary>
    /// 安全获取特质元数据：模组自用槽位（血脉 Trait9、职业 Trait91 等）在游戏 TraitData 数据库中
    /// 不存在，原版 GetDataByDataId 对不存在的 Id 返回 null。
    /// </summary>
    private static TraitData? TryGetTraitData(ETrait trait)
        => Database<TraitData>.GetDataByDataId((int)trait);

    // ----- 辅助方法：合并父母的 Trait 字典（来自 InheritanceChallenge）-----
    private static List<(ETrait eTrait, TraitInfo? mother, TraitInfo? father)> GetTraitCandidates(
        SeqDictionary<ETrait, TraitInfo> motherDict,
        SeqDictionary<ETrait, TraitInfo> fatherDict)
    {
        var result = new List<(ETrait, TraitInfo?, TraitInfo?)>(
            Math.Max(motherDict.Count, fatherDict.Count)
        );

        foreach (var kvp in motherDict)
        {
            fatherDict.TryGetValue(kvp.Key, out var fatherVal);
            result.Add((kvp.Key, kvp.Value, fatherVal));
        }

        foreach (var kvp in fatherDict)
        {
            if (!motherDict.ContainsKey(kvp.Key))
                result.Add((kvp.Key, null, kvp.Value));
        }

        return result;
    }
}