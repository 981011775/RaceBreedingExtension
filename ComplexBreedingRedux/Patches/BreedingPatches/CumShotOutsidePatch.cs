#nullable enable
using System;
using ComplexBreedingRedux.Features;
using HarmonyLib;
using MBMScripts;

namespace ComplexBreedingRedux.Patches.BreedingPatches;

/// <summary>
/// Ловит момент завершения наружного семяизвержения (×0.3)
/// и семяизвержения с презервативом (×0.7) для начисления прогресса растяжения матки.
/// </summary>
[HarmonyPatch(typeof(Male), "CumShot")]
internal static class CumShotOutsidePatch
{
    [ThreadStatic] private static int _prevOutsideCount;
    [ThreadStatic] private static EState _prevState;
    [ThreadStatic] private static bool _canImpregnate;

    public static void Prefix(Character __instance)
    {
        Character? partner = __instance.Partner;
        _prevOutsideCount = partner?.CumshotOutsideCount ?? 0;
        _prevState = __instance.State;
        _canImpregnate = __instance.SpineData?.ImpregnationTime > 0f;
    }

    public static void Postfix(Character __instance)
    {
        Character? partner = __instance.Partner;
        if (!(partner is Female female)) return;

        // Outside cumshot (без презерватива) — CumshotOutsideCount увеличился
        if (female.CumshotOutsideCount > _prevOutsideCount)
        {
            if (!_canImpregnate) return; // оральный секс — растяжения нет
            WombSystem.AddStretchProgress(female, __instance, isInside: false, 0);
            return;
        }

        // Condom cumshot — состояние перешло из CumShot* в Idle при наличии презерватива
        if ((_prevState == EState.CumShotInside || _prevState == EState.CumShotOutside)
            && __instance.State == EState.Idle
            && __instance.BuffListContains(EBuffType.Condom))
        {
            if (!_canImpregnate) return; // оральный секс — растяжения нет
            bool isInside = _prevState == EState.CumShotInside;
            WombSystem.AddStretchProgress(female, __instance, isInside: isInside, 0, withCondom: true);
        }
    }
}
