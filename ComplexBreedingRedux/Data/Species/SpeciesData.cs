#nullable enable
using System;
using System.Collections.Generic;
using MBMScripts;
using UnityEngine;

namespace ComplexBreedingRedux.Data.Species;

internal sealed class SpeciesData : ISpeciesData
{
    public SpeciesData(string internalName, ERace race)
    {
        InternalName = internalName;
        Name = internalName;
        Race = race;
    }

    public ISpeciesData GetInstance => this;
    public string InternalName { get; }
    public ERace Race { get; }
    public int Tier { get; set; }
    public string Name { get; set; }
    public Color[] SkinColors { get; set; } = [];
    public Color[]? HairColors { get; set; }
    public float HairColorFocus { get; set; }
    public bool HairColorSameAsSkin { get; set; }
    public Color? EyeColor { get; set; }
    public float EyeColorFocus { get; set; }
    public Color[]? ScaleColors { get; set; }
    public Color? HornColor { get; set; }

    /// <summary>「独特服色」初始值（物种被套用时写入角色的服色扩展字段；null = 不设置）。</summary>
    public Color? ClothesColor { get; set; }
    public EFigure Figure { get; set; }
    public string? RigPrefix { get; set; }
    public int EyeBallType { get; set; }
    public int EarType { get; set; }
    public int EarHairType { get; set; }
    public int HornType { get; set; }
    public int HornDragonType { get; set; }
    public int TailType { get; set; }
    public int TailDragonType { get; set; }
    public int? TitsType { get; set; }
    public int HairBodyType { get; set; }
    public int UpperBody { get; set; }
    public int LowerBody { get; set; }
    public float HealthRegeneration { get; set; }
    public int MilkQuality { get; set; }
    public float MilkProductionRate { get; set; }
    public float MaxMilkSizeChange { get; set; }
    public int ConceptionRate { get; set; }
    public int MaxHealth { get; set; }
    public int GrowthTime { get; set; }
    public int MaxBirthCount { get; set; }
    public int Maintenance { get; set; }
    public float DecreasingMentalityOnPregnant { get; set; }
    public int BaseChance { get; set; }
    public int VenerealDiseaseChance { get; set; }
    public int HighHealthChance { get; set; }
    public int LowHealthChance { get; set; }
    public int TrainingChance { get; set; }
    public ERace[]? PossiblyFathers { get; set; }
    public string[]? PossiblyMothers { get; set; }
    public Type? BaseMother { get; set; }
    public Dictionary<ETrait, int>? RequiredEssences { get; set; }
    public ICollection<ETrait>? ForbiddenEssences { get; set; }
    public ICollection<ETrait>? GuaranteedTraits { get; set; }

    // 注：原来的 HighRaceTraitId（高等种族特质）字段已随 HighRaceTrait 系统整体移除，
    // 物种血脉改由 RaceBreedingExtension.Bloodline.SpeciesBloodlineMap 按物种 InternalName 统一配置。
}
