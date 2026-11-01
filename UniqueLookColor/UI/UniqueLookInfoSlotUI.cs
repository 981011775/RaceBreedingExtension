#nullable enable
using System;
using System.Collections.Generic;
using MBMScripts;
using SystemExtensionLib.Systems;
using SystemExtensionLib.Tools;
using SystemExtensionLib.Utils;
using UnityEngine;
using UnityEngine.UI;
using RaceBreedingExtension.UniqueLookColor.UIComponents;

namespace RaceBreedingExtension.UniqueLookColor.UI
{
    /// <summary>
    /// 女性信息窗口「扩展区域」的独特发色 / 瞳色 / 服色槽。
    /// 使用 SystemExtensionLib.ExtendedInfoSlotSystem 在信息窗口右侧扩展区注册三个颜色显示槽
    /// （仅当目标为可换色的独特角色时可见），点击按钮弹出取色面板修改
    /// HairColor / EyeColor（游戏字段）/ 服色（模组扩展字段）。
    /// 结构参照 MoreGraphicAttachments.UI.ClothesColorSlotUI。
    /// 注意：不再向画廊（Galley）注入任何 UI。
    /// </summary>
    internal static class UniqueLookInfoSlotUI
    {
        private static readonly List<GameObject> _openPanels = new();

        private static bool _isInjected;
        private static bool _isGlobalEventRegistered;

        public static void InjectSlots()
        {
            if (_isInjected)
                return;
            _isInjected = true;

            InjectOne(UniqueLookPart.Hair, "Unique Hair Color", () => UlcLoc.Slot_Hair);
            InjectOne(UniqueLookPart.Eye, "Unique Eye Color", () => UlcLoc.Slot_Eye);
            // 服色槽：雪狼人 / 魔兔人没有角颜色框，这两个种族该槽改为控制尾巴 / 兽耳毛色，
            // 因此挂上动态标签（普通角色仍显示"独特服色"）。
            InjectOne(UniqueLookPart.Clothes, "Unique Clothes Color", () => UlcLoc.Slot_Clothes, dynamicFurLabel: true);

            if (!_isGlobalEventRegistered)
            {
                _isGlobalEventRegistered = true;
                Patches.UniqueLookPlayDataPatch.AfterSaveDataInitialized += CloseAllPanels;
            }
        }

        private static void InjectOne(UniqueLookPart part, string slotName, Func<string> getLabel, bool dynamicFurLabel = false)
        {
            try
            {
                // 1) 注册颜色显示槽（克隆原版 "Hair Color" 行）
                GameObject slot = ExtendedInfoSlotSystem.RegisterFemaleExtendedColorSlot(
                    ModEntry.ModName, slotName, getLabel, out GameObject? typeValue);

                // 2) typeValue：绑定 颜色(染字) + 颜色名文本 两组 Reference
                if (typeValue != null)
                {
                    var refColor = typeValue.AddComponent<ReferenceUniqueLook>();
                    refColor.Part = part;
                    refColor.AsColorText = false;
                    var binder = typeValue.GetComponent<BinderTextMeshPro>();
                    if (binder != null)
                        binder.SetReferenceArray(new List<Reference> { refColor });

                    var refText = typeValue.AddComponent<ReferenceUniqueLook>();
                    refText.Part = part;
                    refText.AsColorText = true;
                    var binderText = typeValue.GetComponent<BinderTextMeshProText>();
                    if (binderText != null)
                        binderText.SetReferenceArray(new List<Reference> { refText });
                }

                // 3) 弹出取色面板 + 打开按钮
                GameObject panel = BuildPanel(slot, part, getLabel);
                BuildButton(slot, panel);
                _openPanels.Add(panel);

                // 4) 可见性：仅对可换色的独特角色显示本行
                ExtendedInfoSlotSystem.RegisterFemaleExtendedSlotVisibilityCondition(
                    ModEntry.ModName, slotName,
                    c => UniqueLookColorSystem.IsEligible(c));

                // 5) 服色槽的动态标签（兽系 → "独特毛色"）
                if (dynamicFurLabel)
                {
                    ReferenceString? labelRs = slot.GetComponentInChildren<ReferenceString>(true);
                    if (labelRs != null)
                    {
                        var dynamicLabel = labelRs.gameObject.AddComponent<UniqueLookDynamicLabel>();
                        dynamicLabel.ResolveText = ResolveClothesOrFurLabel;
                    }
                    else
                    {
                        UniqueLookColorSystem.LogWarning($"'{slotName}': label ReferenceString not found; dynamic label skipped.");
                    }
                }

                UniqueLookColorSystem.Log($"Info extended slot '{slotName}' injected.");
            }
            catch (Exception e)
            {
                UniqueLookColorSystem.LogError($"Info slot '{slotName}' inject failed: " + e);
            }
        }
        /// <summary>
        /// 服色槽标签：默认"独特服色"；雪狼人 / 魔兔人（<see cref="UniqueLookDefs.BeastTailLook"/>）显示"独特毛色"。
        /// </summary>
        private static string ResolveClothesOrFurLabel(Character? character)
        {
            if (character != null
                && UniqueLookColorSystem.TryGetLookData(character, out UniqueLookDef? def, out _)
                && ReferenceEquals(def, UniqueLookDefs.BeastTailLook))
            {
                return UlcLoc.Slot_Fur;
            }
            return UlcLoc.Slot_Clothes;
        }

        private static GameObject BuildPanel(GameObject slot, UniqueLookPart part, Func<string> getLabel)
        {
            GameObject panel = UIExtraction.ExtractionPanelWindow(
                out GameObject body,
                out TMPro.TextMeshProUGUI tmp,
                out ReferenceFormattingText labelRft,
                out GameObject exitIcon);

            panel.transform.SetParent(slot.transform, false);
            panel.name = part switch
            {
                UniqueLookPart.Eye => "UniqueEyeColorPanel",
                UniqueLookPart.Clothes => "UniqueClothesColorPanel",
                _ => "UniqueHairColorPanel",
            };

            var panelRT = panel.GetComponent<RectTransform>();
            panelRT.pivot = new Vector2(1f, 0.5f);
            panelRT.anchorMin = new Vector2(0f, 1f);
            panelRT.anchorMax = new Vector2(0f, 1f);
            panelRT.anchoredPosition = new Vector2(-13f, 0f);
            panelRT.sizeDelta = new Vector2(300f, 250f);

            // 取色器
            GameObject picker = GalleryExtraction.OnlyColorPicker(out FlexibleColorPicker fcp);
            picker.transform.SetParent(body.transform, false);
            picker.name = "UniqueLookColorPicker";

            var refColor = picker.AddComponent<ReferenceUniqueLook>();
            refColor.Part = part;
            refColor.AsColorText = false;
            var updater = picker.AddComponent<UpdaterUniqueLookColorPicker>();
            updater.SetReferenceArray(new List<Reference> { refColor });

            var interaction = picker.AddComponent<InteractionUniqueLook>();
            interaction.Part = part;
            fcp.onColorChange.AddListener(interaction.ChangeColor);

            picker.SetActive(true);

            labelRft.SetLabel(getLabel);
            ComponentTools.AddClickEvent(exitIcon, () => panel.SetActive(false));

            panel.SetActive(false);
            return panel;
        }

        private static void BuildButton(GameObject slot, GameObject targetPanel)
        {
            GameObject button = UIExtraction.ExtractionButton(out Image image);
            button.transform.SetParent(slot.transform, false);
            button.name = "UniqueLookColorButton";

            var rt = button.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(60f, -10f);

            ComponentTools.AddClickEvent(button, () => targetPanel.SetActive(!targetPanel.activeSelf));
            button.SetActive(true);
        }

        /// <summary>存档数据初始化/读取时关闭所有打开的取色面板（防面板残留）。</summary>
        public static void CloseAllPanels()
        {
            foreach (GameObject panel in _openPanels)
            {
                if (panel != null)
                    panel.SetActive(false);
            }
        }
    }
}
