#nullable enable
using System;
using HarmonyLib;
using MBMScripts;
using RaceBreedingExtension.UniqueLookColor.Extra;
using RaceBreedingExtension.UniqueLookColor.UI;
using Spine;

namespace RaceBreedingExtension.UniqueLookColor.Patches
{
    /// <summary>
    /// 渲染补丁：UpdaterSpineCharacter.OnChangeSpine 完成后，把可换色独特角色
    /// （如 GolemRig 换皮物种）的 HairColor / EyeColor（及可选服色）施加到其独特骨架对应部件。
    /// 在游戏自身槽位染色之后执行（多个 Postfix 顺序无依赖）。
    /// </summary>
    [HarmonyPatch(typeof(UpdaterSpineCharacter), "OnChangeSpine")]
    internal static class UniqueLookRenderPatch
    {
        private static readonly AccessTools.FieldRef<UpdaterSpineCharacter, Skeleton> SkeletonRef =
            AccessTools.FieldRefAccess<UpdaterSpineCharacter, Skeleton>("m_Skeleton");

        public static void Postfix(UpdaterSpineCharacter __instance)
        {
            try
            {
                Character? female = __instance.Female;
                if (female == null)
                    return;
                if (!UniqueLookColorSystem.IsEligible(female))
                    return;

                Skeleton? skeleton = SkeletonRef(__instance);
                if (skeleton == null)
                    return;

                UniqueLookColorSystem.ApplyLookColors(female, skeleton);
            }
            catch (Exception e)
            {
                UniqueLookColorSystem.LogWarning("Render apply failed: " + e.Message);
            }
        }
    }

    /// <summary>
    /// 生命周期补丁：游戏初始化完成后注入「人物信息窗口」的换色 UI
    /// （与 MoreGraphicAttachments 的 SeqObjectPoolManagerPatch.AfterGameInitialized 语义一致）。
    /// </summary>
    [HarmonyPatch(typeof(SeqObjectPoolManager), nameof(SeqObjectPoolManager.Initialize))]
    internal static class UniqueLookGameInitPatch
    {
        public static void Postfix()
        {
            try
            {
                UniqueLookUIRegister.Initialize();
            }
            catch (Exception e)
            {
                UniqueLookColorSystem.LogError("Game-init UI register failed: " + e);
            }
        }
    }

    /// <summary>
    /// 生命周期补丁：PlayData.OnEnable（读档数据就绪）时套用服色存档并关闭残留取色面板。
    /// 顺序：ModSaveData.OnAfterLoad 已把服色读入待应用表，这里再写入角色扩展字段。
    /// </summary>
    [HarmonyPatch(typeof(PlayData), nameof(PlayData.OnEnable))]
    internal static class UniqueLookPlayDataPatch
    {
        public static event Action? AfterSaveDataInitialized;

        public static void Postfix()
        {
            try
            {
                UniqueLookSaveData.ApplyAll();
                AfterSaveDataInitialized?.Invoke();
            }
            catch (Exception e)
            {
                UniqueLookColorSystem.LogError("AfterSaveDataInitialized handlers failed: " + e);
            }
        }
    }
}
