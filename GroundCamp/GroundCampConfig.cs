#nullable enable
using MBM.ModLoader.Settings;

namespace RaceBreedingExtension.GroundCamp
{
    /// <summary>
    /// 地面猎手营地（Ground Camp）子系统配置。
    /// 设置键统一以 "GroundCamp_" 前缀命名，避免与共用 ModName（RaceBreedingExtension）的
    /// 其它子系统（DepravedShield / VoiceReplay_ / TitsModCompat_ / UniqueLook_ / HunterCamp）冲突。
    /// </summary>
    internal static class GroundCampConfig
    {
        private const string KEY_ENABLE = "GroundCamp_Enable";
        private const string KEY_GATE_LARGE = "GroundCamp_GateLargeCamp";
        private const string KEY_SMALL_SLAVE_CAP = "GroundCamp_SmallSlaveCapacity";
        private const string KEY_LARGE_SLAVE_CAP = "GroundCamp_LargeSlaveCapacity";
        private const string KEY_ENABLE_LOGGING = "GroundCamp_EnableLogging";

        /// <summary>主开关：是否在个人领地地面建造列表中追加两个猎手营地。</summary>
        public static bool Enable { get; private set; }

        /// <summary>大型猎手营地是否要求已解锁原版“巨型怪物”教程事件（与原版 MonsterCage2 的解锁条件一致）。</summary>
        public static bool GateLargeCamp { get; private set; }

        /// <summary>猎手营地可放入的奴隶(Female)数量上限。</summary>
        public static int SmallSlaveCapacity { get; private set; } = DefaultSmallSlaveCapacity;

        /// <summary>大型猎手营地可放入的奴隶(Female)数量上限。</summary>
        public static int LargeSlaveCapacity { get; private set; } = DefaultLargeSlaveCapacity;

        public static bool EnableLogging { get; private set; }

        // ─── 固定容量（与原版怪物牢笼保持一致，不提供开关） ───

        /// <summary>猎手营地怪物上限（= 原版 MonsterCage 的 5）。</summary>
        public const int SmallMonsterCapacity = 5;

        /// <summary>大型猎手营地怪物上限（= 原版 MonsterCage2 的 3）。</summary>
        public const int LargeMonsterCapacity = 3;

        /// <summary>奴隶容量默认值：猎手营地 5（需求 7）。</summary>
        public const int DefaultSmallSlaveCapacity = 5;

        /// <summary>奴隶容量默认值：大型猎手营地 3（需求 7）。</summary>
        public const int DefaultLargeSlaveCapacity = 3;

        public static void Register()
        {
            RegisterBool(KEY_ENABLE, true, GclLoc.Config_Enable);
            RegisterBool(KEY_GATE_LARGE, false, GclLoc.Config_GateLargeCamp);
            RegisterInt(KEY_SMALL_SLAVE_CAP, DefaultSmallSlaveCapacity, GclLoc.Config_SmallSlaveCapacity, 0, 20);
            RegisterInt(KEY_LARGE_SLAVE_CAP, DefaultLargeSlaveCapacity, GclLoc.Config_LargeSlaveCapacity, 0, 20);
            RegisterBool(KEY_ENABLE_LOGGING, false, GclLoc.Config_EnableLogging);

            RefreshValues();
        }

        public static void OnLanguageChanged()
        {
            ModSettings.SetDescription(ModEntry.ModName, KEY_ENABLE, GclLoc.Config_Enable);
            ModSettings.SetDescription(ModEntry.ModName, KEY_GATE_LARGE, GclLoc.Config_GateLargeCamp);
            ModSettings.SetDescription(ModEntry.ModName, KEY_SMALL_SLAVE_CAP, GclLoc.Config_SmallSlaveCapacity);
            ModSettings.SetDescription(ModEntry.ModName, KEY_LARGE_SLAVE_CAP, GclLoc.Config_LargeSlaveCapacity);
            ModSettings.SetDescription(ModEntry.ModName, KEY_ENABLE_LOGGING, GclLoc.Config_EnableLogging);
        }

        private static void RegisterBool(string key, bool defaultValue, string desc)
        {
            ModSettings.RegisterBool(ModEntry.ModName, key, defaultValue, desc);
            ModSettings.OnChanged(ModEntry.ModName, key, _ => RefreshValues());
        }

        private static void RegisterInt(string key, int defaultValue, string desc, int min, int max)
        {
            // 说明：加载器提供的是 RegisterInt（无区间重载），这里把区间写进描述里提示玩家。
            ModSettings.RegisterInt(ModEntry.ModName, key, defaultValue, $"{desc}（{min}~{max}）");
            ModSettings.OnChanged(ModEntry.ModName, key, _ => RefreshValues());
        }

        public static void RefreshValues()
        {
            Enable = ModSettings.GetBool(ModEntry.ModName, KEY_ENABLE);
            GateLargeCamp = ModSettings.GetBool(ModEntry.ModName, KEY_GATE_LARGE);
            SmallSlaveCapacity = Clamp(ModSettings.GetInt(ModEntry.ModName, KEY_SMALL_SLAVE_CAP), 0, 20, DefaultSmallSlaveCapacity);
            LargeSlaveCapacity = Clamp(ModSettings.GetInt(ModEntry.ModName, KEY_LARGE_SLAVE_CAP), 0, 20, DefaultLargeSlaveCapacity);
            EnableLogging = ModSettings.GetBool(ModEntry.ModName, KEY_ENABLE_LOGGING);
        }

        private static int Clamp(int value, int min, int max, int fallback)
        {
            if (value < min || value > max)
                return fallback;
            return value;
        }
    }
}
