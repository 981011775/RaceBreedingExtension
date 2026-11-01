namespace ComplexBreedingRedux.Data.Professions;

/// <summary>
/// 职业的「战斗击杀」模式（由职业在 sex 触发 / sex 场景结束时生效）：
/// 全部要求持有者「未堕落」（!IsDepraved）。
/// </summary>
internal enum EProfessionKillMode
{
    /// <summary>无战斗击杀效果。</summary>
    None = 0,

    /// <summary>
    /// 单体：战斗力 &gt; 怪物 → KillChance% 秒杀；战斗力 ≥ 怪物 ×2 → 100% 秒杀。
    /// （KillChance = 25 时为「25% / 2 倍 100%」；= 100 时为「只要更强就 100%」）
    /// </summary>
    SingleTarget = 1,

    /// <summary>
    /// 房间内所有战斗力低于自己的怪物 → KillChance% 秒杀。
    /// 仅「猎手营地 / 大型猎手营地」房间内生效（只影响自己所在房间）。
    /// </summary>
    RoomWeakMonsters = 2,
}
