using HarmonyLib;
using MBMScripts;
using System.Collections.Generic;

namespace DepravedShield.Patches
{
    [HarmonyPatch(typeof(Female), "UpdateMentalityVariation")]
    public static class Female_UpdateMentalityVariation_Patch
    {
        // ★ 将特殊NPC判断逻辑复制过来（或引用SexProgressManager中的方法）
        private static readonly HashSet<EUnitType> SpecialNpcTypes = new HashSet<EUnitType>
        {
            EUnitType.Amilia2,
            EUnitType.Flora2,
            EUnitType.Niel2,
            EUnitType.Sena2,
            EUnitType.Lena2,
            EUnitType.Barbara2
        };

        private static bool IsSpecialNpc(Character character)
        {
            return character != null && SpecialNpcTypes.Contains(character.UnitType);
        }

        static void Prefix(Female __instance, ref float __state)
        {
            __state = __instance.MentalityVariation;
        }

        static void Postfix(Female __instance, float __state)
        {
            if (!ModConfig.EnableMentalityHalf) return;
            if (!__instance.IsDepraved || __state >= 0f) return;

            // ★ 若为特殊NPC，则不扣血
            if (IsSpecialNpc(__instance))
                return;

            float damage = -__state;
            __instance.Health -= damage * 0.5f;
            if (__instance.Health < 0f)
                __instance.Health = 0f;
        }
    }
}