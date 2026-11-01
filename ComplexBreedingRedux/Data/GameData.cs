#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ComplexBreedingRedux.Data.Config;
using ComplexBreedingRedux.Data.Factions;
using ComplexBreedingRedux.Data.Professions;
using ComplexBreedingRedux.Data.Species;
using ComplexBreedingRedux.Data.SpeciesCore;
using ComplexBreedingRedux.Data.Traits;
using ComplexBreedingRedux.Features;
using MBMScripts;
using UnityEngine;

namespace ComplexBreedingRedux.Data;

/// <summary>模组数据注册中心，通过反射自动注册。</summary>
internal static class GameData
{
    // --- 数据注册表 ---
    public static readonly List<ITraitData> TraitsDataList = new List<ITraitData>();
    public static readonly List<ISpeciesCoreData> SpeciesCoreDataList = new List<ISpeciesCoreData>();
    public static readonly List<ISpeciesData> SpeciesDataList = new List<ISpeciesData>();
    public static readonly List<IProfessionData> ProfessionDataList = new List<IProfessionData>();
    public static readonly List<FactionData> FactionDataList = new List<FactionData>();

    // --- 通过反射自动注册 ---

    /// <summary>扫描程序集，按接口填充所有注册表。</summary>
    public static void Initialize()
    {
        TraitsDataList.AddRange(TraitRegistry.All);
        SpeciesCoreDataList.AddRange(SpeciesCoreRegistry.All);
        SpeciesDataList.AddRange(SpeciesRegistry.All);
        ProfessionDataList.AddRange(ProfessionRegistry.All);
        FactionDataList.AddRange(FactionRegistry.All);
        // 注：已删除 Essences、Personalities 的注册（精华/性格系统精简）
    }

    // --- 派系查找 ---

    /// <summary>根据 Id（= 访客 DataType，0-4）查找派系。</summary>
    public static bool TryGetFaction(int id, out FactionData? faction)
    {
        faction = FactionDataList.FirstOrDefault(x => x.Id == id);
        return faction != null;
    }

    // --- 物种身份标识 ---

    /// <summary>将物种的 InternalName 写入角色身上的 Buff。</summary>
    public static void SetSpeciesIdentity(Character character, string internalName)
    {
        if (character.GetBuff(ConfigBuffs.SpeciesIdentity) != null)
            character.RemoveBuff(ConfigBuffs.SpeciesIdentity);
        character.AddBuff(ConfigBuffs.SpeciesIdentity, 0f, true, false, internalName);
    }

    /// <summary>从角色身上的 Buff 读取物种的 InternalName。</summary>
    public static string? GetSpeciesIdentity(Character character)
    {
        Buff? buff = character.GetBuff(ConfigBuffs.SpeciesIdentity);
        if (buff == null || string.IsNullOrWhiteSpace(buff.SpriteName))
            return null;
        return buff.SpriteName;
    }

    // --- 查找方法 ---

    /// <summary>根据游戏内 ETrait 查找模组特质。</summary>
    public static bool TryGetTrait(ETrait trait, out ITraitData? traitData)
    {
        traitData = TraitsDataList.FirstOrDefault(x => x.ReplacedTrait == trait);
        return traitData != null;
    }

    /// <summary>根据 ERace 查找基础物种核心。</summary>
    public static bool TryGetSpeciesCore(ERace race, out ISpeciesCoreData? speciesCore)
    {
        speciesCore = SpeciesCoreDataList.FirstOrDefault(x => x.Race == race);
        return speciesCore != null;
    }

    /// <summary>根据角色查找基础物种核心。</summary>
    public static bool TryGetSpeciesCore(Character character, out ISpeciesCoreData? speciesCore)
        => TryGetSpeciesCore(character.Race, out speciesCore);

    /// <summary>根据稳定的 InternalName（如 Human、Gnome、Imp）查找基础核心物种。</summary>
    public static bool TryGetSpeciesCoreByInternalName(string? internalName, out ISpeciesCoreData? speciesCore)
    {
        if (string.IsNullOrWhiteSpace(internalName))
        {
            speciesCore = null;
            return false;
        }

        speciesCore = SpeciesCoreDataList.FirstOrDefault(x =>
            string.Equals(x.InternalName, internalName, StringComparison.OrdinalIgnoreCase));
        return speciesCore != null;
    }

    /// <summary>根据原版 EUnitType 获取核心物种 InternalName（Human → Human, Dwarf → Gnome…）。</summary>
    public static bool TryGetCoreInternalNameByUnitType(EUnitType unitType, out string? internalName)
    {
        internalName = null;
        Type? vanillaType = unitType switch
        {
            EUnitType.Human => typeof(Human),
            EUnitType.Elf => typeof(Elf),
            EUnitType.Dwarf => typeof(Dwarf),
            EUnitType.Neko => typeof(Neko),
            EUnitType.Inu => typeof(Inu),
            EUnitType.Usagi => typeof(Usagi),
            EUnitType.Hitsuji => typeof(Hitsuji),
            EUnitType.Dragonian => typeof(Dragonian),
            _ => null,
        };

        ISpeciesCoreData? core = vanillaType != null
            ? SpeciesCoreDataList.FirstOrDefault(x => x.GetReplacedType == vanillaType)
            : null;
        if (core == null)
            return false;

        internalName = core.InternalName;
        return true;
    }

    /// <summary>根据原版 EUnitType（Human、Elf、Dwarf、Neko、Inu、Usagi、Hitsuji、Dragonian）查找 CBR 基础物种。</summary>
    public static bool TryGetSpeciesCoreByUnitType(EUnitType unitType, out ISpeciesCoreData? speciesCore)
    {
        if (!TryGetCoreInternalNameByUnitType(unitType, out string? internalName))
        {
            speciesCore = null;
            return false;
        }

        return TryGetSpeciesCoreByInternalName(internalName, out speciesCore);
    }

    /// <summary>根据角色身上的 SpeciesIdentity Buff 查找特殊物种。</summary>
    public static bool TryGetSpecies(Character character, out ISpeciesData? species)
    {
        string? buffName = GetSpeciesIdentity(character);
        string? name = buffName ?? SkinColorSystem.TryMigrate(character);
        species = name != null
            ? SpeciesDataList.FirstOrDefault(x => x.InternalName == name)
            : null;
        return species != null;
    }

    /// <summary>根据 InternalName 查找特殊物种。</summary>
    public static bool TryGetSpecies(string? internalName, out ISpeciesData? species)
    {
        species = internalName != null
            ? SpeciesDataList.FirstOrDefault(x => x.InternalName == internalName)
            : null;
        return species != null;
    }

    /// <summary>根据特质值 ETrait(10) 查找角色职业。</summary>
    public static bool TryGetProfession(Character female, out IProfessionData? profession)
    {
        int professionId = (int)female.GetTraitValue(GameBalance.ProfessionTraitId);
        profession = ProfessionDataList.FirstOrDefault(x => x.Id == professionId);
        return profession != null;
    }

    // 已删除 TryGetEssence、TryGetPersonality 以及所有对 Essences、Personalities、Factions 的引用
}