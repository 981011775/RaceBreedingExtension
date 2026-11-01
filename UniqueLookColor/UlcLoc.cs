#nullable enable
using MBM.ModLoader.Core;

namespace RaceBreedingExtension.UniqueLookColor
{
    /// <summary>
    /// 子系统内轻量三语本地化（zh-CN 简体 / en 英文 / ru 俄文，其它语言回退英文）。
    /// UI 标签与模组菜单描述均不进入游戏 <c>#...</c> 本地化键体系，仅用于本子系统内部界面。
    /// </summary>
    internal static class UlcLoc
    {
        private static bool IsZh =>
            Localization.CurrentLanguageCode?.StartsWith("zh", System.StringComparison.OrdinalIgnoreCase) == true;

        private static bool IsRu =>
            Localization.CurrentLanguageCode?.StartsWith("ru", System.StringComparison.OrdinalIgnoreCase) == true;

        private static string Pick(string zh, string en, string ru) => IsZh ? zh : (IsRu ? ru : en);

        // ---- UI 标签 ----
        public static string Slot_Hair =>
            Pick("独特发色", "Unique Hair Color", "Свой цвет волос");

        public static string Slot_Eye =>
            Pick("独特瞳色", "Unique Eye Color", "Свой цвет глаз");

        public static string Slot_Clothes =>
            Pick("独特服色", "Unique Clothes Color", "Свой цвет одежды");

        /// <summary>兽系独特角色（雪狼人 / 魔兔人）没有角颜色框：服色槽改为控制尾巴 / 兽耳毛色。</summary>
        public static string Slot_Fur =>
            Pick("独特毛色", "Unique Fur Color", "Свой цвет меха");

        // ---- 模组菜单描述 ----
        public static string Config_Enable =>
            Pick("启用独特角色头发/眼睛换色（GolemRig 等换皮物种在渲染层应用 HairColor/EyeColor）",
                 "Apply hair/eye color of rigged unique-figure characters (e.g. GolemRig) at render time",
                 "Включить перекраску волос/глаз у уникальных персонажей " +
                 "(виды с подменой скелета, например GolemRig, применяют HairColor/EyeColor при отрисовке)");

        public static string Config_RecolorVanillaFigure =>
            Pick("允许对原版独特 Figure 角色（Aure 等，无换皮前缀）也应用换色（默认关闭，避免误染原版 NPC）",
                 "Also recolor characters using vanilla unique figures (Aure etc.) without a rig prefix (default off)",
                 "Разрешить перекраску и для ванильных уникальных Figure-персонажей (Aure и др., без префикса подмены; " +
                 "по умолчанию выключено, чтобы не перекрашивать ванильных NPC)");

        public static string Config_EnableLogging =>
            Pick("启用独特角色换色调试日志", "Enable unique look color debug logging",
                 "Включить отладочный лог перекраски уникальных персонажей");
    }
}
