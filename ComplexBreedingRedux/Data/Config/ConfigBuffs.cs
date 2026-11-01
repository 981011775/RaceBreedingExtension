#nullable enable
using MBMScripts;

namespace ComplexBreedingRedux.Data.Config;

/// <summary>
/// 游戏内所有 Buff（增益/减益状态）类型的静态配置类。
/// 将整数 ID 映射到 EBuffType 枚举，便于集中管理和引用。
/// </summary>
internal static class ConfigBuffs
{
    // ─── 派系访客系统（FactionSystem，1001-1004） ───

    /// <summary>派系访客的财富值（支付能力，由品阶与派系声誉共同决定）</summary>
    public static EBuffType FactionVisitorWealth = (EBuffType)1001;

    /// <summary>各派系的声誉值（以“id,分数;...”字符串存储在阿米莉亚的 Buff 上）</summary>
    public static EBuffType FactionsReputations = (EBuffType)1003;

    /// <summary>访客的品阶（1~5，与职业品阶同步）</summary>
    public static EBuffType FactionVisitorTier = (EBuffType)1004;

    /// <summary>
    /// 猎手营地俘虏的「供应派系」（存储值 = 派系 Id + 1；0/无 Buff = 无派系）。
    /// 仅用于记录本次生成使用了哪个派系的声望（职业权重与属性强度），供日志/UI 查询。
    /// </summary>
    public static EBuffType FactionCaptiveFaction = (EBuffType)1005;

    /// <summary>奴隶的子宫尺寸（影响受孕、生产或身体改造）</summary>
    public static EBuffType SlaveWombSize = (EBuffType)2001;

    /// <summary>子宫拉伸进度（用于追踪渐进式改造或训练过程）</summary>
    public static EBuffType WombStretchProgress = (EBuffType)2002;

    /// <summary>物种身份（用于标识角色种族或血统，影响交互选项）</summary>
    public static EBuffType SpeciesIdentity = (EBuffType)4001;
}