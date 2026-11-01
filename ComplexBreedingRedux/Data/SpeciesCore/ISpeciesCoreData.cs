#nullable enable
using System;
using System.Collections.Generic;
using ComplexBreedingRedux.Data.Species;
using MBMScripts;
using UnityEngine;

namespace ComplexBreedingRedux.Data.SpeciesCore;

internal interface ISpeciesCoreData : ISpeciesStats
{
    ISpeciesCoreData GetInstance { get; }
    Type GetReplacedType { get; }
    string InternalName { get; }
    string Name { get; set; }
    Color[] SkinColors { get; set; }
    Color[]? HairColors { get; set; }
    Color? HairColorMigration { get; set; }
    float? HairColorMigrationPower { get; set; }
    bool HairColorSameAsSkin { get; set; }
    Color[]? ScaleColors { get; set; }
    Color[]? HornColors { get; set; }
    int[] TitsTypes { get; set; }
    Color? EyeColorForMigration { get; set; }
    ICollection<ETrait>? GuaranteedTraits { get; set; }
}
