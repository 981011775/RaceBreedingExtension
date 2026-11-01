#nullable enable
using System.Collections.Generic;
using MBMScripts;

namespace ComplexBreedingRedux.Data.Professions;

/// <summary>
/// Единый класс данных профессии. Заменяет 13 отдельных *ProfessionData файлов.
/// </summary>
internal sealed class ProfessionData : IProfessionData
{
    public ProfessionData(string internalName, int id, int tier)
    {
        InternalName = internalName;
        Name = internalName;
        Description = string.Empty;
        Id = id;
        Tier = tier;
    }

    public string InternalName { get; }
    public string Name { get; set; }
    public string Description { get; set; }
    public int Id { get; set; }
    public int Tier { get; set; }
    public ICollection<JobDescription>? Jobs { get; set; }
    public bool Teachable { get; set; }
    public int BaseChance { get; set; }
    public ICollection<ERace>? ForbiddenStartingSpecies { get; set; }
    public ICollection<ETrait>? GuaranteedTraits { get; set; }
    public bool MonsterInheritable { get; set; }
    public float CombatPowerBonus { get; set; }
    public EProfessionKillMode KillMode { get; set; }
    public int KillChance { get; set; }
    public int CampSexCountBonus { get; set; }
}