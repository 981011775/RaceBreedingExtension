#nullable enable
using System;
using MBM.ModLoader.Core;

namespace RaceBreedingExtension.GroundCamp
{
    /// <summary>
    /// 地面猎手营地（Ground Camp）子系统内轻量三语本地化（zh-CN 简体 / en 英文 / ru 俄文，其它语言回退英文）。
    ///
    /// 房间名会被直接写入“地面版 RoomData 副本”的 DisplayName（不进入游戏 <c>#...</c> 本地化键体系），
    /// 因此语言切换时需要调用 <see cref="GroundCampSystem.RefreshRoomNames()"/> 重新写入。
    /// </summary>
    internal static class GclLoc
    {
        private static bool IsZh =>
            Localization.CurrentLanguageCode?.StartsWith("zh", StringComparison.OrdinalIgnoreCase) == true;

        private static bool IsRu =>
            Localization.CurrentLanguageCode?.StartsWith("ru", StringComparison.OrdinalIgnoreCase) == true;

        private static string Pick(string zh, string en, string ru) => IsZh ? zh : (IsRu ? ru : en);

        // ---- 房间名（用户指定）----

        /// <summary>地面猎手营地（复制自原版怪物牢笼 MonsterCage）。</summary>
        public static string RoomName => Pick("猎手营地", "Hunter Camp", "Охотничий лагерь");

        /// <summary>大型地面猎手营地（复制自原版大型怪物牢笼 MonsterCage2）。</summary>
        public static string RoomNameLarge =>
            Pick("大型猎手营地", "Large Hunter Camp", "Большой охотничий лагерь");

        // ---- 模组菜单描述 ----

        public static string Config_Enable =>
            Pick("在个人领地地面建造列表中追加「猎手营地 / 大型猎手营地」（复制原版怪物牢笼，可同时放入怪物与奴隶）",
                 "Add ground-buildable Hunter Camp / Large Hunter Camp to the private estate build list " +
                 "(copies of MonsterCage / MonsterCage2, monsters and slaves can share the room)",
                 "Добавить в список наземных построек личного владения «Охотничий лагерь / Большой охотничий лагерь» " +
                 "(копии ванильных клеток монстров: в комнате могут находиться и монстры, и рабыни)");

        public static string Config_GateLargeCamp =>
            Pick("大型猎手营地需要先解锁原版“巨型怪物”教程事件（与原版大型怪物牢笼的解锁条件一致，默认关闭）",
                 "Require the vanilla 'huge monster' tutorial event before the Large Hunter Camp becomes " +
                 "available (same gate as the vanilla MonsterCage2, default off)",
                 "Для Большого охотничьего лагеря нужно сначала открыть ванильное обучающее событие «гигантский монстр» " +
                 "(то же условие, что у ванильной большой клетки монстров; по умолчанию выключено)");

        public static string Config_SmallSlaveCapacity =>
            Pick("猎手营地可放入的奴隶(Female)数量上限（默认 5，怪物上限与原版怪物牢笼一致：5）",
                 "Slave (Female) capacity of the Hunter Camp (default 5; monster capacity follows the vanilla MonsterCage: 5)",
                 "Лимит рабынь (Female) в Охотничьем лагере " +
                 "(по умолчанию 5; лимит монстров — как у ванильной клетки монстров: 5)");

        public static string Config_LargeSlaveCapacity =>
            Pick("大型猎手营地可放入的奴隶(Female)数量上限（默认 3，怪物上限与原版大型怪物牢笼一致：3）",
                 "Slave (Female) capacity of the Large Hunter Camp (default 3; monster capacity follows the vanilla MonsterCage2: 3)",
                 "Лимит рабынь (Female) в Большом охотничьем лагере " +
                 "(по умолчанию 3; лимит монстров — как у ванильной большой клетки монстров: 3)");

        public static string Config_EnableLogging =>
            Pick("启用地面猎手营地调试日志", "Enable ground hunter camp debug logging",
                 "Включить отладочный лог наземного Охотничьего лагеря");
    }
}
