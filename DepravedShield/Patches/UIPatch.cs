using DepravedShield.Helpers;
using HarmonyLib;
using MBMScripts;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace DepravedShield.Patches
{
    [HarmonyPatch(typeof(Character), "TrainingPercent", MethodType.Getter)]
    internal static class Patch_TrainingPercent
    {
        static void Postfix(Character __instance, ref float __result)
        {
            if (!ModConfig.Enable || !ModConfig.ShowUI) return;

            if (__instance.State == EState.Sex || __instance.State == EState.SexDrained
                || __instance.State == EState.CumShotInside || __instance.State == EState.CumShotOutside)
            {
                float progress = SexProgressManager.GetProgress(__instance);
                __result = progress;
                var binding = SeqDataBinding.Instance;
                if (binding != null)
                {
                    binding.DirtyFlag(__instance, "Training");
                    binding.DirtyFlag(__instance, "TrainingPercent");
                }
            }
            else
            {
                __result = 0f;
            }
        }
    }

    [HarmonyPatch(typeof(StatePlate), "LateUpdate")]
    internal static class Patch_StatePlate_LateUpdate
    {
        static void Postfix(StatePlate __instance)
        {
            if (!ModConfig.Enable || !ModConfig.ShowUI) return;

            var bone = Traverse.Create(__instance).Field("BoneCleanlinessGauge").GetValue<Bone>();
            if (bone == null) return;

            float scale = Traverse.Create(__instance).Property("SkeletonDataScale").GetValue<float>();
            var unit = Traverse.Create(__instance).Field("m_Unit").GetValue<Unit>();
            if (unit == null) return;

            Character character = unit as Character;
            if (character == null) return;

            if (character.State == EState.Sex || character.State == EState.SexDrained
                || character.State == EState.CumShotInside || character.State == EState.CumShotOutside)
            {
                float progress = SexProgressManager.GetProgress(character);
                bone.Y = Mathf.Lerp(-112f * scale, 0f, progress);
            }
            else
            {
                bone.Y = -112f * scale;
            }
        }
    }

    [HarmonyPatch(typeof(ReferenceCharacterStatePlate), "GetFloat")]
    internal static class Patch_GetFloat
    {
        static void Postfix(ReferenceCharacterStatePlate __instance, ref float __result)
        {
            if (!ModConfig.Enable || !ModConfig.ShowUI) return;

            int dataType = Traverse.Create(__instance).Field("m_DataType").GetValue<int>();
            const int TrainingPercentEnum = 2;
            if (dataType == TrainingPercentEnum)
            {
                var unit = Traverse.Create(__instance).Field("m_Unit").GetValue<Unit>();
                if (unit is Character character &&
                    (character.State == EState.Sex || character.State == EState.SexDrained
                     || character.State == EState.CumShotInside || character.State == EState.CumShotOutside))
                {
                    float progress = SexProgressManager.GetProgress(character);
                    __result = Mathf.Lerp(0f, 0.32f, progress);
                }
                else
                {
                    __result = 0f;
                }
            }
        }
    }
}