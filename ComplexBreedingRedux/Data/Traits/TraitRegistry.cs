#nullable enable
using System.Collections.Generic;
using MBMScripts;

namespace ComplexBreedingRedux.Data.Traits;

/// <summary>所有特质的静态注册表（精简版，移除精华条件）。</summary>
internal static class TraitRegistry
{
    // AffectionCrest —— 精灵标记，非精华特质
    public static readonly TraitData AffectionCrest = new("AffectionCrest", ETrait.Trait47)
    {
        IsPositive = true,
        IsAffectingFemaleTrait = true,
        // 不设置任何精华条件
    };

    // ViralResilience（Trait108，疾病抗性）已删除：
    // 原疾病抗性 / 免疫类特质与高等种族特质（HighRaceTrait）已整体移除，
    // 一般特质 ETrait.Trait9 现由血脉系统（RaceBreedingExtension.Bloodline.BloodlineSystem）以
    // 「ID × 1000 + 等级」的单值映射方式占用，界面层显示为「种族特质」。

    // 所有特质的只读列表
    public static IReadOnlyList<TraitData> All { get; } = new[]
    {
        AffectionCrest,
    };
}