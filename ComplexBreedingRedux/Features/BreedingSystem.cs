#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using ComplexBreedingRedux.Data;
using ComplexBreedingRedux.Data.Professions;
using ComplexBreedingRedux.Data.Species;
using ComplexBreedingRedux.Data.SpeciesCore;
using ComplexBreedingRedux.Data.Traits;
using ComplexBreedingRedux.Utilities;
using HarmonyLib;
using MBMScripts;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ComplexBreedingRedux.Features;

internal static class BreedingSystem
{
    /// <summary><see cref="CheckEvolutionPregnancy"/> 的最大递归深度。</summary>
    private const int MaxEvolutionDepth = 5;

    /// <summary>读取角色普通特质字典（TraitInfoDictionary 为私有属性，需反射访问）。</summary>
    private static readonly Func<Character, SeqDictionary<ETrait, TraitInfo>> GetTraitInfoDict =
        (Func<Character, SeqDictionary<ETrait, TraitInfo>>)Delegate.CreateDelegate(
            typeof(Func<Character, SeqDictionary<ETrait, TraitInfo>>),
            AccessTools.PropertyGetter(typeof(Character), "TraitInfoDictionary"));

    /// <summary>
    /// 「假人类父本」→「真实父本」映射（<see cref="GetFakeHumanMaleForWomenBirth"/> 创建）。
    ///
    /// 受孕流程会把怪物 / 玩家 / 派系访客统一包装成一个临时 <see cref="Client"/>，
    /// 以便让子代走人类（物种）逻辑；但「血脉遗传」必须看到真实父本：
    ///   · 真实父本是派系访客（Client）/ 玩家（Player）→ 适用「女性奴隶 × 人类客户」规则；
    ///   · 真实父本是怪物（雄性）              → 适用「母亲 × 怪物 / 雄性」规则。
    /// 若不还原，客户的血脉（Trait9）取不到 → 客户血脉规则全部失效；
    /// 怪物父本也会被这个包装误判成人类客户 → 异血脉时不再 50/50、也不再 +1。
    ///
    /// 使用 ConditionalWeakTable（以引用相等为键）：临时对象回收后条目自动消失，不会泄漏。
    /// </summary>
    private static readonly ConditionalWeakTable<Client, Character> FakeFatherOrigins =
        new ConditionalWeakTable<Client, Character>();


    internal static void Apply(Character __instance, Character? monster, ref bool __result)
    {
        if (monster == null) { __result = false; return; }

        // 防护结界（DepravedShield）生效时彻底阻止受孕：
        // 原版 BeImpregnated 已由护盾补丁拦截，这里同时阻止模组自身的受孕逻辑，
        // 避免“阻止了胎儿创建、却仍返回受孕成功”导致的空怀孕 / 无限怀孕。
        if (DepravedShield.ModConfig.Enable
            && DepravedShield.Helpers.SexProgressManager.GetDepravedShield(__instance) > 0)
        {
            ResetEmptyPregnancy(__instance);
            __result = false;
            return;
        }

        int fetusCountBefore = __instance.FetusUnitIdList.Count;

        // --- 受孕 ---
        bool canConceive = __instance.FetusUnitIdList.Count == 0
            && !__instance.IsInfertile
            && !__instance.IsBaby
            && __instance.BirthCount < __instance.MaxBirthCount
            && !((monster.IsBig || monster is Horse) && __instance.MultiplePregnancyCount < 2);

        if (canConceive)
        {
            // --- 受孕几率 ---
            float conceptionChance;
            if (__instance.BuffListContains(EBuffType.FertilityMedication))
            {
                conceptionChance = 1f;
                __instance.RemoveBuff(EBuffType.FertilityMedication);
            }
            else if (!PlayDataUtils.GetInstance().GetGameEventFlag(EGameEvent.Tutorial_FirstCumshot))
            {
                conceptionChance = 1f;
                PlayDataUtils.GetInstance().SetGameEventFlag(EGameEvent.Tutorial_FirstCumshot, true);
            }
            else
            {
                float femaleRate = Mathf.Clamp(__instance.ConceptionRate, 0.01f, 1f);
                conceptionChance = (femaleRate + monster.ConceptionRate) * 0.5f;
            }

            int roll = (int)Math.Round(conceptionChance * 100f, 0);
            if (Random.Range(1, 101) <= roll)
                GetPregnant(__instance, monster, __instance.MultiplePregnancyCount);
        }

        WombSystem.AddStretchProgress(__instance, monster, isInside: true, __instance.FetusUnitIdList.Count, justConceived: __instance.FetusUnitIdList.Count > fetusCountBefore);

        // ---- 新增：子宫大小多胎加成 ----
        // ★ 触手（Tentacle）不参与多胎加成：原版对触手父本固定只生成 1 个胎儿
        //   （见 Character.BeImpregnated 中的 `if (!(monster is Tentacle))` 分支），
        //   一次受孕 = 1 个触手卵，不能因为子宫尺寸而多出若干触手卵。
        if (__instance.FetusUnitIdList.Count > fetusCountBefore
            && __instance is Female female
            && !(monster is Tentacle))
        {
            ApplyMultiplePregnancyBonus(female, monster);
        }

        // 只有确实创建出胎儿才算受孕成功：否则男性方会把 SexState 置为“受孕成功”，
        // 而女性身上没有胎儿（PregnantType=1 / FetusUnitIdList 为空）→ 空怀孕、永远无法生育。
        bool conceived = __instance.FetusUnitIdList.Count > fetusCountBefore;
        if (conceived)
            PlayDataUtils.AddAchievement(EAchievement.A000002);
        else
            ResetEmptyPregnancy(__instance);

        __result = conceived;
    }

    /// <summary>
    /// 清理“处于怀孕状态但没有胎儿”的异常状态（护盾拦截或胎儿创建失败时可能出现），
    /// 避免女性被永久卡在空怀孕状态而无法再次受孕/生育。
    /// </summary>
    private static void ResetEmptyPregnancy(Character female)
    {
        if (female == null)
            return;
        if (female.FetusUnitIdList.Count == 0 && female.PregnantType > 0)
        {
            female.PregnantType = 0;
            female.IsJustPregnant = false;
        }
    }

    /// <summary>
    /// 根据子宫大小增加多胎率：若随机判定成功，则填充多胎上限。
    /// </summary>
    private static void ApplyMultiplePregnancyBonus(Female female, Character male)
    {
        EWombSize size = WombSystem.GetWombSize(female);
        int chance = 0;
        switch (size)
        {
            case EWombSize.Small: chance = 0; break;
            case EWombSize.Average: chance = 10; break;
            case EWombSize.Big: chance = 20; break;
            case EWombSize.Huge: chance = 30; break;
            case EWombSize.Enormous: chance = 40; break;
        }
        if (chance <= 0) return;
        if (Random.Range(0, 100) >= chance) return;  // 未命中

        int maxFetus = female.MultiplePregnancyCount - female.FetusUnitIdList.Count;
        if (maxFetus > 0)
        {
            for (int i = 0; i < maxFetus; i++)
            {
                GameManager.Instance.PlayerData.NewFetus(female, male);
            }
            // 可在此添加日志或提示（可选）
        }
    }

    private static bool Rand(int chance)
        => chance > 0 && Random.Range(1, 101) <= chance;

    private static bool CheckNewSpeciesImpregnation(Character female, Character male)
    {
        foreach (ISpeciesData species in GameData.SpeciesDataList
            .OrderByDescending(x => x.Tier)
            .ThenBy(x => x.Name))
        {
            if (!BreedingChanceHelpers.BasicCheck(female, male, species, out int chance))
                continue;

            if (chance > 0)
                GameManagerUtils.AddDebugLogMessage($"Preg:{chance}% -> {female.RaceName},{species.Name}");

            if (Rand(chance))
            {
                ERace savedRace = female.Race;
                female.Race = species.Race;
                female.GetPregnant(GetFakeHumanMaleForWomenBirth(male, species.InternalName));
                female.Race = savedRace;
                return true;
            }
        }
        return false;
    }

    private static bool SameSpeciesImpregnation(Character female, Character male)
    {
        bool eligible = male.Race == ERace.Human || BreedingChanceHelpers.IsSameMonsterSpecies(female, male);
        return eligible && SameOrLowerSpeciesFemalePregnancy(female, male);
    }

    /// <summary>
    /// 按母亲的物种走「同族 → 降级物种 → 基础种族」链并直接产出女胎（不检查父本物种是否匹配）。
    /// 母亲本身是基础种族（无物种身份）时，直接产出同族（基础种族）女胎。
    /// </summary>
    private static bool SameOrLowerSpeciesFemalePregnancy(Character female, Character male)
    {
        if (GameData.TryGetSpecies(female, out ISpeciesData? femaleSpecies) && femaleSpecies != null)
            return CheckEvolutionPregnancy(female, male, femaleSpecies, isRootSpecies: true);

        female.GetPregnant(GetFakeHumanMaleForWomenBirth(male, string.Empty));
        return true;
    }

    /// <summary>
    /// ② 同族 / 降级物种（含基础种族）女胎判定。
    /// · 人类父本（客户）：维持原逻辑——同族 → 降级物种 → 基础种族的整条链；
    /// · 怪物 / 雄性父本：仅 <see cref="GameBalance.MonsterSameOrLowerFemaleChance"/>%（默认 10%）
    ///   的几率走该分支，并且不再要求父本与母亲是「同种怪物」——所谓同族即母亲自己的物种。
    ///   ⇒ 奴隶 × 怪物的结果分布：
    ///     ① 进化物种女胎（<see cref="CheckNewSpeciesImpregnation"/>，按各物种 BaseChance 判定）
    ///     ② 同族 / 降级物种女胎（含基础种族）：10%
    ///     ③ 怪物男胎：剩余大部分概率
    /// </summary>
    private static bool ReturnsSameOrLowerSpeciesFemale(Character female, Character male)
    {
        if (male.Race == ERace.Human)
            return SameSpeciesImpregnation(female, male);

        return Rand(GameBalance.MonsterSameOrLowerFemaleChance)
            && SameOrLowerSpeciesFemalePregnancy(female, male);
    }

    /// <summary>
    /// 同族 / 降级生育判定（女胎）。分两步：
    ///   ① 同族：几率 = 100 − <see cref="GameBalance.TierLowerEvoBirths"/>[品阶]。
    ///      品阶 1 同样带降级几率（见 GameBalance 注释），否则链条会断在品阶 1。
    ///      ★ 没有任何降级目标的物种（例如「职业能力生成」的 Golem：无 BaseMother、
    ///        无 PossiblyMothers）恒定同族，不做降级判定，也不落回上层兜底。
    ///   ② 降级：候选 =「PossiblyMothers 指向的链条物种」+「BaseMother 基础种族」。
    ///      · 配了基础种族（BaseMother）时优先产出基础种族：把母亲 Race 临时换成基础种族并用
    ///        空物种名的假人类父本，后代不带物种身份 ⇒ 即为基础种族（含基础怪物种族）；
    ///      · 否则递归进入链条物种（最多 <see cref="MaxEvolutionDepth"/> 层），
    ///        因此高阶物种最终也能落到基础种族：
    ///        Bugbear → Redcap → Hobgoblin → Goblin → Human（基础种族）。
    /// </summary>
    private static bool CheckEvolutionPregnancy(
        Character female, Character male, ISpeciesData femaleSpecies,
        bool isRootSpecies = false, int depth = 0)
    {
        if (depth >= MaxEvolutionDepth) return false;

        // 降级目标：基础种族（BaseMother）/ PossiblyMothers 链条物种
        ISpeciesCoreData? baseCore = GetBaseMotherCore(femaleSpecies);
        List<ISpeciesData> lowerMothers = GetLowerMotherCandidates(femaleSpecies);
        bool canDegrade = baseCore != null || lowerMothers.Count > 0;

        // ① 同族（无降级目标的物种恒定同族，例如职业能力生成的 Golem）
        if (!canDegrade || !Rand(GetLowerBirthChance(femaleSpecies.Tier)))
        {
            female.GetPregnant(GetFakeHumanMaleForWomenBirth(male, femaleSpecies.InternalName));
            return true;
        }

        // ② 降级：基础种族优先（与链条物种 50/50）
        if (baseCore != null && (!lowerMothers.Any() || Random.Range(0, 2) == 0))
        {
            ERace savedRace = female.Race;
            female.Race = baseCore.Race;
            female.GetPregnant(GetFakeHumanMaleForWomenBirth(male, string.Empty));
            female.Race = savedRace;
            return true;
        }

        ISpeciesData lowerSpecies = lowerMothers[Random.Range(0, lowerMothers.Count)];
        return CheckEvolutionPregnancy(female, male, lowerSpecies, isRootSpecies: false, depth: depth + 1);
    }

    /// <summary>降级几率（%）：按品阶查表；未配置的品阶按 0（不降级）处理。</summary>
    private static int GetLowerBirthChance(int tier)
        => GameBalance.TierLowerEvoBirths.TryGetValue(tier, out int chance) ? chance : 0;

    /// <summary>基础种族（BaseMother）对应的核心种族数据；未配置或查不到时返回 null。</summary>
    private static ISpeciesCoreData? GetBaseMotherCore(ISpeciesData species)
    {
        if (species.BaseMother == null) return null;

        return GameData.SpeciesCoreDataList.FirstOrDefault(scd => scd.GetReplacedType == species.BaseMother);
    }

    /// <summary>
    /// 「降级」候选物种：PossiblyMothers 中不是自身者。
    /// ★ 不再按品阶过滤：PossiblyMothers 就是权威的降级链，允许同阶与更高阶
    ///   （例：Orc ← Goblin 同品阶 1、Dracolich ← Dragon 品阶 5 &gt; 4），
    ///   否则该类链条会断，基础种族永远生不出来；同时排除自身以避免死循环。
    /// </summary>
    private static List<ISpeciesData> GetLowerMotherCandidates(ISpeciesData species)
        => (species.PossiblyMothers ?? Array.Empty<string>())
            .Select(name => GameData.SpeciesDataList.FirstOrDefault(sd => sd.InternalName == name))
            .Where(sd => sd != null && sd.InternalName != species.InternalName)
            .Select(sd => sd!)
            .ToList();

    /// <summary>
    /// 受孕主流程（奴隶 × 父本）。单次尝试的优先级：
    ///   ⓪ <see cref="Tentacle"/> 父本：固定 100% 产出触手卵（与原版完全一致），
    ///      不参与下面任何随机分支；
    ///   ① <see cref="CheckNewSpeciesImpregnation"/>：进化物种女胎（按各物种 BaseChance 判定）；
    ///   ② <see cref="ReturnsSameOrLowerSpeciesFemale"/>：同族 / 降级物种（含基础种族）女胎
    ///      —— 人类父本维持原链，怪物父本仅 10%（<see cref="GameBalance.MonsterSameOrLowerFemaleChance"/>）；
    ///   ③ 其余情况生下真实父本的雄性后代（怪物男胎；大屌 / 马会一次消耗 2 次受孕机会）。
    /// </summary>
    private static bool GetPregnant(Character female, Character male, int times = 1)
    {
        // ★ 触手（Tentacle）：固定 100% 繁育触手卵，与原版一致。
        //   原版 Character.BeImpregnated 对触手父本走独立分支：只调用一次 NewFetus
        //   （PlayData.NewFetus → new Tentacle，其 Race = ERace.Tentacle；该个体出生在
        //   Baby 扇区后由原版逻辑自动转化为「触手卵」道具 Item_TentacleEgg），并且：
        //     · 不做多胎判定（固定 1 个）；
        //     · 受孕成功后女方 IsNotDraggable = true（生育时由原版 Birth 状态复位为 false）。
        //   ★ 本模组整体接管了 BeImpregnated，若不在此处提前返回，触手父本会继续走下面的
        //     「进化物种 / 同族降级物种（10%）/ 怪物男胎」随机分支，出现“和触手生出了其它种族”
        //     或一次受孕产出多个触手卵的情况 —— 这正是需要修复的 bug。
        if (male is Tentacle)
        {
            female.GetPregnant(male);
            female.IsNotDraggable = true;
            return true;
        }

        times = Random.Range((times < 2 || !male.IsBig) ? 1 : 2, times + 1);
        bool anyPregnant = false;
        int remaining = times;
        int iterations = 0;

        while (iterations < times && remaining != 0)
        {
            if (CheckNewSpeciesImpregnation(female, male))
            {
                anyPregnant = true;
                remaining--;
            }
            else if (ReturnsSameOrLowerSpeciesFemale(female, male))
            {
                anyPregnant = true;
                remaining--;
            }
            else if (male.IsBig || male is Horse)
            {
                anyPregnant = true;
                remaining -= remaining >= 2 ? 2 : 1;
                female.GetPregnant(male);
            }
            else
            {
                anyPregnant = true;
                remaining--;
                female.GetPregnant(male);
            }
            iterations++;
        }
        return anyPregnant;
    }

    // =========================================================================
    // 将物种应用到新生胎儿（NewFetus）
    // =========================================================================

    internal static void ApplyFetus(Character female, Character male, ref Unit result)
    {
        if (result is Male maleFetus)
        {
            // ★ 怪物男胎：种族完全由「本次繁育的父系种族」决定 —— 游戏在 NewFetus 中依据真实父本
            //   的类型（Goblin / Orc / Werewolf / Minotaur / Salamander / Horse / Tentacle）创建
            //   对应的雄性，与母亲的物种链、以及物种表 PossiblyFathers 里的其它怪物父系都无关。
            //   这里只做一条补充：父本自身若确实带有「物种身份」才继承（模组中只有雌性 / 访客会有），
            //   不再把 DisplayName（显示名，例如「哥布林」）当作物种名写进 SpeciesIdentity ——
            //   英文环境下显示名可能恰好等于物种 InternalName，会被误判成「进化物种」。
            string? fatherSpecies = GameData.GetSpeciesIdentity(male);
            if (!string.IsNullOrEmpty(fatherSpecies) && GameData.TryGetSpecies(fatherSpecies, out _))
            {
                GameData.SetSpeciesIdentity(maleFetus, fatherSpecies);
            }
            // 外观：男性后代保留原版骨架，仅做随机外观微调（ChangeMaleVisuals）
            ChangeMaleVisuals(maleFetus);
            SetTraitsAndProfession(maleFetus, mainParent: male, subParent: female);

            // 与女胎一致：写入职业后重新排序特质列表，
            // 保证职业特质显示在种族特质/进化种族特质之后、其它一般特质之前
            if (result is Character character)
            {
                character.SortTrait();
                character.Health = character.MaxHealth;
            }
        }
        else if (result is Female femaleFetus)
        {
            // 直接根据父本 DisplayName 判断是否应用进化物种
            ISpeciesData? speciesData = GameData.SpeciesDataList
                .FirstOrDefault(x => x.InternalName == male.DisplayName);

            if (speciesData != null)
            {
                femaleFetus.DisplayName = null;
                femaleFetus.SkinColorType = 0;

                if (!string.IsNullOrEmpty(speciesData.RigPrefix))
                {
                    // ... 换皮逻辑 ...
                }
                else
                {
                    femaleFetus.Race = female.Race;
                    ApplySpeciesColors(femaleFetus, female, speciesData);
                    ApplyAllStats(femaleFetus, speciesData);
                }

                GameData.SetSpeciesIdentity(femaleFetus, speciesData.InternalName);
                // ★ 独特服色初始值（天使=金 / 堕天使=深紫 等；仅在该胎还没有服色时写入）
                RaceBreedingExtension.UniqueLookColor.UniqueLookColorSystem.ApplySpeciesClothesColor(femaleFetus);
            }

            // ★ 移动到这里：无论是否进化物种，都调用职业继承
            SetTraitsAndProfession(femaleFetus, mainParent: female, subParent: male);

            // 公共收尾
            if (result is Character character)
            {
                character.SortTrait();
                character.Health = character.MaxHealth;
            }
        }
    }
    // --- 物种外观 ---

    private static void ApplySpeciesColors(Female fetus, Character mother, ISpeciesData species)
    {
        string? motherIdentity = GameData.GetSpeciesIdentity(mother);
        if (motherIdentity != species.InternalName)
        {
            fetus.SkinColor = SkinColorSystem.PickRandom(species.SkinColors);
            if (species.HairColors is { Length: > 0 })
                fetus.HairColor = Color.Lerp(fetus.HairColor, SkinColorSystem.PickRandom(species.HairColors),
                    Math.Min(1f, species.HairColorFocus + 0.3f));
            if (species.EyeColor.HasValue)
                fetus.EyeColor = Color.Lerp(fetus.EyeColor, species.EyeColor.Value,
                    Math.Min(1f, species.EyeColorFocus + 0.3f));
            if (species.ScaleColors != null)
                fetus.ScaleColor = SkinColorSystem.PickRandom(species.ScaleColors);
        }
        else
        {
            Color? hairTarget = species.HairColors is { Length: > 0 } ? species.HairColors[0] : (Color?)null;
            HairColorMigration(fetus, hairTarget, species.HairColors, species.HairColorFocus);
            Color[]? eyeBase = species.EyeColor.HasValue ? new[] { species.EyeColor.Value } : null;
            EyeColorMigration(fetus, species.EyeColor, eyeBase, species.EyeColorFocus);
        }
    }

    private static void ApplyCoreColors(Female fetus, ISpeciesCoreData core)
    {
        bool matchesCore = SkinColorSystem.IsInPalette(fetus.SkinColor, core.SkinColors);
        if (!matchesCore)
            fetus.SkinColor = SkinColorSystem.PickRandom(core.SkinColors);
        HairColorMigration(fetus, core.HairColorMigration, core.HairColors, core.HairColorMigrationPower);
    }

    private static void ApplyAllStats(Female fetus, ISpeciesStats stats)
    {
        fetus.SkinColorType = 0;
        fetus.Race = stats.Race;
        fetus.Figure = stats.Figure;
        fetus.EyeBallType = stats.EyeBallType;
        fetus.EarType = stats.EarType;
        fetus.EarHairType = stats.EarHairType;
        fetus.HornType = stats.HornType;
        fetus.HornDragonType = stats.HornDragonType;
        fetus.TailType = stats.TailType;
        fetus.TailDragonType = stats.TailDragonType;
        fetus.HairBodyType = stats.HairBodyType;
        fetus.UpperBodyType = stats.UpperBody;
        fetus.LowerBodyType = stats.LowerBody;

        fetus.DecreasingMentalityOnHunger = stats.MilkQuality;
        fetus.ConceptionRate = stats.ConceptionRate / 100f;
        fetus.MaxHealth = (float)stats.MaxHealth;
        fetus.GrowthTime = (float)stats.GrowthTime;
        fetus.MaxBirthCount = stats.MaxBirthCount;
        fetus.Maintenance = stats.Maintenance;
        fetus.DecreasingMentalityOnPregnant = stats.DecreasingMentalityOnPregnant;
        fetus.Health = fetus.MaxHealth;
    }

    // --- 外观随机变化 ---

    private static void ChangeFemaleVisuals(Female fetus)
    {
        fetus.PubicHairType = fetus.Data.GetInitialPubicHairType(fetus.DataType);
        if (Random.Range(1, 11) == 1)
        {
            int before = fetus.TitsType;
            int delta = Random.Range(0, 2) != 0 ? 1 : -1;
            fetus.TitsType += delta;
            PlayDataSystem.LogBirthBreastJitter(fetus, before, delta);
        }
        if (Random.Range(1, 11) <= 2)
            fetus.EyeType = Random.Range(1, 5);
    }

    private static void ChangeMaleVisuals(Male fetus)
    {
        fetus.PubicHairType = fetus.Data.GetInitialPubicHairType(fetus.DataType);
        if (Random.Range(0.01f, 1f) > 0.85f) fetus.PenisType = fetus.Data.GetInitialPenisType(fetus.DataType);
        if (Random.Range(0.01f, 1f) > 0.85f) fetus.UpperBodyType = fetus.Data.GetInitialUpperBodyType(fetus.DataType);
        if (Random.Range(0.01f, 1f) > 0.85f) fetus.BallsType = fetus.Data.GetInitialBallsType(fetus.DataType);
        if (Random.Range(0.01f, 1f) > 0.85f) fetus.EyeType = fetus.Data.GetInitialEyeType(fetus.DataType);
        if (Random.Range(0.01f, 1f) > 0.85f) fetus.EarType = fetus.Data.GetInitialEarType(fetus.DataType);
        if (Random.Range(0.01f, 1f) > 0.85f) fetus.HornType = fetus.Data.GetInitialHornType(fetus.DataType);
        if (Random.Range(0.01f, 1f) > 0.85f) fetus.BeardType = fetus.Data.GetInitialBeardType(fetus.DataType);
    }

    // --- 继承特质和职业（不含精华） ---
    // 注意：普通 Trait 的继承已由 InitializeTraitWithParentsPatch 处理，
    // 此方法仅负责添加物种保证特质（GuaranteedTraits）和职业继承。
    private static void SetTraitsAndProfession(Character child, Character mainParent, Character subParent)
    {
        // 添加物种保证特质（如 Ghoul 的 Trait108 等；血脉相关特质已迁移至血脉系统 Bloodline）
        if (GameData.TryGetSpecies(child, out ISpeciesData? childSpecies) && childSpecies?.GuaranteedTraits != null)
        {
            foreach (ETrait t in childSpecies.GuaranteedTraits)
                if (!child.TraitContains(t))
                    child.AddRaceTrait(t);
        }

        // --- 职业遗传（概率 = GameBalance.ProfessionInheritChance，75%）---
        //     雌性后代：从母亲继承职业；
        //     怪物（雄性）后代：继承父母双方中「怪物可遗传」的职业（优先父亲）。
        if (child is Female)
        {
            float motherProf = mainParent.GetTraitValue(GameBalance.ProfessionTraitId);
            if (motherProf > 0 && Random.Range(0f, 1f) < GameBalance.ProfessionInheritChance)
            {
                child.AddTrait(GameBalance.ProfessionTraitId);
                child.SetTraitValue(GameBalance.ProfessionTraitId, motherProf);
            }
        }
        else
        {
            // 怪物（雄性后代）只能继承以下“怪物可遗传”的职业：
            // Brigand / Assassin / Thief / Marauder / Plunderer / Trainer。
            // 若男女双方都拥有可遗传职业，则优先继承雄性（父亲）一方的职业；
            // 若父亲没有、母亲有，则怪物可继承母亲的该类职业。
            int inheritableId = 0;
            if (GameData.TryGetProfession(mainParent, out IProfessionData? fatherProfession)
                && fatherProfession != null
                && fatherProfession.MonsterInheritable)
            {
                inheritableId = fatherProfession.Id;
            }
            else if (GameData.TryGetProfession(subParent, out IProfessionData? motherProfession)
                     && motherProfession != null
                     && motherProfession.MonsterInheritable)
            {
                inheritableId = motherProfession.Id;
            }

            if (inheritableId > 0 && Random.Range(0f, 1f) < GameBalance.ProfessionInheritChance)
            {
                child.AddTrait(GameBalance.ProfessionTraitId);
                child.SetTraitValue(GameBalance.ProfessionTraitId, inheritableId);
            }
        }


        // --- 血脉（Bloodline，基于 ETrait.Trait9 映射，取代原高等种族特质） ---
        // 继承规则：父母双方同名血脉 → 继承并 50% 几率 +1；不同名 → 随机继承一方；
        //           随后由物种最低等级兜底（例如天使最低「神圣血脉+5」）。
        // 女胎与男胎统一调用（男胎由基础种族表兜底，使“怪物专属效果”可生效）。
        ApplyBloodline(child, mainParent, subParent);

        // 排序由外部调用执行（ApplyFetus 末尾已调用 SortTrait）
    }

    /// <summary>
    /// 血脉分配：遗传自母亲（mainParent）与父亲（subParent），再套用子代物种的最低等级。
    /// 遗传规则见 BloodlineSystem.ApplyInheritedBloodline。
    ///
    /// ★ 传入的亲本会先还原为「真实亲本」：<see cref="GetFakeHumanMaleForWomenBirth"/> 会把
    ///   怪物 / 玩家 / 访客包装成临时 Client（假人类父本），而 BloodlineSystem 以「父本是否
    ///   Client」区分人类客户与怪物 / 雄性 —— 不还原会导致：
    ///     · 女性奴隶 × 人类客户：客户的血脉数据读不到，客户血脉规则全部失效；
    ///     · 母亲 × 怪物：怪物被误判为人类客户（异血脉时不再 50/50，也不再 +1）。
    /// </summary>
    private static void ApplyBloodline(Character child, Character mainParent, Character subParent)
        => RaceBreedingExtension.Bloodline.BloodlineSystem.ApplyInheritedBloodline(
            child, ResolveRealParent(mainParent), ResolveRealParent(subParent));

    /// <summary>
    /// 把「假人类父本」还原为生成它的真实亲本（派系访客 Client / 玩家 Player / 怪物等），
    /// 非假父本原样返回。允许链式包装（限制追溯深度，避免异常数据造成死循环）。
    /// </summary>
    private static Character ResolveRealParent(Character parent)
    {
        for (int depth = 0; depth < 4 && parent is Client fake; depth++)
        {
            if (!FakeFatherOrigins.TryGetValue(fake, out Character? origin) || origin == null)
                break;
            parent = origin;
        }
        return parent;
    }

    // --- 颜色工具 ---

    private static void HairColorMigration(Character c, Color? target, Color[]? baseColors, float? power)
    {
        if (power is float p && p > 0f)
        {
            if (target.HasValue) { c.HairColor = Color.Lerp(c.HairColor, target.Value, p); return; }
            if (baseColors?.Length > 0) c.HairColor = Color.Lerp(c.HairColor, baseColors[0], p);
        }
    }

    private static void EyeColorMigration(Character c, Color? target, Color[]? baseColors, float? power)
    {
        if (power is float p && p > 0f)
        {
            if (target.HasValue) { c.EyeColor = Color.Lerp(c.EyeColor, target.Value, p); return; }
            if (baseColors?.Length > 0) c.EyeColor = Color.Lerp(c.EyeColor, baseColors[0], p);
        }
    }

    /// <summary>
    /// 创建一个虚拟的客户端父亲，用于传递给 <c>Female.GetPregnant</c>。
    /// ★ 创建后会登记「假父本 → 真实父本」（见 <see cref="FakeFatherOrigins"/>），
    ///   以便血脉遗传时按真实父本的类型（人类客户 / 怪物·雄性）选择规则。
    /// </summary>
    private static Client GetFakeHumanMaleForWomenBirth(Character male, string? speciesName)
    {
        var client = new Client(male.UnitId)
        {
            EyeColor = male.EyeColor,
            HairColor = male.SkinColor,
            SkinColor = male.SkinColor,
            DisplayName = speciesName ?? string.Empty
        };

        // ★ 记录真实父本（派系访客 Client / 玩家 Player / 怪物）：血脉遗传需要按真实父本判定
        //   是「人类客户」还是「怪物 / 雄性」，否则客户血脉取不到（规则全部失效）、
        //   怪物父本还会被这个包装误判成人类客户。
        if (!FakeFatherOrigins.TryGetValue(client, out _))
            FakeFatherOrigins.Add(client, ResolveRealParent(male));

        client.OnEnableTrait();
        client.ClearRaceTrait();
        client.ClearTrait();

        // 继承父亲拥有的普通特质（非精华）
        foreach (ITraitData traitData in GameData.TraitsDataList)
        {
            if (male.TraitContains(traitData.ReplacedTrait))
                client.AddRaceTrait(traitData.ReplacedTrait);
        }

        // 继承父亲拥有的普通特质（Trait93~Trait100 等）——
        // 若只把父亲的属性写进 RaceTrait 列表，子代继承时读取的是 TraitInfoDictionary，
        // 会取不到父亲属性，导致“普通属性只继承母亲”。这里同时写入普通特质。
        // 注意：职业特质与血脉（Trait9）不在普通特质继承范围内 ——
        //       职业由 SetTraitsAndProfession 单独处理；血脉由 BloodlineSystem 依据
        //       「真实父本」（FakeFatherOrigins 还原）判定，无需复制到假父本上。
        foreach (TraitInfo info in GetTraitInfoDict(male).Values)
        {
            if (info.Trait == GameBalance.ProfessionTraitId || info.Trait == (ETrait)9)
                continue;
            client.AddTrait(info.Trait);
            client.AddTraitValue(info.Trait, info.Value);
        }

        // Keep the father's profession trait on the fake father so the next generation
        // of monster (male) offspring can still inherit the 6 monster-inheritable professions.
        float fatherProfession = male.GetTraitValue(GameBalance.ProfessionTraitId);
        if (fatherProfession > 0)
        {
            client.AddTrait(GameBalance.ProfessionTraitId);
            client.SetTraitValue(GameBalance.ProfessionTraitId, fatherProfession);
        }
        return client;
    }
}