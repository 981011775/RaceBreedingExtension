#nullable enable
using MBMScripts;

namespace ComplexBreedingRedux.Data.Professions;

internal class JobDescription
{
    public string?  Name          { get; set; }
    public float    Value         { get; set; }
    public float    ValueMax      { get; set; }
    public int      Chance        { get; set; }
    public EJobType Type          { get; set; }
    public bool?    FullyDepraved { get; set; }

    /// <summary>该工作的奖励物品；有值时不发放金币，改为向玩家库存添加物品。</summary>
    public EItemType? ItemReward { get; set; }

    /// <summary>物品奖励数量（仅当 ItemReward 有值时生效，默认 1；当 Min/Max 指定时优先使用区间）。</summary>
    public int ItemCount { get; set; } = 1;

    /// <summary>物品奖励数量下限（0 = 未指定）。指定后每次工作在该区间内随机数量。</summary>
    public int ItemCountMin { get; set; }

    /// <summary>物品奖励数量上限（0 = 未指定）。</summary>
    public int ItemCountMax { get; set; }

    /// <summary>该工作会随机消除一名奴隶角色指定数值的防护结界(DepravedShield)；0 表示不触发。</summary>
    public int ShieldRemoval { get; set; }

    /// <summary>被消除防护结界的奴隶数量下限（0/1 = 未指定，仅 1 名）。</summary>
    public int ShieldRemovalTargetsMin { get; set; }

    /// <summary>被消除防护结界的奴隶数量上限（0 = 未指定）。</summary>
    public int ShieldRemovalTargetsMax { get; set; }
}
