using ComplexBreedingRedux.Data;
using ComplexBreedingRedux.Features;
using ComplexBreedingRedux.Patches;
using ComplexBreedingRedux.Patches.AppearancePatches;
using ComplexBreedingRedux.Patches.InitPatches;
using ComplexBreedingRedux.Properties;
using HarmonyLib;
using MBM.ModLoader.Core;
using MBMScripts;
using System;
using UnityEngine;

namespace ComplexBreedingRedux;

public class Plugin
{
    public const string ModName = "ComplexBreedingRedux";

    private static Harmony? _harmony;

    /// <summary>
    /// 由合并后的 RaceBreedingExtension.ModEntry.Load() 调用。
    /// 不再作为独立入口（internal，避免 ModLoader 重复发现 Load()）。
    /// </summary>
    internal static void Initialize()
    {
        GameData.Initialize();
        Strings.Culture = Localization.CurrentCulture;
        LocalizationSystem.RefreshLocalizedStrings();
        ModConfig.Register();
        Localization.OnLanguageChanged += OnLanguageChanged;

        _harmony = new Harmony(ModName);

        // 原有：NPC 外观补丁
        NpcInitPatches.Register(_harmony);

        // 新增：Golem 等物种的换皮重定向补丁（手动注册，不能依赖 [HarmonyPatch] 自动应用）
        GolemReskinPatches.Register(_harmony);

        // 新增：Trait 继承补丁（融合 InheritanceChallenge 逻辑）
        var initTraitMethod = AccessTools.Method(
            typeof(Character),
            "InitializeTrait",
            new[] { typeof(Character), typeof(Character) }
        );
        var initTraitPrefix = new HarmonyMethod(
            typeof(InitializeTraitWithParentsPatch),
            nameof(InitializeTraitWithParentsPatch.Prefix)
        );
        _harmony.Patch(initTraitMethod, prefix: initTraitPrefix);

        _harmony.Patch(
        AccessTools.Method(typeof(Character), "ChangeToAdult"),
        postfix: new HarmonyMethod(typeof(ChangeToAdultPatch), nameof(ChangeToAdultPatch.Postfix))
        );

        Debug.Log($"[CBR] Trait inheritance patch applied.");

        Debug.Log($"[CBR] loaded, lang: " + Localization.CurrentLanguageCode);
    }

    private static void OnLanguageChanged(string langCode)
    {
        Strings.Culture = Localization.CurrentCulture;
        LocalizationSystem.InvalidateCaches();
        LocalizationSystem.RefreshLocalizedStrings();
        ModConfig.UpdateDescriptions();
        Debug.Log($"[CBR] language changed: {langCode}");
    }
}