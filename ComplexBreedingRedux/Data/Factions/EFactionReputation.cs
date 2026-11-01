#nullable enable

namespace ComplexBreedingRedux.Data.Factions;

/// <summary>派系声誉等级（对应声誉分数区间，见 FactionSystem.GetReputationTier）。</summary>
internal enum EFactionReputation
{
    Unknown = 0,
    Obscure = 1,
    Insignificant = 2,
    Noteworthy = 3,
    Reputable = 4,
    Significant = 5,
    Famous = 6,
    Fabled = 7,
}
