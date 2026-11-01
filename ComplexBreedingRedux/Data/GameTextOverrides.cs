#nullable enable
using System;
using ComplexBreedingRedux.Properties;
using MBM.ModLoader.Core;
using MBMScripts;

namespace ComplexBreedingRedux.Data;

/// <summary>
/// 原版文本替换表：按「原版本地化键」整体替换名称 / 描述 / 物品说明。
///
/// 由 <see cref="ComplexBreedingRedux.Features.LocalizationSystem.ApplyLocalize"/> 在
/// <c>SeqLocalization.Localize</c> 阶段拦截，因此会覆盖原版（含任何本地化数据）的文本。
///
/// 文本本身不再硬编码在 C# 里，而是放进标准本地化文件
/// （<c>ComplexBreedingRedux\Properties\Strings.resx</c> / <c>Strings.zh-cn.resx</c> / <c>Strings.ru.resx</c>），
/// 键名统一为 <c>Override_*</c>，从而跟随 <see cref="Strings.Culture"/> 一起切换 EN / zh-CN / ru。
///
/// 当前替换项（原版键 → Override_* 键）：
///   #Trait_108_Name / #Trait_108_Text / #Trait_108_Tooltip → Override_Trait108_*
///   #Trait_0101200000 / #Trait_71 / #Trait_71_Name          → Override_Trait71_Name
///   #Trait_0101200000_Tooltip / #Trait_71_Tooltip / #Trait_71_Text → Override_Trait71_Tooltip
///   #Item127Text                                            → Override_Item127_Text
/// </summary>
internal static class GameTextOverrides
{
    /// <summary>从标准本地化文件读取（缺失时回退到键名），并保证使用当前语言。</summary>
    private static string Text(string key)
    {
        Strings.Culture = Localization.CurrentCulture;
        return Strings.ResourceManager.GetString(key, Strings.Culture) ?? key;
    }

    /// <summary>命中替换表时返回 true，并把替换后的文本写入 <paramref name="value"/>。</summary>
    public static bool TryGet(string? key, out string value)
    {
        value = string.Empty;
        if (string.IsNullOrEmpty(key))
            return false;

        switch (key)
        {
            // ─── ETrait.Trait108：疾病免疫 ───
            case "#Trait_108_Name":
                value = Text("Override_Trait108_Name");
                return true;

            case "#Trait_108_Text":
            case "#Trait_108_Tooltip":
                value = Text("Override_Trait108_Text");
                return true;

            // ─── ETrait.Trait71：超凡魅力（原版键 #Trait_0101200000） ───
            case "#Trait_0101200000":
            case "#Trait_71":
            case "#Trait_71_Name":
                value = Text("Override_Trait71_Name");
                return true;

            case "#Trait_0101200000_Tooltip":
            case "#Trait_71_Tooltip":
            case "#Trait_71_Text":
                value = Text("Override_Trait71_Tooltip");
                return true;

            // ─── Item127（催淫剂）说明 ───
            case "#Item127Text":
                value = Text("Override_Item127_Text");
                return true;

            default:
                return false;
        }
    }

    // ─── 角色详情 UI 直接替换（不依赖 SeqLocalization 路径） ───

    /// <summary>特质名称替换（无替换时返回 null）。</summary>
    public static string? GetTraitName(ETrait trait)
    {
        switch (trait)
        {
            case ETrait.Trait108:   // 疾病免疫
                return Text("Override_Trait108_Name");
            case ETrait.Trait71:    // 超凡魅力
                return Text("Override_Trait71_Name");
            default:
                return null;
        }
    }

    /// <summary>特质描述替换（无替换时返回 null）。</summary>
    public static string? GetTraitTooltip(ETrait trait)
    {
        switch (trait)
        {
            case ETrait.Trait108:   // 疾病免疫
                return Text("Override_Trait108_Text");
            case ETrait.Trait71:    // 超凡魅力
                return Text("Override_Trait71_Tooltip");
            default:
                return null;
        }
    }
}

