#nullable enable
using MBM.ModLoader.Settings;
using RaceBreedingExtension;

namespace RaceBreedingExtension.VoiceReplay
{
    /// <summary>
    /// 重制语音（VoiceReplay）子系统配置。
    /// 音频相关的模组菜单开关精简为两项：
    ///   - VoiceReplay_Enable（启用重制语音）
    ///   - VoiceReplay_EnableLogging（启用重制语音调试日志）
    /// 其余音频行为（日常/性爱/触摸语音复刻、VoiceType A-G 切换跟随、
    /// 根据堕落护盾进度条播放性爱语音等）均为常驻功能，不再提供菜单开关。
    /// 设置键以 "VoiceReplay_" 前缀命名，避免与共用 ModName（RaceBreedingExtension）
    /// 的 DepravedShield（"Enable"/"EnableLogging" 等）与 TitsModCompat（"TitsModCompat_"）
    /// 键冲突。
    /// </summary>
    public static class VoiceReplayConfig
    {
        private const string KEY_ENABLE = "VoiceReplay_Enable";
        private const string KEY_ENABLE_LOGGING = "VoiceReplay_EnableLogging";

        // ---- 常驻功能（不再提供菜单开关，始终保持开启，行为与原版本一致）----

        /// <summary>复刻日常语音 GameManager.PlayVoiceSound（含不同种族、怪物与特殊角色音效）。</summary>
        public const bool PlayDailyVoice = true;

        /// <summary>复刻性爱语音 GameManager.PlayVoiceSoundInSex（含进度条驱动）。</summary>
        public const bool PlaySexVoice = true;

        /// <summary>复刻触摸语音 GameManager.PlayTouchSound（特殊角色音效）。</summary>
        public const bool PlayTouchVoice = true;

        /// <summary>在 EState.Sex/SexDrained/CumShotInside/CumShotOutside 场景下根据堕落护盾进度条播放语音。</summary>
        public const bool UseProgressForSexVoice = true;

        /// <summary>0-90% 区间内，进度达到该值（0-1，默认 0.45）时从普通 Sex 语音切换为 SexDrained 语音。</summary>
        public const float SexDrainedThreshold = 0.45f;

        public static bool Enable { get; private set; }
        public static bool EnableLogging { get; private set; }

        /// <summary>由 VoiceReplaySystem.Initialize() 调用一次（调用前需已设置 Properties.Strings.Culture）。</summary>
        public static void Register()
        {
            RegisterBool(KEY_ENABLE, true, Properties.Strings.Config_Enable);
            RegisterBool(KEY_ENABLE_LOGGING, false, Properties.Strings.Config_EnableLogging);

            RefreshValues();
        }

        private static void RegisterBool(string key, bool defaultValue, string desc)
        {
            ModSettings.RegisterBool(ModEntry.ModName, key, defaultValue, desc);
            ModSettings.OnChanged(ModEntry.ModName, key, _ => RefreshValues());
        }

        /// <summary>语言切换时刷新模组菜单描述（由 VoiceReplaySystem.OnLanguageChanged 调用）。</summary>
        public static void OnLanguageChanged()
        {
            ModSettings.SetDescription(ModEntry.ModName, KEY_ENABLE, Properties.Strings.Config_Enable);
            ModSettings.SetDescription(ModEntry.ModName, KEY_ENABLE_LOGGING, Properties.Strings.Config_EnableLogging);
        }

        private static void RefreshValues()
        {
            Enable = ModSettings.GetBool(ModEntry.ModName, KEY_ENABLE);
            EnableLogging = ModSettings.GetBool(ModEntry.ModName, KEY_ENABLE_LOGGING);
        }
    }
}

