using DepravedShield.Helpers;
using DepravedShield.Properties;        // ★ 添加资源引用
using MBM.ModLoader.Core;
using MBMScripts;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using SystemExtensionLib.Components;
using SystemExtensionLib.Systems;
using SystemExtensionLib.Tools;
using UnityEngine;

namespace DepravedShield.Patches
{
    public static class ReferenceCharacterPatch
    {
        private static GameObject _slotOrgasm;
        private static GameObject _slotShield;
        private static UpdaterExtendedAreaManager _updaterManager;
        private static bool _uiInitialized = false;

        /// <summary> 初始化 UI 插槽（在 ModEntry.Load 中调用）</summary>
        public static void InitializeUI()
        {
            if (_uiInitialized) return;

            // 注册 OrgasmCount 插槽（原有）
            _slotOrgasm = ExtendedInfoSlotSystem.RegisterFemaleExtendedDisplaySlot(
                ModEntry.ModName, "OrgasmCount",
                () => Strings.UI_OrgasmCount,        // ★ 使用本地化
                out var typeValueOrgasm);

            if (typeValueOrgasm != null)
            {
                var refComp = typeValueOrgasm.AddComponent<ReferenceOrgasmCount>();
                var binder = typeValueOrgasm.GetComponent<BinderTextMeshProText>();
                if (binder == null)
                    binder = typeValueOrgasm.AddComponent<BinderTextMeshProText>();
                binder.SetReferenceArray(new List<Reference> { refComp });
            }

            // ★ 注册 DepravedShield 插槽（位于 OrgasmCount 下方）
            _slotShield = ExtendedInfoSlotSystem.RegisterFemaleExtendedDisplaySlot(
                ModEntry.ModName, "DepravedShield",
                () => Strings.UI_DepravedShield,    // ★ 使用本地化
                out var typeValueShield);

            if (typeValueShield != null)
            {
                var refComp = typeValueShield.AddComponent<ReferenceDepravedShield>();
                var binder = typeValueShield.GetComponent<BinderTextMeshProText>();
                if (binder == null)
                    binder = typeValueShield.AddComponent<BinderTextMeshProText>();
                binder.SetReferenceArray(new List<Reference> { refComp });
            }

            // 获取 UpdaterExtendedAreaManager 引用（用于强制刷新）
            if (_slotOrgasm != null)
            {
                var content = _slotOrgasm.transform.parent;
                if (content != null)
                {
                    var extendedArea = content.parent;
                    if (extendedArea != null)
                    {
                        _updaterManager = extendedArea.GetComponent<UpdaterExtendedAreaManager>();
                    }
                }
            }

            // 订阅两个事件
            SexProgressManager.OnOrgasmCountChanged += RefreshUIIfCurrent;
            SexProgressManager.OnDepravedShieldChanged += RefreshUIIfCurrent;

            // ★ 将插槽移动到最底部（在所有模组加载完成后执行）
            if (Loader.AllModsLoaded)
            {
                ModCoroutineHelper.StartCoroutine(DelayedMoveToBottom());
            }
            else
            {
                Loader.OnAllModsLoaded += () =>
                {
                    ModCoroutineHelper.StartCoroutine(DelayedMoveToBottom());
                };
            }

            _uiInitialized = true;
            ModEntry.Log("UI slots initialized (OrgasmCount & DepravedShield).");
        }

        /// <summary> 延迟一帧后移动插槽到底部，并保持正确顺序 </summary>
        private static IEnumerator DelayedMoveToBottom()
        {
            yield return null; // 等待一帧，确保所有插槽都已创建
            if (_slotOrgasm != null && _slotShield != null && _slotOrgasm.transform.parent != null)
            {
                // 获取父对象下的子对象数量
                Transform parent = _slotOrgasm.transform.parent;
                int maxIndex = parent.childCount - 1;

                // 将 Shield 放到最底部，Orgasm 放在 Shield 上方一位（即倒数第二）
                _slotShield.transform.SetSiblingIndex(maxIndex);
                _slotOrgasm.transform.SetSiblingIndex(maxIndex - 1);

                ModEntry.Log("Moved OrgasmCount and DepravedShield slots to bottom (Orgasm above Shield).");
            }
        }

        /// <summary> 刷新 UI 插槽（如果当前显示的角色与传入角色相同）</summary>
        private static void RefreshUIIfCurrent(Character character)
        {
            if (!_uiInitialized || _updaterManager == null || character == null)
                return;

            var current = GetCurrentDisplayedUnit();
            if (current != character) return;

            // 强制调用 Updater 的 Display 方法
            var method = typeof(UpdaterExtendedAreaManager).GetMethod("Display",
                BindingFlags.NonPublic | BindingFlags.Instance);
            method?.Invoke(_updaterManager, null);
        }

        private static Character GetCurrentDisplayedUnit()
        {
            var window = GameObject.Find("Window Female Information (Window)");
            if (window == null) return null;
            var targetUnit = window.GetComponentInChildren<TargetUnit>();
            return targetUnit?.Unit as Character;
        }

        // ===== Reference 组件：高潮次数 =====
        public class ReferenceOrgasmCount : Reference
        {
            public override void Initialize()
            {
                ReferenceType = EReferenceType.String;
            }

            public override string GetString()
            {
                var targetUnit = base.Updater?.TargetUnit;
                var character = targetUnit?.Unit as Character;
                if (character == null) return "0";

                int count = SexProgressManager.SexPeriodicDamageHelper.GetOrgasmCount(character);
                return count.ToString();
            }
        }

        // ===== Reference 组件：防护结界（DepravedShield） =====
        public class ReferenceDepravedShield : Reference
        {
            public override void Initialize()
            {
                ReferenceType = EReferenceType.String;
            }

            public override string GetString()
            {
                var targetUnit = base.Updater?.TargetUnit;
                var character = targetUnit?.Unit as Character;
                if (character == null) return "0";

                int shield = SexProgressManager.GetDepravedShield(character);
                return shield.ToString();
            }
        }
    }
}