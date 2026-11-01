namespace RaceBreedingExtension.Bloodline.Data;

/// <summary>
/// 血脉（Bloodline）类型。取代原高等种族特质（HighRaceTrait）：
/// 每个单位只可能拥有其中一种血脉（存储在一般特质槽位 ETrait.Trait9 的值里，
/// 编码方式见 <see cref="BloodlineSystem.Encode"/>）。
/// ID 与「种族特质新.txt」中的顺序一致。
/// </summary>
internal enum EBloodline
{
    /// <summary>无血脉。</summary>
    None = 0,

    /// <summary>恶魔血脉 Demon Bloodline（紫色）。</summary>
    Demon = 1,

    /// <summary>怪物血脉 Monster Bloodline（红色）。</summary>
    Monster = 2,

    /// <summary>野性血脉 Wild Bloodline（墨绿色）。</summary>
    Wild = 3,

    /// <summary>神圣血脉 Divine Bloodline（金色）。</summary>
    Divine = 4,

    /// <summary>魔力血脉 Magic Bloodline（蓝色）。</summary>
    Magic = 5,

    /// <summary>元素血脉 Elemental Bloodline（青色）。</summary>
    Elemental = 6,

    /// <summary>自然血脉 Nature Bloodline（葱绿色）。</summary>
    Nature = 7,

    /// <summary>龙之血脉 Dragon Bloodline（橙色）。</summary>
    Dragon = 8,
}
