using DepravedShield.Helpers;
using DepravedShield.Patches;
using DepravedShield.Properties;          // ★ 引用资源类
using MBM.ModLoader.Core;
using System;
using UnityEngine;

namespace DepravedShield
{
    public static class ModEntry
    {
        // 必须与 DLL 文件名一致（无扩展名），ModLoader 的“Mod settings”菜单、
        // ModSettings/ModSaveData/ModLoaderAPI 均按该名称与程序集匹配。
        internal const string ModName = "RaceBreedingExtension";

        /// <summary>
        /// 由合并后的 RaceBreedingExtension.ModEntry.Load() 调用。
        /// 不再作为独立入口（internal，避免 ModLoader 重复发现 Load()）。
        /// 注意：音频相关（hit.wav 播放）已按合并要求移除；
        /// Harmony [HarmonyPatch] 补丁由 ModLoader 自动 PatchAll，此处不再手动 PatchAll。
        /// </summary>
        internal static void Initialize()
        {
            // ★ 设置当前语言文化（必须在使用 Strings 资源前）
            Strings.Culture = Localization.CurrentCulture;

            ModConfig.Register();
            Localization.OnLanguageChanged += OnLanguageChanged;
            Log("Mod loaded!");

            // ★ 初始化 UI 插槽（一次）
            ReferenceCharacterPatch.InitializeUI();

            // 注：原 RegisterCustomTraits（直接向游戏注册 Trait201 等新特质）已移除——
            // 直接添加新 RaceTrait 会导致游戏冻结；旧自定义特质（高等种族特质 HighRaceTrait）
            // 也已整体删除，改为血脉系统（RaceBreedingExtension.Bloodline.BloodlineSystem，
            // 基于一般特质 ETrait.Trait9 的值映射：血脉 ID × 1000 + 等级）。
        }

        internal static void Log(string msg) => Debug.Log($"[{ModName}] {msg}");

        /// <summary>
        /// 调试日志：仅当模组设置中开启「EnableLogging」时才输出（默认关闭）。
        /// 所有高频 / 每次事件触发的诊断信息（护盾扣减、阻止怀孕、阻止 NewFetus、
        /// 阻止获取堕落值等）都走这里，避免 output_log.txt / ModLoader.log 被大量
        /// [RaceBreedingExtension] 文本刷屏；需要排查问题时再在模组菜单打开开关。
        /// </summary>
        internal static void LogDebug(string msg)
        {
            if (ModConfig.EnableLogging)
                Debug.Log($"[{ModName}] {msg}");
        }

        internal static void LogWarning(string msg) => Debug.LogWarning($"[{ModName}] {msg}");
        internal static void LogError(string msg) => Debug.LogError($"[{ModName}] {msg}");

        private static void OnLanguageChanged(string langCode)
        {
            // ★ 更新资源文化，使 Strings 属性返回对应语言文本
            Strings.Culture = Localization.CurrentCulture;
            // ★ 刷新配置描述（ModConfig 中的 UpdateDescriptions 方法）
            ModConfig.OnLanguageChanged();
            Log($"Language changed: {langCode}");
        }
    }
}
