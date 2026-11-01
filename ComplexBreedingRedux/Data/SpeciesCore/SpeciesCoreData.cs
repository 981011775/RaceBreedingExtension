#nullable enable
using System;
using System.Collections.Generic;
using ComplexBreedingRedux.Data.Species;
using MBMScripts;
using UnityEngine;

namespace ComplexBreedingRedux.Data.SpeciesCore;

internal sealed class SpeciesCoreData : ISpeciesCoreData
{
    public SpeciesCoreData(string internalName, ERace race, Type replacedType)
    {
        InternalName = internalName;
        Name = internalName;
        Race = race;
        GetReplacedType = replacedType;
    }

    public ISpeciesCoreData GetInstance => this;
    public string InternalName { get; }
    public ERace Race { get; }
    public Type GetReplacedType { get; }
    public string Name { get; set; }
    public Color[] SkinColors { get; set; } = [];
    public Color[]? HairColors { get; set; }
    public Color? HairColorMigration { get; set; }
    public float? HairColorMigrationPower { get; set; }
    public bool HairColorSameAsSkin { get; set; }
    public Color[]? ScaleColors { get; set; }
    public Color[]? HornColors { get; set; }
    public EFigure Figure { get; set; }
    public int EyeBallType { get; set; }
    public int EarType { get; set; }
    public int EarHairType { get; set; }
    public int HornType { get; set; }
    public int HornDragonType { get; set; }
    public int TailType { get; set; }
    public int TailDragonType { get; set; }
    public int HairBodyType { get; set; }
    public int[] TitsTypes { get; set; } = [];
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
    public Color? EyeColorForMigration { get; set; }
    public ICollection<ETrait>? GuaranteedTraits { get; set; }
}
