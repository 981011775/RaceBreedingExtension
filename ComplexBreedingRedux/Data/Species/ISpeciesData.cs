#nullable enable
using System;
using System.Collections.Generic;
using MBMScripts;
using UnityEngine;

namespace ComplexBreedingRedux.Data.Species;

internal interface ISpeciesData : ISpeciesStats
{
    ISpeciesData GetInstance { get; }
    int Tier { get; set; }
    string InternalName { get; }
    string Name { get; set; }

    /// <summary>换皮前缀。若设置（如 "GolemRig"），该物种会复用同 Figure 的原版骨架，
    /// 但把 Spine 资产名中的原版人名（如 aure_*）替换为 {prefix}_*，从而显示自定义贴图。</summary>
    string? RigPrefix { get; set; }
    Color[] SkinColors { get; set; }
    Color[]? HairColors { get; set; }
    float HairColorFocus { get; set; }
    bool HairColorSameAsSkin { get; set; }
    Color? EyeColor { get; set; }
    float EyeColorFocus { get; set; }
    Color[]? ScaleColors { get; set; }
    Color? HornColor { get; set; }

    /// <summary>
    /// 「独特服色」初始值（仅对可换色的独特外观角色有意义：换皮物种 / 配置允许的原版独特 Figure）。
    /// 在物种被套用到角色时写入角色的服色扩展字段（见 UniqueLookColor 子系统）；
    /// <c>null</c> = 不设置（保持原版衣物染色 / 白色未覆盖）。
    /// </summary>
    Color? ClothesColor { get; set; }
    int? TitsType { get; set; }
    int BaseChance { get; set; }
    int VenerealDiseaseChance { get; set; }
    int HighHealthChance { get; set; }
    int LowHealthChance { get; set; }
    int TrainingChance { get; set; }
    ERace[]? PossiblyFathers { get; set; }
    string[]? PossiblyMothers { get; set; }
    Type? BaseMother { get; set; }
    Dictionary<ETrait, int>? RequiredEssences { get; set; }
    ICollection<ETrait>? ForbiddenEssences { get; set; }
    ICollection<ETrait>? GuaranteedTraits { get; set; }

    // 注：原来的 HighRaceTraitId（高等种族特质）字段已随 HighRaceTrait 系统整体移除，
    // 物种血脉改由 RaceBreedingExtension.Bloodline.SpeciesBloodlineMap 按物种 InternalName 统一配置。
}
