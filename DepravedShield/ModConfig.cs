using DepravedShield.Helpers;
using MBM.ModLoader.Settings;
using DepravedShield.Properties;  // ★ 添加资源引用

namespace DepravedShield
{
    public static class ModConfig
    {
        private const string KEY_ENABLE = "Enable";
        private const string KEY_ENABLE_LOGGING = "EnableLogging";
        private const string KEY_SHOW_UI = "ShowUI";
        private const string KEY_INCREASE_DURATION = "IncreaseDuration";
        private const string KEY_TOTAL_PERCENT = "TotalPercent";
        private const string KEY_ENABLE_MENTALITY_HALF = "EnableMentalityHalf";
        public static bool Enable { get; private set; }
        public static bool EnableLogging { get; private set; }
        public static bool ShowUI { get; private set; }
        public static float IncreaseDuration { get; private set; }
        public static float TotalPercent { get; private set; }

        public static bool EnableMentalityHalf { get; private set; }

        public static void Register()
        {
            // ★ 使用 Strings 资源类获取描述
            RegisterBool(KEY_ENABLE, true, Strings.Config_Enable);
            RegisterBool(KEY_ENABLE_LOGGING, false, Strings.Config_EnableLogging);
            RegisterBool(KEY_SHOW_UI, true, Strings.Config_ShowUI);
            RegisterFloat(KEY_INCREASE_DURATION, 5f, Strings.Config_IncreaseDuration);
            RegisterFloat(KEY_TOTAL_PERCENT, 15f, Strings.Config_TotalPercent);
            RegisterBool(KEY_ENABLE_MENTALITY_HALF, true, Strings.Config_EnableMentalityHalf);

            RefreshValues();
        }

        private static void RegisterBool(string key, bool defaultValue, string desc)
        {
            ModSettings.RegisterBool(ModEntry.ModName, key, defaultValue, desc);
            ModSettings.OnChanged(ModEntry.ModName, key, _ => RefreshValues());
        }

        private static void RegisterFloat(string key, float defaultValue, string desc)
        {
            ModSettings.RegisterFloat(ModEntry.ModName, key, defaultValue, desc);
            ModSettings.OnChanged(ModEntry.ModName, key, _ => RefreshValues());
        }

        private static void RefreshValues()
        {
            Enable = ModSettings.GetBool(ModEntry.ModName, KEY_ENABLE);
            EnableLogging = ModSettings.GetBool(ModEntry.ModName, KEY_ENABLE_LOGGING);
            ShowUI = ModSettings.GetBool(ModEntry.ModName, KEY_SHOW_UI);
            IncreaseDuration = ModSettings.GetFloat(ModEntry.ModName, KEY_INCREASE_DURATION);
            TotalPercent = ModSettings.GetFloat(ModEntry.ModName, KEY_TOTAL_PERCENT);
            EnableMentalityHalf = ModSettings.GetBool(ModEntry.ModName, KEY_ENABLE_MENTALITY_HALF);
        }

        // ★ 此方法由 ModEntry.OnLanguageChanged 调用，用于刷新配置描述
        public static void OnLanguageChanged()
        {
            ModSettings.SetDescription(ModEntry.ModName, KEY_ENABLE, Strings.Config_Enable);
            ModSettings.SetDescription(ModEntry.ModName, KEY_ENABLE_LOGGING, Strings.Config_EnableLogging);
            ModSettings.SetDescription(ModEntry.ModName, KEY_SHOW_UI, Strings.Config_ShowUI);
            ModSettings.SetDescription(ModEntry.ModName, KEY_INCREASE_DURATION, Strings.Config_IncreaseDuration);
            ModSettings.SetDescription(ModEntry.ModName, KEY_TOTAL_PERCENT, Strings.Config_TotalPercent);
            ModSettings.SetDescription(ModEntry.ModName, KEY_ENABLE_MENTALITY_HALF, Strings.Config_EnableMentalityHalf);
        }
    }
}