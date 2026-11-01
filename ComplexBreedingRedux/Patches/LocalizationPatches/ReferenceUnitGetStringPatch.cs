#nullable enable
using ComplexBreedingRedux.Features;
using HarmonyLib;
using MBMScripts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ComplexBreedingRedux.Patches.LocalizationPatches;

[HarmonyPatch(typeof(ReferenceUnit), "GetString")]
internal static class ReferenceUnitGetStringPatch
{
    /// <summary>Запас в пикселях — отступы текста от краёв Vertical.</summary>
    private const float Padding = 8f;

    public static void Postfix(ReferenceUnit __instance, int ___m_UnitDataType, ref string __result)
    {
        Character? character = __instance.Updater.TargetUnit?.Unit as Character;
        if (character == null) return;
        LocalizationSystem.ApplyUnitString(character, ___m_UnitDataType, ref __result);

        if (___m_UnitDataType == 27)
            WidenTooltipToFitText(__instance.transform, __result);
    }

    /// <summary>
    /// Рассчитывает необходимую ширину по тексту через TMP_Text.GetPreferredValues()
    /// и расширяет Vertical + Horizontal если текущая ширина меньше нужной.
    /// </summary>
    private static void WidenTooltipToFitText(Transform textTransform, string text)
    {
        TMP_Text? tmp = textTransform.GetComponent<TMP_Text>();
        if (tmp == null) return;

        RectTransform? vrt = textTransform.parent as RectTransform;
        if (vrt == null || vrt.name != "Vertical") return;

        // GetPreferredValues(text, 0, 0) возвращает (width, height) без ограничений —
        // width = ширина самой длинной строки (с учётом rich-text, <BR>, размера шрифта).
        Vector2 pref = tmp.GetPreferredValues(text, 0f, 0f);
        float neededWidth = pref.x + Padding;

        if (vrt.sizeDelta.x >= neededWidth) return;

        float delta = neededWidth - vrt.sizeDelta.x;
        vrt.sizeDelta = new Vector2(neededWidth, vrt.sizeDelta.y);

        RectTransform? hrt = vrt.parent as RectTransform;
        if (hrt != null)
        {
            ContentSizeFitter? csf = hrt.GetComponent<ContentSizeFitter>();
            if (csf != null)
                csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            hrt.sizeDelta = new Vector2(hrt.sizeDelta.x + delta, hrt.sizeDelta.y);
        }
    }
}
