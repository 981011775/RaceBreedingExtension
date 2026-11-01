#nullable enable
using System.Collections.Generic;
using MBMScripts;

namespace ComplexBreedingRedux.Data.Traits;

/// <summary>
/// Единый класс данных трейта. Заменяет 8 отдельных *TraitData файлов.
/// </summary>
internal sealed class TraitData : ITraitData
{
    public TraitData(string internalName, ETrait replacedTrait)
    {
        InternalName  = internalName;
        Name          = internalName;
        Description   = string.Empty;
        ReplacedTrait = replacedTrait;
    }

    public string                       InternalName                    { get; }
    public string                       Name                            { get; set; }
    public string                       Description                     { get; set; }
    public ETrait                       ReplacedTrait                   { get; set; }
    public bool                         IsPositive                      { get; set; }
    public bool                         IsAffectingMaleTrait            { get; set; }
    public bool                         IsAffectingFemaleTrait          { get; set; }
    public bool                         IsMaleTrait                     { get; set; }
    public bool                         IsFemaleTrait                   { get; set; }
    public int?                         OnlyWhenSumEssencesIsLessThen   { get; set; }
    public int?                         OnlyWhenSumEssencesIsGraterThen { get; set; }
    public Dictionary<ETrait, int>?     RequiredEssences                { get; set; }
    public ICollection<ETrait>?         ForbiddenEssences               { get; set; }
}
