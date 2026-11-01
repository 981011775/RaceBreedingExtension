#nullable enable
using System;
using MBM.ModLoader.Core;

namespace RaceBreedingExtension.HunterCamp
{
    /// <summary>
    /// 猎手营地（地面营地俘虏系统）子系统内轻量三语本地化（zh-CN 简体 / en 英文 / ru 俄文，其它语言回退英文）。
    /// </summary>
    internal static class HclLoc
    {
        private static bool IsZh =>
            Localization.CurrentLanguageCode?.StartsWith("zh", StringComparison.OrdinalIgnoreCase) == true;

        private static bool IsRu =>
            Localization.CurrentLanguageCode?.StartsWith("ru", StringComparison.OrdinalIgnoreCase) == true;

        private static string Pick(string zh, string en, string ru) => IsZh ? zh : (IsRu ? ru : en);

        // ---- 模组菜单描述 ----

        public static string Config_Enable =>
            Pick("启用个人领地猎手营地（地面猎手营地 / 大型猎手营地）的自动捕获系统",
                 "Enable the automatic capture system of the private-estate Hunter Camps " +
                 "(Hunter Camp / Large Hunter Camp)",
                 "Включить систему автоматического захвата в Охотничьих лагерях личного владения " +
                 "(Охотничий лагерь / Большой охотничий лагерь)");

        public static string Config_SpawnCostDivisor =>
            Pick("生成奴隶消耗的金币 = 该奴隶的战斗力 ÷ 此值（默认 30，不为整数直接取整；金币不足时金额直接扣成负数）",
                 "Gold cost of a generated slave = the slave's combat power ÷ this value " +
                 "(default 30, rounded down; gold may go negative when it is not enough)",
                 "Золото за создание рабыни = её сила ÷ это значение " +
                 "(по умолчанию 30, округление вниз; при нехватке золота сумма уходит в минус)");

        public static string Config_EscapePenaltyMultiplier =>
            Pick("奴隶逃跑时损失的金币 = 该奴隶的战斗力 × 此倍率（默认 1.0，即损失等于其战斗力）",
                 "Gold lost when a captive escapes = the slave's combat power × this multiplier " +
                 "(default 1.0, i.e. the loss equals her combat power)",
                 "Потеря золота при побеге рабыни = её сила × этот множитель " +
                 "(по умолчанию 1.0, то есть потеря равна её силе)");

        public static string Config_SexEscapeCount =>
            Pick("触发多少次性行为后奴隶逃跑（可被怪物特性增加，特性规则待定）",
                 "How many sex sessions before the captive escapes " +
                 "(may be increased by monster traits; trait rules TBD)",
                 "Сколько половых актов должно пройти до побега рабыни " +
                 "(может увеличиваться чертами монстров; правила черт уточняются)");

        public static string Config_SpawnDelay =>
            Pick("营地空置多少秒后自动生成奴隶（默认 5 秒）",
                 "Seconds an empty camp waits before automatically generating a slave (default 5)",
                 "Сколько секунд пустой лагерь ждёт перед автоматическим созданием рабыни (по умолчанию 5)");

        public static string Config_EscapeSeconds =>
            Pick("房内没有怪物时，奴隶逃跑所需的秒数（默认 60 秒）",
                 "Seconds before the captive escapes when there is no monster in the room (default 60)",
                 "Сколько секунд нужно рабыне для побега, если в комнате нет монстра (по умолчанию 60)");

        public static string Config_AutoPairWithMonster =>
            Pick("自动把奴隶移到怪物所在座位（用于触发原版性行为）",
                 "Automatically move the captive onto the seat of the monster (triggers the vanilla sex logic)",
                 "Автоматически переносить рабыню на место монстра (запускает ванильную логику секса)");

        public static string Config_EnableLogging =>
            Pick("启用猎手营地调试日志", "Enable hunter camp debug logging",
                 "Включить отладочный лог Охотничьего лагеря");
    }
}

