#nullable enable
using DarkTonic.MasterAudio;
using HarmonyLib;
using MBMScripts;

namespace RaceBreedingExtension.VoiceReplay.Patches
{
    /// <summary>
    /// 重制语音（VoiceReplay）Harmony 补丁。
    /// 采用与原“全屏蔽语音”mod 相同的补丁结构（[HarmonyPatch(typeof(GameManager))] + 方法级补丁），
    /// 但将“屏蔽（mute）”改为“完整复刻原版语音播放（replay）”：
    ///   - GameManager.PlayVoiceSound       → 复刻原版日常语音（含种族 / 特殊角色 / VoiceType A-G）
    ///   - GameManager.PlayVoiceSoundInSex  → 复刻原版性爱语音，并在
    ///     EState.Sex/SexDrained/CumShotInside/CumShotOutside 场景下改为根据
    ///     DepravedShield（堕落护盾）进度条播放（0-90% 循环 Sex/SexDrained，100% 高潮/中出语音）
    ///   - GameManager.PlayTouchSound       → 复刻原版触摸语音（特殊角色）
    /// 补丁由 ModLoader 在加载本程序集后自动 PatchAll，无需手动应用。
    /// 注意：请勿与本工程之外的“全屏蔽语音”mod 同时启用，二者会冲突。
    /// </summary>
    [HarmonyPatch(typeof(GameManager))]
    internal static class VoiceReplayPatches
    {
        // ==================== 日常语音 ====================
        [HarmonyPrefix]
        [HarmonyPatch(nameof(GameManager.PlayVoiceSound), typeof(Character))]
        private static bool Prefix_PlayVoiceSound(GameManager __instance, Character character, ref PlaySoundResult __result)
        {
            if (!VoiceReplayConfig.Enable) return true;

            try
            {
                __result = VoiceReplaySystem.PlayDailyVoice(character);
                return false; // 跳过原版，使用复刻的原版语音列表
            }
            catch (System.Exception e)
            {
                VoiceReplaySystem.LogError("PlayVoiceSound patch failed, fallback to vanilla: " + e);
                return true;
            }
        }

        // ==================== 性爱语音（进度条驱动） ====================
        [HarmonyPrefix]
        [HarmonyPatch(nameof(GameManager.PlayVoiceSoundInSex), typeof(Character))]
        private static bool Prefix_PlayVoiceSoundInSex(GameManager __instance, Character character, ref PlaySoundResult __result)
        {
            if (!VoiceReplayConfig.Enable) return true;

            try
            {
                __result = VoiceReplaySystem.PlaySexVoice(character);
                return false; // 跳过原版，使用复刻的性爱语音 / 进度条语音
            }
            catch (System.Exception e)
            {
                VoiceReplaySystem.LogError("PlayVoiceSoundInSex patch failed, fallback to vanilla: " + e);
                return true;
            }
        }

        // ==================== 触摸语音 ====================
        [HarmonyPrefix]
        [HarmonyPatch(nameof(GameManager.PlayTouchSound), typeof(Character))]
        private static bool Prefix_PlayTouchSound(GameManager __instance, Character character, ref PlaySoundResult __result)
        {
            if (!VoiceReplayConfig.Enable) return true;

            try
            {
                __result = VoiceReplaySystem.PlayTouchVoice(character);
                return false; // 跳过原版，使用复刻的触摸语音
            }
            catch (System.Exception e)
            {
                VoiceReplaySystem.LogError("PlayTouchSound patch failed, fallback to vanilla: " + e);
                return true;
            }
        }
    }
}
