#nullable enable
using MBM.ModLoader.Core;

namespace ComplexBreedingRedux.Features.SlaveMarket
{
    /// <summary>
    /// 「子代战斗力过滤出售」子系统内轻量三语本地化
    /// （zh-CN 简体 / en 英文 / ru 俄文，其它语言回退英文）。
    /// 模组菜单描述不进入游戏 <c>#...</c> 本地化键体系，仅用于本子系统内部界面。
    /// 与 UlcLoc / HclLoc / GclLoc 保持同一写法，便于语言切换时刷新。
    /// </summary>
    internal static class OpsLoc
    {
        private static bool IsZh =>
            Localization.CurrentLanguageCode?.StartsWith("zh", System.StringComparison.OrdinalIgnoreCase) == true;

        private static bool IsRu =>
            Localization.CurrentLanguageCode?.StartsWith("ru", System.StringComparison.OrdinalIgnoreCase) == true;

        private static string Pick(string zh, string en, string ru) => IsZh ? zh : (IsRu ? ru : en);

        // ---- 模组菜单描述（第一行开关）----

        /// <summary>子代战斗力过滤出售：开关描述。</summary>
        public static string Config_OffspringPowerSellFilter =>
            Pick("开启子代战斗力过滤出售",
                 "Enable offspring combat power filter for selling",
                 "Включить фильтр продажи потомства по боевой силе");
    }
}
