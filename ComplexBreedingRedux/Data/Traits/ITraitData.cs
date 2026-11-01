#nullable enable
using System.Collections.Generic;
using MBMScripts;

namespace ComplexBreedingRedux.Data.Traits;

internal interface ITraitData
{
    string InternalName { get; }
    string Name { get; set; }
    string Description { get; set; }
    ETrait ReplacedTrait { get; set; }
    bool IsPositive { get; set; }
    bool IsAffectingMaleTrait { get; set; }
    bool IsAffectingFemaleTrait { get; set; }
    bool IsMaleTrait { get; set; }
    bool IsFemaleTrait { get; set; }
    int? OnlyWhenSumEssencesIsLessThen { get; set; }
    int? OnlyWhenSumEssencesIsGraterThen { get; set; }
    Dictionary<ETrait, int>? RequiredEssences { get; set; }
    ICollection<ETrait>? ForbiddenEssences { get; set; }
}
