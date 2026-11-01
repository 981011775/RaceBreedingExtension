#nullable enable
using MBM.ModLoader.Settings;

namespace RaceBreedingExtension.UniqueLookColor
{
    /// <summary>
    /// 独特角色换色（Unique Look Color）子系统配置。
    /// 设置键统一以 "UniqueLook_" 前缀命名，避免与共用 ModName（RaceBreedingExtension）的
    /// 其它子系统（DepravedShield / VoiceReplay_ / TitsModCompat_ / HunterCamp）冲突。
    /// </summary>
    internal static class UniqueLookColorConfig
    {
        private const string KEY_ENABLE = "UniqueLook_Enable";
        private const string KEY_RECOLOR_VANILLA_FIGURE = "UniqueLook_RecolorVanillaFigure";
        private const string KEY_ENABLE_LOGGING = "UniqueLook_EnableLogging";

        /// <summary>主开关：是否在渲染层对独特 Figure / 换皮 Rig 角色应用头发、眼睛染色。</summary>
        public static bool Enable { get; private set; }

        /// <summary>
        /// 是否允许修改「游戏原版独特角色」的头发 / 眼睛颜色（无物种换皮前缀、使用原版独特 Figure 的角色）。
        /// 默认关闭 —— 避免误染原版 NPC（Aure、Bella、Claire…）。仅影响渲染层判定，不改变原版数据。
        /// </summary>
        public static bool RecolorVanillaFigure { get; private set; }

        public static bool EnableLogging { get; private set; }

        public static void Register()
        {
            RegisterBool(KEY_ENABLE, true, UlcLoc.Config_Enable);
            RegisterBool(KEY_RECOLOR_VANILLA_FIGURE, false, UlcLoc.Config_RecolorVanillaFigure);
            RegisterBool(KEY_ENABLE_LOGGING, false, UlcLoc.Config_EnableLogging);
            RefreshValues();
        }

        public static void OnLanguageChanged()
        {
            ModSettings.SetDescription(ModEntry.ModName, KEY_ENABLE, UlcLoc.Config_Enable);
            ModSettings.SetDescription(ModEntry.ModName, KEY_RECOLOR_VANILLA_FIGURE, UlcLoc.Config_RecolorVanillaFigure);
            ModSettings.SetDescription(ModEntry.ModName, KEY_ENABLE_LOGGING, UlcLoc.Config_EnableLogging);
        }

        private static void RegisterBool(string key, bool defaultValue, string desc)
        {
            ModSettings.RegisterBool(ModEntry.ModName, key, defaultValue, desc);
            ModSettings.OnChanged(ModEntry.ModName, key, _ => RefreshValues());
        }

        private static void RefreshValues()
        {
            Enable = ModSettings.GetBool(ModEntry.ModName, KEY_ENABLE);
            RecolorVanillaFigure = ModSettings.GetBool(ModEntry.ModName, KEY_RECOLOR_VANILLA_FIGURE);
            EnableLogging = ModSettings.GetBool(ModEntry.ModName, KEY_ENABLE_LOGGING);
        }
    }
}
