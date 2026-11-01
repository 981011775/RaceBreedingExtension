#nullable enable
using System.Collections.Generic;
using MBMScripts;

namespace ComplexBreedingRedux.Data.Professions;

internal interface IProfessionData
{
    /// <summary>Неизменяемое английское имя (ключ для локализации).</summary>
    string InternalName { get; }
    string Name { get; set; }
    string Description { get; set; }
    int Id { get; set; }
    int Tier { get; set; }
    ICollection<JobDescription>? Jobs { get; set; }
    bool Teachable { get; set; }
    int BaseChance { get; set; }
    ICollection<ERace>? ForbiddenStartingSpecies { get; set; }
    ICollection<ETrait>? GuaranteedTraits { get; set; }

    /// <summary>是否为“怪物可遗传”职业：拥有该职业的怪物（雄性后代）可以将其遗传给后代。</summary>
    bool MonsterInheritable { get; set; }

    /// <summary>
    /// 战斗力统计加成（0.10 = +10%）。参与本人战斗力计算：
    ///   奴隶战斗力 = 体力总量 × 种族稀有度系数 × 职业稀有度系数 × 血脉系数 × (1 + 本加成)
    ///   怪物战斗力 = (Trait96 + 交配时间 + 交配每秒体力消耗×100) × 怪物种族系数 × 血脉系数 × (1 + 本加成)
    /// </summary>
    float CombatPowerBonus { get; set; }

    /// <summary>战斗击杀模式（未堕落时，在 sex 触发 / sex 场景结束时判定）。</summary>
    EProfessionKillMode KillMode { get; set; }

    /// <summary>击杀几率（单体模式：更强时 25/100；房间模式：25/50/100）。</summary>
    int KillChance { get; set; }

    /// <summary>
    /// 当该职业生成在“怪物”（雄性）身上时，猎手营地内 sex 的奴隶逃跑前所需的 sex 次数加成
    /// （原始基础次数 5 → +1/+2/+3/+4）。只对 monsters 所在房间内的奴隶生效。
    /// </summary>
    int CampSexCountBonus { get; set; }
}