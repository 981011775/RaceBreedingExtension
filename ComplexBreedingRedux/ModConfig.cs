#nullable enable
using ComplexBreedingRedux.Features.SlaveMarket;

namespace ComplexBreedingRedux;

/// <summary>
/// 模组配置。
/// 已按合并要求清理“功能已关闭但残留”的代码：
/// - 原 AutoSell 模式 / 进化树配置系统已停用，相关配置键与进化树构建代码已删除，
///   仅保留 AutoSell 实际使用到的固定默认值属性；
/// - 现只注册「子代战斗力过滤出售」一个开关（见 <see cref="OffspringPowerSellFilter"/>），
///   且由于这里是全模组**最早**注册设置的地方，该开关显示在模组菜单第一行。
/// </summary>
internal static class ModConfig
{
    /// <summary>
    /// 初始化。
    /// ★ 这里是合并模组中**第一个**注册 ModSettings 的地方
    /// （RaceBreedingExtension.ModEntry.Load() → ComplexBreedingRedux.Plugin.Initialize() →
    /// ModConfig.Register()，早于 UniqueLookColor / DepravedShield / VoiceReplay 等子系统），
    /// 因此在此注册的设置会显示在模组菜单**第一行**。
    /// </summary>
    public static void Register()
    {
        // ★ 子代战斗力过滤出售（菜单第一行开关）
        OffspringPowerSellFilter.Register();
    }

    /// <summary>语言切换更新（刷新各设置项的菜单描述）</summary>
    public static void UpdateDescriptions()
    {
        OffspringPowerSellFilter.OnLanguageChanged();
    }

    // --- 公共属性：全部返回固定默认值（供 AutoSell 使用）---

    public static EAutoSellModeOption AutoSellFemaleModeEnum => EAutoSellModeOption.SellAll;
    public static EAutoSellModeOption AutoSellMaleModeEnum => EAutoSellModeOption.SellAll;
    public static EAutoSellEvoFilter AutoSellFemaleEvoFilterEnum => EAutoSellEvoFilter.NoFilter;
    public static bool AutoSellDegradation => true;
    public static string StartingSlaveSpecies => "";  // 空字符串表示无特殊起始种族

    /// <summary>检查某个进化物种是否被保护（现始终返回 false）</summary>
    public static bool IsSpeciesEvoProtected(string speciesInternalName) => false;
}
