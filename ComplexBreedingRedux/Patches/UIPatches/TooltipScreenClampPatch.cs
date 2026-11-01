#nullable enable
using System.Collections.Generic;
using HarmonyLib;
using MBMScripts;
using UnityEngine;

namespace ComplexBreedingRedux.Patches.UIPatches;

// Если tooltip-панель уходит за нижний край экрана — поднимаем её вверх.
// Патчим оба класса: InteractionToolTipPrefab и InteractionToolTipPrefabString.

[HarmonyPatch(typeof(InteractionToolTipPrefab), "OnEnter")]
internal static class TooltipPrefabScreenClampPatch
{
    public static void Postfix(List<PooledObject> ___m_OverPooledObjectList)
    {
        TooltipScreenClamp.AttachClampHelpers(___m_OverPooledObjectList);
    }
}

[HarmonyPatch(typeof(InteractionToolTipPrefabString), "OnEnter")]
internal static class TooltipPrefabStringScreenClampPatch
{
    public static void Postfix(List<PooledObject> ___m_OverPooledObjectList)
    {
        TooltipScreenClamp.AttachClampHelpers(___m_OverPooledObjectList);
    }
}

internal static class TooltipScreenClamp
{
    internal static void AttachClampHelpers(List<PooledObject> list)
    {
        foreach (PooledObject po in list)
        {
            TooltipClampHelper helper = po.GetComponent<TooltipClampHelper>();
            if (helper == null)
                helper = po.gameObject.AddComponent<TooltipClampHelper>();
            helper.enabled = true;
        }
    }
}

/// <summary>
/// Компонент, добавляемый на каждую tooltip-панель.
/// В LateUpdate находит самую нижнюю точку ВСЕХ дочерних RectTransform
/// (текст может переполнять фиксированный корень) и сдвигает корень вверх.
/// </summary>
[DefaultExecutionOrder(30000)]
internal sealed class TooltipClampHelper : MonoBehaviour
{
    private const float Margin = 4f;
    private const int MaxFrames = 5;
    private int _frame;

    private void OnEnable()
    {
        _frame = 0;
    }

    private void LateUpdate()
    {
        if (++_frame > MaxFrames)
        {
            enabled = false;
            return;
        }

        Camera? cam = GameManager.Instance?.MainCamera;
        if (cam == null) return;

        RectTransform? rt = transform as RectTransform;
        if (rt == null) return;

        // Ищем самую нижнюю точку всей иерархии (дети могут выходить за корень)
        float minScreenY = float.MaxValue;
        Vector3[] corners = new Vector3[4];
        RectTransform[] allRts = GetComponentsInChildren<RectTransform>();
        foreach (RectTransform crt in allRts)
        {
            crt.GetWorldCorners(corners);
            float bot = cam.WorldToScreenPoint(corners[0]).y;
            if (bot < minScreenY) minScreenY = bot;
        }

        float overshoot = Margin - minScreenY;
        if (overshoot <= 0f) return;

        // Сдвигаем корень вверх — все дети двигаются вместе с ним
        Vector3 pivotScreen = cam.WorldToScreenPoint(rt.position);
        pivotScreen.y += overshoot;
        rt.position = cam.ScreenToWorldPoint(pivotScreen);
    }
}
