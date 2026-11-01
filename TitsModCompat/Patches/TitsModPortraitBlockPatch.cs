#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MBMScripts;
using UnityEngine;
using UnityEngine.UI;

namespace RaceBreedingExtension.TitsModCompat.Patches
{
    /// <summary>
    /// 屏蔽 titmod 对“独特角色（特殊NPC）”Spine 肖像（EnableInfoPortrait）的替换，
    /// 使其回归原版静态肖像；非独特角色不受影响（保留 titmod 的动态 Spine 肖像）。
    ///
    /// 原理：titmod 的 TitsMod.Patches.InformationAnimPatch.RebuildSpine(Transform, bool)
    /// 在替换成功后会把原静态肖像 Standing Image 的 alpha 置 0 并叠加一个 Spine SkeletonGraphic；
    /// 对独特角色（特殊NPC）该 Spine 图形无法正确渲染，导致肖像区域变黑。
    /// 本补丁在前置阶段检测到独特角色时：还原 Standing Image、销毁已生成的 Spine 图形、
    /// 调用 titmod 内部 Cleanup() 复位其静态状态，并跳过原方法，从而保留原版肖像。
    ///
    /// 注意：本类不使用 [HarmonyPatch] 特性自动应用（RaceBreedingExtension 的加载顺序早于
    /// TitsMod，PatchAll 处理本程序集时 titsmod 类型尚不存在，且 Harmony 2.16 对 TargetMethod()
    /// 返回 null 会抛异常中止整个 PatchAll），而是由 TitsModCompatSystem 在 ModLoader 的
    /// OnAllModsLoaded 事件（所有模组加载完毕后触发）中手动应用补丁。
    /// </summary>
    internal static class TitsModPortraitBlockPatch
    {
        /// <summary>全部独特角色（特殊NPC）的类型全名（来源：用户提供的完整列表）。</summary>
        private static readonly HashSet<string> UniqueCharacterTypeNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "MBMScripts.Anna",
            "MBMScripts.Aure",
            "MBMScripts.Bella",
            "MBMScripts.Claire",
            "MBMScripts.Karen",
            "MBMScripts.Nero",
            "MBMScripts.Sylvia",
            "MBMScripts.Vivi",
            "MBMScripts.Amilia2",
            "MBMScripts.Barbara2",
            "MBMScripts.Flora2",
            "MBMScripts.Lena2",
            "MBMScripts.Niel2",
            "MBMScripts.Sena2",
        };

        private static bool _resolved;
        private static MethodInfo? _targetMethod;
        private static MethodInfo? _cleanupMethod;

        /// <summary>
        /// 动态定位 titsmod 的方法：TitsMod.Patches.InformationAnimPatch.RebuildSpine(Transform, bool)
        /// 及其私有 Cleanup()。titsmod 未安装（类型不存在）时返回 false 且 target 为 null。
        /// </summary>
        internal static bool TryResolveTarget(out MethodInfo? target)
        {
            if (_resolved)
            {
                target = _targetMethod;
                return target != null;
            }
            _resolved = true;

            Type? type = AccessTools.TypeByName("TitsMod.Patches.InformationAnimPatch");
            if (type == null)
            {
                _targetMethod = null;
                _cleanupMethod = null;
                target = null;
                return false;
            }

            _targetMethod = AccessTools.Method(type, "RebuildSpine", new[] { typeof(Transform), typeof(bool) }) as MethodInfo;
            if (_targetMethod != null)
                _cleanupMethod = AccessTools.Method(type, "Cleanup", Type.EmptyTypes) as MethodInfo;

            target = _targetMethod;
            return target != null;
        }

        /// <summary>
        /// 前置拦截：开关开启且当前角色为独特角色时，还原原版静态肖像并跳过 titmod 的 Spine 替换。
        /// 非独特角色直接放行（保留 titmod 的动态 Spine 肖像）。
        /// </summary>
        internal static bool Prefix(Transform window, bool force)
        {
            if (!TitsModCompatConfig.BlockUniquePortrait) return true;
            if (window == null) return true;

            try
            {
                var tu = window.GetComponentInParent<TargetUnit>();
                var character = (tu != null ? tu.Unit : null) as Character;
                if (character == null) return true;
                if (!IsUniqueCharacter(character)) return true;

                RestoreOriginalPortrait(window);
                return false; // 跳过 titsmod 的 RebuildSpine，保留原版静态肖像
            }
            catch (Exception e)
            {
                TitsModCompatSystem.LogError("Prefix failed, fallback to TitsMod behavior: " + e);
                return true;
            }
        }

        private static bool IsUniqueCharacter(Character character)
        {
            Type type = character.GetType();
            return type != null && UniqueCharacterTypeNames.Contains(type.FullName ?? string.Empty);
        }

        /// <summary>
        /// 还原原版静态肖像：
        ///  1) Standing Image 恢复可见（颜色还原为白色，与 titmod Cleanup 行为一致）；
        ///  2) 销毁 titmod 已创建的 Spine 图形（TitsMod_InformationSpine）；
        ///  3) 调用 titmod 内部 Cleanup() 同步其静态状态，防止切换角色后残留影响。
        /// </summary>
        private static void RestoreOriginalPortrait(Transform window)
        {
            var standingImageTransform = window.Find("Standing Image");
            if (standingImageTransform != null)
            {
                var image = standingImageTransform.GetComponent<Image>();
                if (image != null)
                    image.color = Color.white;

                var spine = standingImageTransform.Find("TitsMod_InformationSpine");
                if (spine != null)
                    UnityEngine.Object.Destroy(spine.gameObject);
            }

            try
            {
                _cleanupMethod?.Invoke(null, null);
            }
            catch (Exception e)
            {
                TitsModCompatSystem.LogError("TitsMod Cleanup() invoke failed: " + e);
            }
        }
    }
}
