#nullable enable
using System.Collections.Generic;
using System.Linq;
using ComplexBreedingRedux.Data.Species;
using MBMScripts;

namespace ComplexBreedingRedux.Data.Species;

internal static class BreedingChanceHelpers
{
    /// <summary>对应怪物种族（不含进化变体）的奴隶种族名称。</summary>
    private static readonly Dictionary<ERace, string> CanonicalSpeciesByMonsterRace = new()
    {
        [ERace.Goblin] = "Goblin",
        [ERace.Orc] = "Orc",
        [ERace.Werewolf] = "Werewolf",
        [ERace.Minotaur] = "Minotaur",
        [ERace.Salamander] = "Salamander",
    };

    /// <summary>判断奴隶和怪物是否为同一物种（例如 Orc×Orc 有效，而 Hobgoblin×Goblin 无效）。</summary>
    public static bool IsSameMonsterSpecies(Character female, Character male)
    {
        if (!GameData.TryGetSpecies(female, out ISpeciesData? femaleSpecies) || femaleSpecies == null)
            return false;

        return CanonicalSpeciesByMonsterRace.TryGetValue(male.Race, out string? canonical)
            && femaleSpecies.InternalName == canonical;
    }

    /// <summary>
    /// 基础检查：判断是否可能诞生指定物种的后代。
    /// 检查父亲种族是否在 PossiblyFathers 中，母亲是否匹配 PossiblyMothers 或 BaseMother。
    /// 返回基础几率（BaseChance），不再累加任何条件。
    /// </summary>
    public static bool BasicCheck(Character female, Character male, ISpeciesData species, out int chance)
    {
        chance = 0; // 默认0

        // 父亲必须是允许的种族
        if (species.PossiblyFathers == null || !species.PossiblyFathers.Contains(male.Race))
            return false;

        // 检查母亲是否匹配
        bool motherMatches = false;

        // 首先尝试判断母亲是否为已进化的物种
        if (GameData.TryGetSpecies(female, out var femaleSpecies) && femaleSpecies != null)
        {
            // 母亲物种匹配自身（同种繁殖）或存在于 PossiblyMothers 中
            if (femaleSpecies.InternalName == species.InternalName)
            {
                motherMatches = true;
            }
            else if (species.PossiblyMothers != null && species.PossiblyMothers.Contains(femaleSpecies.InternalName))
            {
                motherMatches = true;
            }
        }
        else
        {
            // 母亲是基础种族（无 species buff），检查 BaseMother
            if (species.BaseMother != null
                && GameData.TryGetSpeciesCore(female, out var speciesCoreData)
                && speciesCoreData != null
                && species.BaseMother == speciesCoreData.GetReplacedType)
            {
                motherMatches = true;
            }
        }

        if (!motherMatches)
            return false;

        // 通过所有检查，几率 = BaseChance
        chance = species.BaseChance;
        return true;
    }
}