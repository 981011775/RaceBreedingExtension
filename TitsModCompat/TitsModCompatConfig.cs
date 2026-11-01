#nullable enable
using MBM.ModLoader.Settings;

namespace RaceBreedingExtension.TitsModCompat
{
    /// <summary>
    /// titmod 兼容子系统（肖像模组）的配置（模组菜单开关）。
    /// 所有设置键均以 "TitsModCompat_" 前缀命名，避免与共用同一个 ModName
    /// （RaceBreedingExtension）的 DepravedShield（"Enable"/"EnableLogging" 等）和
    /// VoiceReplay（"VoiceReplay_"）子系统的键冲突。
    /// 菜单描述通过 Properties.Strings 资源完成本地化（随游戏语言切换）。
    /// </summary>
    public static class TitsModCompatConfig
    {
        private const string KEY_BLOCK_UNIQUE_PORTRAIT = "TitsModCompat_BlockUniquePortrait";

        /// <summary>
        /// 屏蔽 titmod 对独特角色（特殊NPC）Spine 肖像的修改（默认开启，修复肖像变黑）；
        /// 非独特角色不受影响，仍保留 titmod 的动态 Spine 肖像。
        /// </summary>
        public static bool BlockUniquePortrait { get; private set; }

        /// <summary>由 TitsModCompatSystem.Initialize() 调用一次（调用前需已设置 Properties.Strings.Culture）。</summary>
        public static void Register()
        {
            RegisterBool(KEY_BLOCK_UNIQUE_PORTRAIT, true, Properties.Strings.Config_BlockUniquePortrait);

            RefreshValues();
        }

        private static void RegisterBool(string key, bool defaultValue, string desc)
        {
            ModSettings.RegisterBool(ModEntry.ModName, key, defaultValue, desc);
            ModSettings.OnChanged(ModEntry.ModName, key, _ => RefreshValues());
        }

        /// <summary>语言切换时刷新模组菜单描述（由 TitsModCompatSystem.OnLanguageChanged 调用）。</summary>
        public static void OnLanguageChanged()
        {
            ModSettings.SetDescription(ModEntry.ModName, KEY_BLOCK_UNIQUE_PORTRAIT, Properties.Strings.Config_BlockUniquePortrait);
        }

        private static void RefreshValues()
        {
            BlockUniquePortrait = ModSettings.GetBool(ModEntry.ModName, KEY_BLOCK_UNIQUE_PORTRAIT);
        }
    }
}

