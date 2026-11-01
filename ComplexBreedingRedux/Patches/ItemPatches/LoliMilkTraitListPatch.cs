#nullable enable
using System;
using System.Collections.Generic;
using HarmonyLib;
using MBMScripts;

namespace ComplexBreedingRedux.Patches.ItemPatches;

/// <summary>
/// 萝莉母乳（LoliMilk）效果：与其他母乳完全同一套机制 ——
/// 使用时 15% 几率（首次使用必定触发）从下面的列表中随机取一条生效。
///
/// 效果为「降级 Trait99 / Trait100」（即增加这两个属性的正值）：
///   交配时间 +5 秒           （Trait99）
///   交配时每秒体力消耗量 +0.05（Trait100）
/// 数值幅度与游戏原生 <c>Character.UpgradeTrait</c> 的每级幅度一致，方向取反即为降级。
/// </summary>
[HarmonyPatch(typeof(GameManager), nameof(GameManager.GetMilkTraitList))]
internal static class LoliMilkTraitListPatch
{
    private static readonly List<ValueTuple<ETrait, float>> LoliMilkTraitList = new List<ValueTuple<ETrait, float>>
    {
        new ValueTuple<ETrait, float>(ETrait.Trait99, 5f),
        new ValueTuple<ETrait, float>(ETrait.Trait100, 0.05f),
    };

    public static void Postfix(EItemType unit, ref List<ValueTuple<ETrait, float>> __result)
    {
        if (unit == EItemType.LoliMilk)
            __result = LoliMilkTraitList;
    }
}
