#nullable enable
using MBMScripts;

namespace RaceBreedingExtension.HunterCamp;

/// <summary>
/// 本子系统使用的自定义 Buff 类型（游戏内未知 ID 仅作为数据标记，不显示给玩家）。
/// 与 ComplexBreedingRedux.Data.Config.ConfigBuffs（1001~4001）保持区段分离。
/// </summary>
internal static class CampBuffs
{
    /// <summary>
    /// 猎手营地俘虏标记（挂在被营地自动“捕获”的 Female 身上）。
    /// 该 Buff 为 Infinity（不随时间消耗）、Show = false（不显示图标），
    /// 仅用于跨存档识别“这名奴隶是营地俘虏”，并承载两个数值：
    ///   MaxTime = 房内无怪物时累计的逃跑倒计时（秒）；
    ///   Time    = 预留（备用，当前为 0）。
    /// </summary>
    public static readonly EBuffType CaptiveMark = (EBuffType)5100;
}
