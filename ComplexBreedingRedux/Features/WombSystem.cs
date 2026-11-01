#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ComplexBreedingRedux.Data;
using ComplexBreedingRedux.Data.Config;
using ComplexBreedingRedux.Properties;
using MBMScripts;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ComplexBreedingRedux.Features;

internal enum EWombSize
{
    Small = 1,
    Average = 2,
    Big = 3,
    Huge = 4,
    Enormous = 5,
}

internal static class WombSystem
{
    /// <summary>女儿继承母亲子宫尺寸的基础概率（%）；未继承时尺寸 -1。</summary>
    private const int WombSizeInheritChancePercent = 50;

    // ──────────────────────────────────────────────
    //  子宫尺寸 — 读取 / 写入
    // ──────────────────────────────────────────────

    /// <summary>从 buff 系统中读取雌性的子宫尺寸。限制在 [1,5]。</summary>
    public static EWombSize GetWombSize(Female female)
        => (EWombSize)Mathf.Clamp(BuffGetInt(ConfigBuffs.SlaveWombSize, female), 1, 5);

    /// <summary>直接写入子宫尺寸（用于 TentacleSystem）。</summary>
    public static void SetWombSizeDirect(Female female, int value)
        => BuffSetInt(ConfigBuffs.SlaveWombSize, value, female);

    /// <summary>
    /// 在女儿出生时调用（NewFetus）。
    /// <see cref="WombSizeInheritChancePercent"/>% 概率继承母亲的子宫尺寸；否则尺寸 -1。
    /// </summary>
    public static void InheritWombSize(Character motherCharacter, Female child)
    {
        Female? mother = motherCharacter as Female;
        if (mother == null) return;
        int wombSize = (int)GetWombSize(mother);
        if (wombSize == 1 || Random.Range(1, 101) <= WombSizeInheritChancePercent)
            BuffSetInt(ConfigBuffs.SlaveWombSize, wombSize, child);
        else
            BuffSetInt(ConfigBuffs.SlaveWombSize, wombSize - 1, child);
    }

    // ──────────────────────────────────────────────
    //  累积子宫拉伸系统
    // ──────────────────────────────────────────────

    /// <summary>每次行为对每个种族类别的基础进度。</summary>
    private static readonly Dictionary<ERaceSize, float> RaceBaseProgress = new Dictionary<ERaceSize, float>
    {
        { ERaceSize.Human,  0.03f },  // 人类（客人）— 约 34 次从 S→A
        { ERaceSize.Small,  0.04f },  // 小型（地精）— 约 25 次从 S→A
        { ERaceSize.Medium, 0.07f },  // 中型（兽人/狼人）— 约 15 次从 S→A
        { ERaceSize.Large,  0.12f },  // 大型（马/牛头人/蝾螈）— 约 9 次从 S→A
    };

    /// <summary>升级到下一级的成本除数。</summary>
    private static readonly Dictionary<int, float> LevelCostDivisor = new Dictionary<int, float>
    {
        { 1, 2.0f },  // 小型   → 平均
        { 2, 2.5f },  // 平均 → 较大
        { 3, 3.0f },  // 较大   → 巨大
        { 4, 4.0f },  // 巨大   → 巨型
    };

    private enum ERaceSize { Human, Small, Medium, Large }

    /// <summary>
    /// 为一次行为增加子宫拉伸进度。
    /// 从 BreedingSystem.Apply、CumShotOutsidePatch（体外/避孕套）调用。
    /// </summary>
    public static void AddStretchProgress(Character femaleChar, Character monster, bool isInside, int fetusCount, bool withCondom = false, bool justConceived = false)
    {
        Female? female = femaleChar as Female;
        if (female == null) return;
        if (monster is Tentacle) return;

        int wombSize = (int)GetWombSize(female);

        if (wombSize >= 5) return;
        if (female.BirthCount <= wombSize * 2) return;

        float baseProgress = GetRaceBaseProgress(monster);
        float penisMultiplier = GetPenisMultiplier(monster);
        float fetusBonus = justConceived ? 1f + fetusCount * 0.25f : 1f;
        float finishMultiplier = isInside ? 1.0f : 0.3f;
        float condomMultiplier = withCondom ? 0.7f : 1.0f;
        float factionMul = 1.0f;
        float levelCost = LevelCostDivisor.TryGetValue(wombSize, out float div) ? div : 1f;

        // 获取女性的 Trait98 值（若没有该特质，则返回 0）
        float trait98Value = female.GetTraitValue(ETrait.Trait98);
        // 计算加成因子：例如每 1 点 Trait98 值提升 1% 进度，即乘以 (1 + trait98Value / 100)
        float trait98Multiplier = 1.0f + (trait98Value / 10.0f);

        // 然后将 delta 乘以该因子
        float delta = baseProgress * penisMultiplier * fetusBonus * finishMultiplier * condomMultiplier * factionMul / levelCost * trait98Multiplier;

        float prev = GetStretchProgress(female);
        float current = Math.Min(prev + delta, 1f);

        if (current >= 1f)
        {
            BuffSetInt(ConfigBuffs.SlaveWombSize, wombSize + 1, female);
            SetStretchProgress(female, 0f);
            female.PopUpMessage(Loc("Popup_WombDilated"), null, 0.5f);
        }
        else
        {
            SetStretchProgress(female, current);
        }
    }

    /// <summary>返回拉伸进度（0.0–1.0），供 UI 使用。</summary>
    public static float GetStretchProgress(Female female)
    {
        Buff? buff = female.GetBuff(ConfigBuffs.WombStretchProgress);
        if (buff == null || string.IsNullOrWhiteSpace(buff.SpriteName))
            return 0f;
        return float.TryParse(buff.SpriteName, NumberStyles.Float, CultureInfo.InvariantCulture, out float val)
            ? Mathf.Clamp01(val)
            : 0f;
    }

    // ──────────────────────────────────────────────
    //  进度辅助方法
    // ──────────────────────────────────────────────

    private static void SetStretchProgress(Female female, float value)
    {
        if (female.GetBuff(ConfigBuffs.WombStretchProgress) != null)
            female.RemoveBuff(ConfigBuffs.WombStretchProgress);
        if (value > 0f)
            female.AddBuff(ConfigBuffs.WombStretchProgress, 0f, true, false,
                value.ToString("G", CultureInfo.InvariantCulture));
    }

    private static ERaceSize GetRaceSize(Character monster)
    {
        if (monster is Client) return ERaceSize.Human;
        if (monster is Goblin) return ERaceSize.Small;
        if (monster is Orc) return ERaceSize.Medium;
        if (monster is Werewolf) return ERaceSize.Medium;
        if (monster is Horse) return ERaceSize.Large;
        if (monster is Minotaur) return ERaceSize.Large;
        if (monster is Salamander) return ERaceSize.Large;
        return ERaceSize.Medium;
    }

    private static float GetRaceBaseProgress(Character monster)
    {
        ERaceSize size = GetRaceSize(monster);
        return RaceBaseProgress.TryGetValue(size, out float val) ? val : 0.07f;
    }

    private static float GetPenisMultiplier(Character monster)
    {
        if (monster is Horse) return 1.0f;
        return monster.PenisType == 2 ? 1.5f : 1.0f;
    }

    // ──────────────────────────────────────────────
    //  Buff 存储辅助方法
    // ──────────────────────────────────────────────

    private static int BuffGetInt(EBuffType buffType, Character character)
    {
        Buff? buff = character.GetBuff(buffType);
        if (buff == null || string.IsNullOrWhiteSpace(buff.SpriteName))
            return 0;
        return int.Parse(buff.SpriteName);
    }

    private static void BuffSetInt(EBuffType buffType, int value, Character character)
    {
        if (character.GetBuff(buffType) != null)
            character.RemoveBuff(buffType);
        character.AddBuff(buffType, 0f, true, false, $"{value}");
    }

    private static string Loc(string key) => Strings.ResourceManager.GetString(key, Strings.Culture) ?? key;
}