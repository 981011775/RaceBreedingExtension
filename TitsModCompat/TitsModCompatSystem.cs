#nullable enable
using System;
using System.Reflection;
using HarmonyLib;
using MBM.ModLoader.Core;
using RaceBreedingExtension.TitsModCompat.Patches;
using UnityEngine;

namespace RaceBreedingExtension.TitsModCompat
{
    /// <summary>
    /// titmod 兼容子系统入口（由 RaceBreedingExtension.ModEntry.Load() 调用）。
    ///
    /// 功能：通过模组菜单开关屏蔽 titmod 的 EnableInfoPortrait 对“独特角色（特殊NPC）”
    /// Spine 肖像的替换（修复独特角色肖像变黑），使其回归原版静态肖像；
    /// 非独特角色的修改完整保留（仍显示 titmod 的动态 Spine 肖像）。
    ///
    /// 重要：RaceBreedingExtension 的加载顺序早于 TitsMod，因此补丁不能依赖 [HarmonyPatch]
    /// 自动 PatchAll（此时 titsmod 类型尚不存在，且 Harmony 2.16 对 TargetMethod() 返回 null
    /// 会抛异常中止整个 PatchAll）。本子系统在 ModLoader 的 Loader.OnAllModsLoaded 事件
    /// （所有模组加载完毕后触发）中手动应用补丁；未安装 titsmod 时自动跳过，对原版无副作用。
    /// </summary>
    internal static class TitsModCompatSystem
    {
        private static bool _initialized;
        private static bool _patchApplied;

        private static readonly Harmony HarmonyInstance = new Harmony("RaceBreedingExtension.TitsModCompat");

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            try
            {
                // ★ 本地化：设置当前语言文化（必须在注册菜单描述之前）
                Properties.Strings.Culture = Localization.CurrentCulture;

                TitsModCompatConfig.Register();
                Localization.OnLanguageChanged += OnLanguageChanged;

                // 补丁必须在 titsmod 加载完成后再应用（RBE 加载顺序早于 titsmod）。
                if (Loader.AllModsLoaded)
                    ApplyPatchOnce();
                else
                    Loader.OnAllModsLoaded += OnAllModsLoadedHandler;

                Log("TitsMod compatibility subsystem initialized (unique NPC Spine portrait blocking).");
            }
            catch (Exception e)
            {
                LogError("TitsMod compatibility initialize failed: " + e);
            }
        }

        /// <summary>游戏语言切换时刷新本地化资源文化与模组菜单描述。</summary>
        private static void OnLanguageChanged(string langCode)
        {
            try
            {
                Properties.Strings.Culture = Localization.CurrentCulture;
                TitsModCompatConfig.OnLanguageChanged();
                Log("Language changed: " + langCode);
            }
            catch (Exception e)
            {
                LogError("Language change failed: " + e);
            }
        }

        private static void OnAllModsLoadedHandler()
        {
            try
            {
                ApplyPatchOnce();
            }
            catch (Exception e)
            {
                LogError("Apply patch on AllModsLoaded failed: " + e);
            }
        }

        /// <summary>解析 titsmod 方法并手动挂载前缀补丁（仅一次）。</summary>
        private static void ApplyPatchOnce()
        {
            if (_patchApplied) return;

            if (!TitsModPortraitBlockPatch.TryResolveTarget(out MethodInfo? target) || target == null)
            {
                Log("TitsMod not installed — unique NPC portrait block patch skipped (vanilla compatible).");
                return;
            }

            _patchApplied = true;

            HarmonyInstance.Patch(target, prefix: new HarmonyMethod(
                AccessTools.DeclaredMethod(typeof(TitsModPortraitBlockPatch), nameof(TitsModPortraitBlockPatch.Prefix))));

            Log("TitsMod unique NPC portrait block patch applied.");
        }

        internal static void Log(string msg) => Debug.Log($"[{ModEntry.ModName}][TitsModCompat] {msg}");
        internal static void LogWarning(string msg) => Debug.LogWarning($"[{ModEntry.ModName}][TitsModCompat] {msg}");
        internal static void LogError(string msg) => Debug.LogError($"[{ModEntry.ModName}][TitsModCompat] {msg}");
    }
}

