#nullable enable
using ComplexBreedingRedux.Data;
using ComplexBreedingRedux.Data.Species;
using ComplexBreedingRedux.Features;
using ComplexBreedingRedux.Properties;
using DepravedShield.Helpers;
using MBM.ModLoader.Core;
using MBM.ModLoader.Settings;
using MBMScripts;
using RaceBreedingExtension.Bloodline.Data;
using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RaceBreedingExtension.Bloodline;

/// <summary>
/// 血脉（Bloodline）系统核心 —— 取代原高等种族特质（HighRaceTrait，已整体移除）。
///
/// 存储介质（与职业系统相同的「单 Trait 映射」思路）：一般特质槽位 ETrait.Trait9。
///   值 = 血脉 ID × 1000 + 等级（恶魔血脉+3 = 1003，龙之血脉+10 = 8010）。
///   因此每个单位最多只有 1 种血脉（等级 1~10）。旧 HighRaceTrait 的值（191~201）
///   会被判定为非法编码并自动清除（见 MigrateLegacy）。
///
/// 取得途径：
///   - 女性（雌性奴隶 / 进化物种）：物种最低等级（ApplySpeciesBloodline）+ 遗传；
///   - 怪物（雄性）：仅由遗传时生成（ApplyInheritedBloodline）；
///   - 派系访客（Client）：按所属派系随机（FactionSystem.ApplyVisitorBloodline），
///     等级只由「模组派系声誉等级」决定（Unknown 1-3 / Obscure 1-4 / … / Fabled 1-10；
///     ★ 不参考原版声望等级、不受访客品阶影响）。
///
/// 数值效果：
///   - 每级属性（SexTime / DecreasingMentalityOnSex / Maintenance / GrowthTime / ConceptionRate）
///     以「写入原版特质槽位」的方式实现（见 BloodlineVanillaStats），女性奴隶与怪物（雄性）
///     都会写入，是否生效与上下限完全由原版游戏逻辑决定；
///     ★ 数值随存档保存，因此平衡调整（改每级数值）后旧存档角色需要在首次访问时
///       做一次性换算（<see cref="EnsureStatVersion"/> + <see cref="StatVersion"/>）；
///   - 防护结界（DepravedShield）是模组自有系统，仅女性生效，在
///     SexProgressManager.GetDepravedShield 中叠加；
///   - 堕落时高潮掉落物品在 SexProgressManager.CheckAndHandleOrgasm 中调用 OnOrgasm；
///   - 怪物（雄性持有者）专属效果在 BloodlineMonsterCumshotPatch 检测到 cumshot 时
///     调用 OnMonsterCumshot。
/// </summary>
internal static class BloodlineSystem
{
    /// <summary>存储槽位：一般特质 ETrait.Trait9（值 = 血脉编码）。</summary>
    public const ETrait SlotTrait = (ETrait)9;

    /// <summary>血脉最低等级。</summary>
    public const int MinLevel = 1;

    /// <summary>血脉最高等级。</summary>
    public const int MaxLevel = 10;

    /// <summary>
    /// 后代血脉等级 +1 的几率（0.5 = 50%）。仅在下列「父母双方都有血脉」的情形才会掷骰：
    ///   - 母亲 × 怪物 / 雄性：双方都有血脉（同种 → 取双方较高等级；异种 → 50/50 取一方）；
    ///   - 女性奴隶 × 人类客户（<see cref="Client"/>）：双方血脉**相同**（取双方较高等级，双方都可继承）。
    /// 其它情形（客户血脉与母亲不同 / 仅一方有血脉 / 双方都无血脉）一律不 +1。
    /// 上限为 <see cref="MaxLevel"/>。
    /// </summary>
    public const float LevelUpChance = 0.5f;

    private static bool _initialized;

    /// <summary>由 RaceBreedingExtension.ModEntry.Load() 调用。</summary>
    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        try
        {
            Strings.Culture = Localization.CurrentCulture;
            Localization.OnLanguageChanged += _ => OnLanguageChanged();
            RefreshLocalizedStrings();

            Debug.Log("[Bloodline] system initialized.");
        }
        catch (Exception e)
        {
            Debug.LogError("[Bloodline] initialize failed: " + e);
        }
    }

    private static void OnLanguageChanged()
    {
        try
        {
            Strings.Culture = Localization.CurrentCulture;
            RefreshLocalizedStrings();
        }
        catch (Exception e)
        {
            Debug.LogError("[Bloodline] language change failed: " + e);
        }
    }

    /// <summary>刷新本地化名称与风味描述。</summary>
    public static void RefreshLocalizedStrings()
    {
        foreach (BloodlineData data in BloodlineRegistry.All)
        {
            string key = "Bloodline_" + data.InternalName;
            data.Name = Loc(key + "_Name");
            data.Description = Loc(key + "_Desc");
        }
    }

    // ─── 编码 / 解码 ──────────────────────────────────────────────────────────

    /// <summary>编码：值 = 血脉 ID × 1000 + 等级（如 龙之血脉+3 = 8003）。</summary>
    public static int Encode(EBloodline bloodline, int level)
        => (int)bloodline * 1000 + Mathf.Clamp(level, MinLevel, MaxLevel);

    /// <summary>解码 Trait9 上的原始值（非法编码返回 false）。</summary>
    public static bool TryDecode(float rawValue, out BloodlineData? data, out int level)
    {
        data = null;
        level = 0;

        int value = Mathf.RoundToInt(rawValue);
        if (value <= 0)
            return false;

        EBloodline id = (EBloodline)(value / 1000);
        int lv = value % 1000;
        if (id == EBloodline.None || lv < MinLevel || lv > MaxLevel)
            return false;

        data = BloodlineRegistry.GetById(id);
        if (data == null)
            return false;

        level = lv;
        return true;
    }

    // ─── 查询 ─────────────────────────────────────────────────────────────────

    /// <summary>读取角色当前血脉（无 / 编码非法返回 null）。</summary>
    public static BloodlineData? TryGetBloodline(Character? character, out int level)
    {
        level = 0;
        if (character == null || !character.TraitContains(SlotTrait))
            return null;
        return TryDecode(character.GetTraitValue(SlotTrait), out BloodlineData? data, out level) ? data : null;
    }

    /// <summary>角色是否拥有血脉。</summary>
    public static bool HasBloodline(Character? character)
        => TryGetBloodline(character, out _) != null;

    /// <summary>角色是否拥有指定血脉（返回其等级，0 = 没有）。</summary>
    public static int GetLevel(Character? character, EBloodline bloodline)
    {
        BloodlineData? data = TryGetBloodline(character, out int level);
        return data != null && data.Id == bloodline ? level : 0;
    }

    // ─── 设置 / 清除 ──────────────────────────────────────────────────────────

    /// <summary>写入血脉（会覆盖 Trait9 上的旧值），并同步原版属性特质（先移除旧等级、再写入新等级）。</summary>
    public static void SetBloodline(Character? character, EBloodline bloodline, int level)
    {
        if (character == null) return;

        // 数值版本迁移：旧存档里的旧数值先换算成当前数值，之后再按新等级重算
        EnsureStatVersion(character);

        // 旧血脉（用于移除其原版属性加成）
        BloodlineData? oldData = TryDecode(character.GetTraitValue(SlotTrait), out BloodlineData? decodedOld, out int oldLevel)
            ? decodedOld
            : null;

        BloodlineData? data = BloodlineRegistry.GetById(bloodline);
        if (data == null)
        {
            ClearBloodline(character);
            return;
        }

        int clamped = Mathf.Clamp(level, MinLevel, MaxLevel);

        BloodlineVanillaStats.Apply(character, oldData, oldLevel, -1);

        if (!character.TraitContains(SlotTrait))
            character.AddTrait(SlotTrait);
        character.SetTraitValue(SlotTrait, Encode(bloodline, clamped));

        BloodlineVanillaStats.Apply(character, data, clamped, +1);
        MarkStatVersion(character);

        LogDebug($"[Bloodline] {character.DisplayName} 血脉已写入 → {data.Name}+{clamped}（Trait9 = {Encode(bloodline, clamped)}）");
    }

    /// <summary>清除血脉（移除 Trait9 槽位，并回收其原版属性加成）。</summary>
    public static void ClearBloodline(Character? character)
    {
        if (character == null) return;

        // 数值版本迁移：保证下面「按当前数值移除」的量与角色身上实际写入的量一致
        EnsureStatVersion(character);

        if (TryDecode(character.GetTraitValue(SlotTrait), out BloodlineData? data, out int level))
            BloodlineVanillaStats.Apply(character, data, level, -1);

        if (character.TraitContains(SlotTrait))
            character.RemoveTrait(SlotTrait);
    }

    /// <summary>提升等级（不超过 MaxLevel；无血脉时不生效），并同步原版属性特质。</summary>
    public static void RaiseLevel(Character? character, int level)
    {
        BloodlineData? data = TryGetBloodline(character, out int current);
        if (character == null || data == null)
            return;

        // 数值版本迁移：保证下面「按旧等级移除」的量与角色身上实际写入的量一致
        EnsureStatVersion(character);

        int target = Mathf.Clamp(level, MinLevel, MaxLevel);
        if (target <= current)
            return;

        BloodlineVanillaStats.Apply(character, data, current, -1);
        character.SetTraitValue(SlotTrait, Encode(data.Id, target));
        BloodlineVanillaStats.Apply(character, data, target, +1);
        MarkStatVersion(character);

        LogDebug($"[Bloodline] {character.DisplayName} 血脉等级提升 → {data.Name}+{target}");
    }

    /// <summary>旧存档迁移：Trait9 上的旧 HighRaceTrait 值（191~201）等非法编码一律清除。</summary>
    public static void MigrateLegacy(Character? character)
    {
        if (character == null || !character.TraitContains(SlotTrait))
            return;
        if (!TryDecode(character.GetTraitValue(SlotTrait), out _, out _))
            ClearBloodline(character);
    }

    // ─── 每级数值的版本迁移（平衡调整对旧存档立即生效） ────────────────────────

    /// <summary>
    /// 当前「每级数值」的版本号。★ 每次修改 <see cref="BloodlineRegistry"/> 里的每级数值都必须 +1，
    /// 并把改动前的数值补录进 <see cref="BloodlineStatHistory"/>。
    /// 版本 1（隐含，未记录）＝ 平衡调整前的数值。
    /// </summary>
    public const int StatVersion = 2;

    /// <summary>本会话内已确认过数值版本的角色（UnitId → 版本），避免每次都读存档。</summary>
    private static readonly Dictionary<int, int> _statVersionCache = new Dictionary<int, int>();

    /// <summary>角色数值版本的存档键（写在 MBM.ModLoader 的 ModSaveData 里，按存档槽位隔离）。</summary>
    private static string StatVersionKey(int unitId) => "BloodlineStatVer_" + unitId;

    /// <summary>
    /// 旧存档数值迁移（每个角色只执行一次）：
    /// 每级属性写进的是原版特质槽位、随存档保存，因此平衡调整后旧存档里的角色仍带着旧数值。
    /// 这里按角色记录「上次写入数值时的版本」，首次发现版本落后时把旧数值换算成新数值
    /// （<see cref="BloodlineVanillaStats.RealignToCurrentVersion"/>），并把新版本号写回存档；
    /// 新角色由 <see cref="MarkStatVersion"/> 在写入属性时直接标记为当前版本，因此不会被重复换算。
    /// </summary>
    public static void EnsureStatVersion(Character? character)
    {
        if (character == null) return;

        int unitId = character.UnitId;
        if (_statVersionCache.TryGetValue(unitId, out int cached) && cached >= StatVersion)
            return;

        try
        {
            int applied = ModSaveData.GetInt(ModEntry.ModName, StatVersionKey(unitId), 0);

            if (applied < StatVersion)
            {
                if (TryGetBloodline(character, out int level) is BloodlineData data && level > 0)
                    BloodlineVanillaStats.RealignToCurrentVersion(character, data, level, applied);

                ModSaveData.SetInt(ModEntry.ModName, StatVersionKey(unitId), StatVersion);
                LogDebug($"[Bloodline] {character.DisplayName} 血脉属性数值已由 v{applied} 换算为 v{StatVersion}");
            }

            _statVersionCache[unitId] = StatVersion;
        }
        catch (Exception e)
        {
            // 迁移失败不影响游戏：只记日志、不缓存结果，下次访问再试
            Debug.LogError("[Bloodline] 数值版本迁移失败: " + e);
        }
    }

    /// <summary>把角色标记为「已按当前版本写入属性」（仅在刚写完属性后调用）。</summary>
    private static void MarkStatVersion(Character? character)
    {
        if (character == null) return;

        _statVersionCache[character.UnitId] = StatVersion;

        try
        {
            ModSaveData.SetInt(ModEntry.ModName, StatVersionKey(character.UnitId), StatVersion);
        }
        catch (Exception e)
        {
            Debug.LogError("[Bloodline] 数值版本写入失败: " + e);
        }
    }

    // ─── 物种分配 ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 按物种补发血脉（市场、繁殖、成长等所有物种确认路径的统一入口）。
    /// 规则：
    ///   1) 旧存档迁移（任何角色）：Trait9 上无法解码的旧 HighRaceTrait 值一律清除；
    ///   2) 只有女性（雌性奴隶 / 进化物种）按物种获得血脉（SpeciesBloodlineMap 的物种表），
    ///      物种等级按「最低等级」处理：
    ///        - 无血脉 → 直接获得物种血脉（物种最低等级）；
    ///        - 同名血脉 → 等级不足则提升到物种最低等级；
    ///        - 不同名血脉 → 物种最低等级 ≥ 当前等级时改为物种血脉（物种身份优先），
    ///                        否则保留已有的更高等级血脉，避免培养成果被降级；
    ///   3) 怪物（雄性）的血脉只在遗传时生成（见 ApplyInheritedBloodline），不会在此直接添加。
    /// </summary>
    public static void ApplySpeciesBloodline(Character? character)
    {
        if (character == null) return;

        // 1) 旧存档迁移（非法编码清理 + 每级数值的版本换算）
        MigrateLegacy(character);
        EnsureStatVersion(character);

        // 2) 仅女性按物种补发
        if (!(character is Female))
            return;

        if (!GameData.TryGetSpecies(character, out ISpeciesData? species) || species == null)
            return;

        if (!SpeciesBloodlineMap.TryGetBySpeciesName(species.InternalName, out EBloodline speciesBloodline, out int speciesMinLevel))
            return;

        BloodlineData? current = TryGetBloodline(character, out int currentLevel);
        ApplyEvolutionSpeciesRule(character, current, currentLevel, speciesBloodline, speciesMinLevel);
    }

    /// <summary>进化物种的血脉规则（物种身份 + 最低等级，见 ApplySpeciesBloodline 说明）。</summary>
    private static void ApplyEvolutionSpeciesRule(
        Character character, BloodlineData? current, int currentLevel, EBloodline speciesBloodline, int speciesMinLevel)
    {
        if (current == null)
        {
            SetBloodline(character, speciesBloodline, speciesMinLevel);
            return;
        }

        if (current.Id == speciesBloodline)
        {
            RaiseLevel(character, speciesMinLevel);
            return;
        }

        if (speciesMinLevel >= currentLevel)
            SetBloodline(character, speciesBloodline, speciesMinLevel);
    }

    // ─── 遗传 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// 后代血脉遗传（取代原 HighRaceTrait 的继承逻辑），女胎与男胎（怪物）统一适用：
    ///   - ★ 女性奴隶 × 人类客户（<see cref="Client"/>：派系访客 / 假人类父本）：
    ///       · 母亲与客户血脉**相同** → 继承该血脉，等级取双方较高值（双方都可继承），并参与 50% +1；
    ///       · 血脉**不同**（或客户本身无血脉）→ 仅继承**母亲**一方血脉（母亲等级，不 +1）；
    ///   - 非客户父本、双方均有血脉且为同一种 → 继承该血脉，等级取双方较高值；
    ///   - 非客户父本、双方血脉不同 → 随机继承其中一方（等级同步该方）；
    ///   - 仅一方有血脉 → 继承该方血脉；
    ///   - 双方都没有 → 无血脉（女胎随后由物种最低等级补发；怪物不会直接获得血脉）。
    /// ★ 仅当「父母双方都有血脉」（客户情形下还需双方血脉相同）时，才按
    ///   <see cref="LevelUpChance"/>（50%）掷骰 +1。
    /// 最后对女胎应用物种最低等级（ApplySpeciesBloodline 对雄性不生效）。
    /// ★ 传入的亲本必须是「真实亲本」：受孕流程会把怪物 / 玩家 / 访客统一包装成临时
    ///   <see cref="Client"/>（假人类父本），调用方需先还原（BreedingSystem.ApplyBloodline），
    ///   否则怪物 / 雄性会被当成人类客户（血脉不同时不再 50/50，也不再 +1）。
    /// </summary>
    public static void ApplyInheritedBloodline(Character? child, Character? firstParent, Character? secondParent)
    {
        if (child == null) return;

        // 母亲 = 双亲中的女性一方（女胎传入顺序为 (母, 父)，男胎为 (父, 母)，故按性别判定避免顺序歧义）
        Character? mother = firstParent is Female
            ? firstParent
            : (secondParent is Female ? secondParent : firstParent);
        Character? father = ReferenceEquals(mother, firstParent) ? secondParent : firstParent;

        BloodlineData? motherData = TryGetBloodline(mother, out int motherLevel);
        BloodlineData? fatherData = TryGetBloodline(father, out int fatherLevel);

        // ★ 人类客户作为父本：派系访客（Client）或玩家（Player，人类雄性）。
        //   注意：这里收到必须是「真实父本」——受孕流程会把怪物 / 玩家 / 访客统一包装成
        //   临时 Client（假人类父本），调用方 BreedingSystem.ApplyBloodline 负责还原；
        //   否则怪物 / 雄性会被误判为人类客户，第 4 / 5 条规则（50/50 + 50% +1）会失效。
        bool fatherIsClient = father is Client || father is Player;

        EBloodline inherited = EBloodline.None;
        int level = 0;
        bool levelUpEligible = false;   // 仅「父母双方都有血脉」（客户情形下还需血脉相同）才允许 50% +1

        if (fatherIsClient)
        {
            if (motherData != null && fatherData != null && motherData.Id == fatherData.Id)
            {
                // 血脉相同 → 继承该血脉，等级取双方较高值（双方都可继承），参与 50% +1
                inherited = motherData.Id;
                level = Mathf.Max(motherLevel, fatherLevel);
                levelUpEligible = true;
            }
            else if (motherData != null)
            {
                // 血脉不同（或客户无血脉）→ 仅继承母亲一方血脉，不 +1
                inherited = motherData.Id;
                level = motherLevel;
            }
            // 母亲无血脉：忽略客户血脉（之后由物种最低等级兜底）
        }
        else if (motherData != null && fatherData != null)
        {
            if (motherData.Id == fatherData.Id)
            {
                inherited = motherData.Id;
                level = Mathf.Max(motherLevel, fatherLevel);
            }
            else if (Random.Range(0f, 1f) < 0.5f)
            {
                inherited = motherData.Id;
                level = motherLevel;
            }
            else
            {
                inherited = fatherData.Id;
                level = fatherLevel;
            }
            levelUpEligible = true;
        }
        else if (motherData != null)
        {
            inherited = motherData.Id;
            level = motherLevel;
        }
        else if (fatherData != null)
        {
            inherited = fatherData.Id;
            level = fatherLevel;
        }

        if (inherited != EBloodline.None)
        {
            // ★ 50% 概率 +1（仅双方都有血脉的情形）
            if (levelUpEligible && Random.Range(0f, 1f) < LevelUpChance)
                level++;

            SetBloodline(child, inherited, Mathf.Clamp(level, MinLevel, MaxLevel));
        }

        // 物种最低等级（可能覆盖 / 提升）
        ApplySpeciesBloodline(child);

        // 诊断：开启模组日志 / 调试模式时打印判定依据（便于核对「人类客户」血脉规则）
        LogInheritance(child, mother, father, motherData, motherLevel, fatherData, fatherLevel,
            fatherIsClient, levelUpEligible, inherited, level);
    }

    // ─── 派系声望效率（堕落时的派系营业声望获取速度，本轮新增） ──────────────

    /// <summary>
    /// 血脉每级提供的「堕落时派系营业声望获取速度」加成：+1 → 1.5 倍、+10 → 6 倍。
    /// </summary>
    public const float FactionReputationSpeedPerLevel = 0.5f;

    /// <summary>
    /// 堕落状态下，该等级血脉为「其所属派系」提供的营业声望获取倍率 = 1 + 0.5 × 等级
    /// （+1 → 1.5 倍、+10 → 6 倍）。
    /// ★ 与公主（Princess）职业的 ×10 为「相乘」（血脉 +10 且公主时 = 6 × 10 = 60 倍），
    ///   见 ComplexBreedingRedux.Features.FactionSystem.CombineFactionReputationMultiplier。
    /// </summary>
    public static float GetFactionReputationSpeedMultiplier(int level)
        => 1f + FactionReputationSpeedPerLevel * Mathf.Clamp(level, 0, MaxLevel);

    // ─── 数值效果 ─────────────────────────────────────────────────────────────

    /// <summary>防护结界 DepravedShield 加成（仅女性生效；每级 ShieldPerLevel 点）。</summary>
    public static int GetShieldBonus(Character? character)
    {
        if (!(character is Female))
            return 0;
        return TryGetBloodline(character, out int level) is BloodlineData data ? data.ShieldPerLevel * level : 0;
    }

    /// <summary>
    /// 怪物（雄性）血脉加成：与其在 EState.Sex / EState.SexDrained 场景中的女性，
    /// GetSexProgressSpeed 的 speed 提升量（0 = 无）。
    /// </summary>
    public static float GetPartnerSexSpeedBonus(Character? female)
    {
        if (female == null) return 0f;
        if (female.State != EState.Sex && female.State != EState.SexDrained) return 0f;

        Character? partner = female.Partner;
        if (!(partner is Male)) return 0f;

        BloodlineData? data = TryGetBloodline(partner, out int level);
        if (data == null || data.MonsterEffect != EMonsterBloodlineEffect.PartnerSexSpeed)
            return 0f;
        return data.MonsterEffectValue * level;
    }

    /// <summary>
    /// 高潮时效果：堕落状态（IsDepraved）下按 5% × 等级 的几率获得对应物品
    /// （怪物血脉不要求堕落状态）。
    /// 由 DepravedShield 的 SexProgressManager.CheckAndHandleOrgasm 调用。
    /// </summary>
    public static void OnOrgasm(Character? character)
    {
        try
        {
            if (character == null) return;
            BloodlineData? data = TryGetBloodline(character, out int level);
            if (data == null || data.OrgasmItem == null) return;
            if (data.OrgasmRequiresDepraved && !character.IsDepraved) return;

            int chance = Mathf.Clamp(5 * level, 0, 100);
            if (Random.Range(1, 101) > chance) return;

            GrantItem(character, data.OrgasmItem.Value, 1);
        }
        catch (Exception e)
        {
            Debug.LogError("[Bloodline] OnOrgasm failed: " + e);
        }
    }

    /// <summary>
    /// 怪物（雄性持有者）触发 cumshot 时的血脉效果：
    /// 防护结界削减 / 特质升级几率 / 体力恢复几率，全部按怪物血脉等级叠加。
    /// 由 DepravedShield 的 SexProgressPatch（Male.Sex Postfix）调用。
    /// </summary>
    public static void OnMonsterCumshot(Character? monster, Character? female)
    {
        try
        {
            if (monster == null || female == null) return;
            BloodlineData? data = TryGetBloodline(monster, out int level);
            if (data == null || data.MonsterEffect == EMonsterBloodlineEffect.None) return;

            switch (data.MonsterEffect)
            {
                case EMonsterBloodlineEffect.PartnerShieldDrain:
                    {
                        int amount = Mathf.RoundToInt(data.MonsterEffectValue * level);
                        if (amount > 0)
                        {
                            SexProgressManager.ReduceDepravedShield(female, amount);
                            female.PopUpMessage(string.Format(Loc("Bloodline_Popup_ShieldDrain"), data.Name, amount), null, 0.25f);
                        }
                        break;
                    }
                case EMonsterBloodlineEffect.PartnerTraitUpgrade:
                    {
                        int chance = Mathf.Clamp(Mathf.RoundToInt(data.MonsterEffectValue * level), 0, 100);
                        if (chance > 0 && Random.Range(1, 101) <= chance)
                        {
                            female.UpgradeTrait();
                            female.PopUpMessage(Loc("Bloodline_Popup_Upgrade"), null, 0.25f);
                        }
                        break;
                    }
                case EMonsterBloodlineEffect.PartnerStaminaRestore:
                    {
                        int chance = Mathf.Clamp(Mathf.RoundToInt(data.MonsterEffectValue * level), 0, 100);
                        if (chance > 0 && Random.Range(1, 101) <= chance)
                        {
                            female.Health = Mathf.Min(female.Health + female.MaxHealth * 0.5f, female.MaxHealth);
                            female.PopUpMessage(Loc("Bloodline_Popup_Stamina"), null, 0.25f);
                        }
                        break;
                    }
            }
        }
        catch (Exception e)
        {
            Debug.LogError("[Bloodline] OnMonsterCumshot failed: " + e);
        }
    }

    /// <summary>把血脉掉落物品放入玩家库存并弹窗提示。</summary>
    private static void GrantItem(Character character, EItemType item, int count)
    {
        GameManager.Instance.PlayerData.NewItem(
            item, ESector.Inventory, new ValueTuple<int, int>(GetInventoryGroup(item), 0), -1, count, true);

        string itemName = LocalizationSystem.GetLocItemName(item);
        character.PopUpMessage(string.Format(Loc("Bloodline_Popup_Milk"), itemName), null, 0.25f);
    }

    /// <summary>原版库存分栏（与 ProfessionSystem.GetInventoryGroup 一致：乳汁/药丸等消耗品 = 1）。</summary>
    private static int GetInventoryGroup(EItemType item) => 1;

    // ─── 界面显示 ─────────────────────────────────────────────────────────────

    /// <summary>血脉名称显示文本（带血脉颜色与等级，例如「龙之血脉+3」）。</summary>
    public static string GetDisplayName(Character? character)
    {
        BloodlineData? data = TryGetBloodline(character, out int level);
        return data == null ? string.Empty : GetDisplayName(data, level);
    }

    /// <summary>按 Trait9 原始值生成显示名（供本地化键 #Bloodline_&lt;value&gt; 映射）。</summary>
    public static string GetDisplayNameByValue(int rawValue)
        => TryDecode(rawValue, out BloodlineData? data, out int level) && data != null
            ? GetDisplayName(data, level)
            : string.Empty;

    /// <summary>血脉描述显示文本（含当前等级、每级效果与怪物专属效果；交配时间/体力消耗取该角色 Trait99/Trait100 的实际值）。</summary>
    public static string GetDescription(Character? character)
    {
        // 先做数值版本迁移，保证下面显示的 Trait99 / Trait100 实际值是最新的
        EnsureStatVersion(character);

        BloodlineData? data = TryGetBloodline(character, out int level);
        return data == null ? string.Empty : GetDescription(data, level, character);
    }

    /// <summary>
    /// 按 Trait9 原始值生成描述（供本地化键 #BloodlineText_&lt;value&gt; 映射）。
    /// 该路径没有角色对象，因此「交配时间 / 体力消耗量」只显示血脉自身的加成（不含角色的其它来源）。
    /// </summary>
    public static string GetDescriptionByValue(int rawValue)
        => TryDecode(rawValue, out BloodlineData? data, out int level) && data != null
            ? GetDescription(data, level, character: null)
            : string.Empty;

    /// <summary>名称：血脉颜色 + 名称 + 「+等级」。</summary>
    private static string GetDisplayName(BloodlineData data, int level)
        => "<color=" + data.ColorHex + ">" + data.Name + "+" + level + "</color>";

    /// <summary>
    /// 描述排版（参考 ComplexBreedingRedux 精华系统）：
    /// 风味文本 → 当前等级 → 每级效果（含当前等级总值）→ 怪物效果
    /// （怪物效果区块固定包含「交配时间 / 体力消耗量」两行，见 <see cref="BuildTraitValueLines"/>）。
    /// </summary>
    private static string GetDescription(BloodlineData data, int level)
        => GetDescription(data, level, character: null);

    private static string GetDescription(BloodlineData data, int level, Character? character)
    {
        string text = data.Description ?? string.Empty;
        text += string.Format("<size={0}>", GameColors.DescriptionFontSize);
        text += "<BR><color=" + GameColors.BoldColorHex + ">"
              + string.Format(Loc("Bloodline_Tooltip_CurrentLevel"), "+" + level) + "</color>";

        string lines = string.Empty;

        // 注：交配时间（Trait99）与体力消耗量（Trait100）两行已移到下面的「怪物效果」区块，
        //     以免被原版特质面板的 8 格上限挤掉而看不到（仅 UI 位置变化，实际效果不变）。

        if (data.FoodConsumptionPerLevel != 0)
            lines += string.Format("<BR>- " + Loc("Bloodline_Line_Food"),
                Signed(data.FoodConsumptionPerLevel), Signed(data.FoodConsumptionPerLevel * level));

        if (data.GrowthTimePerLevel != 0)
            lines += string.Format("<BR>- " + Loc("Bloodline_Line_Growth"),
                Signed(data.GrowthTimePerLevel), Signed(data.GrowthTimePerLevel * level));

        if (data.ConceptionRatePerLevel != 0f)
            lines += string.Format("<BR>- " + Loc("Bloodline_Line_Conception"),
                SignedPercent(data.ConceptionRatePerLevel), SignedPercent(data.ConceptionRatePerLevel * level));

        if (data.ShieldPerLevel != 0)
            lines += string.Format("<BR>- " + Loc("Bloodline_Line_Shield"),
                Signed(data.ShieldPerLevel), Signed(data.ShieldPerLevel * level));

        // ★ 堕落时「血脉所属派系」的营业声望获取速度（每级 +0.5 → +1 = 1.5 倍、+10 = 6 倍）
        var bloodlineFaction = FactionSystem.GetFactionForBloodline(data.Id);
        if (bloodlineFaction != null)
        {
            lines += string.Format("<BR>- " + Loc("Bloodline_Line_FactionReputation"),
                bloodlineFaction.Name, GetFactionReputationSpeedMultiplier(level).ToString("0.##"));
        }

        if (data.OrgasmItem != null)
        {
            string itemName = LocalizationSystem.GetLocItemName(data.OrgasmItem.Value);
            string key = data.OrgasmRequiresDepraved ? "Bloodline_Line_MilkDepraved" : "Bloodline_Line_Milk";
            lines += string.Format("<BR>- " + Loc(key), 5 * level, itemName);
        }

        if (!string.IsNullOrEmpty(lines))
        {
            text += "<BR><BR><color=" + GameColors.BoldColorHex + ">" + Loc("Bloodline_Tooltip_PerLevel") + "</color>";
            text += lines;
        }

        // ★ 怪物效果区块：先列该血脉的怪物专属效果，再固定补上「交配时间 / 体力消耗量」两行
        //   （Trait99 / Trait100）—— 原版特质面板只有 8 格，这两条经常被血脉/职业特质挤掉而
        //   显示不出来；放在这里保证玩家一定能看到它们的实际数值。
        string monsterLine = BuildMonsterEffectLine(data, level)
                             + BuildTraitValueLines(data, level, character);
        if (!string.IsNullOrEmpty(monsterLine))
        {
            text += "<BR><BR><color=" + GameColors.BoldColorHex + ">" + Loc("Bloodline_Tooltip_Monster") + "</color>";
            text += monsterLine;
        }

        return text + "</size>";
    }

    /// <summary>
    /// 「交配时间 / 体力消耗量」数值的不利 / 有利配色：
    /// 数值 &gt; 0 → 红色（<see cref="GameColors.BadColorHex"/>，交配更久 / 更费体力 = 不利）；
    /// 数值 &lt; 0 → 绿色（<see cref="GameColors.GoodColorHex"/>，更短 / 更省 = 有利）；
    /// 等于 0 → 无色（返回 null）。交配时间越长、体力消耗越大越不利，因此正数红、负数绿。
    /// </summary>
    private static string? GetSignedStatColorHex(float value)
    {
        if (value > 0f)
            return GameColors.BadColorHex;
        if (value < 0f)
            return GameColors.GoodColorHex;
        return null;
    }

    /// <summary>带颜色的带符号数值（仅**数字**着色；保留给「数字变色、文本不变色」的场景）。</summary>
    private static string ColoredSigned(float value)
    {
        string? color = GetSignedStatColorHex(value);
        return color == null ? Signed(value) : "<color=" + color + ">" + Signed(value) + "</color>";
    }

    /// <summary>
    /// ★ 整行着色（本轮需求）：把**整行文本**（含标签与「（每级+…）」）按数值统一染色 ——
    /// 数值 &gt; 0 → 整行红、&lt; 0 → 整行绿、= 0 → 整行不变色。
    /// 用于「交配时间 / 体力消耗量」两行；行首的「- 」符号留在着色之外（与其它行对齐）。
    /// </summary>
    private static string ColorWholeLine(string line, float value)
    {
        string? color = GetSignedStatColorHex(value);
        return color == null ? line : "<color=" + color + ">" + line + "</color>";
    }

    /// <summary>
    /// 「怪物效果」区块里固定的两行：交配时间（Trait99）/ 体力消耗量（Trait100）。
    ///
    /// 数值规则（与用户需求一致）：显示值 = **血脉与 Trait 的实际增减相加**。
    /// 由于血脉的每级交配时间/体力消耗本来就是**写入 Trait99 / Trait100 槽位**实现的
    /// （见 <see cref="BloodlineVanillaStats"/>），所以直接读该角色这两个特质的当前值即可：
    ///   例：Trait99 = -5s、恶魔血脉 +2 级（+20s）→ 显示 +15s。
    /// 若该血脉本身带每级加成（如恶魔 +5s/级、野性 +0.05/级），文本末尾附「（每级+X）」；
    /// 不带每级加成的血脉只显示两行纯数值（无括号）。
    /// 无角色对象时（本地化键 #BloodlineText_&lt;value&gt; 路径）退化为「血脉自身的加成 × 等级」。
    /// </summary>
    private static string BuildTraitValueLines(BloodlineData data, int level, Character? character)
    {
        int sexTime = character != null
            ? Mathf.RoundToInt(character.GetTraitValue(BloodlineVanillaStats.SexTimeTrait))
            : data.SexTimePerLevel * level;

        float mentality = character != null
            ? character.GetTraitValue(BloodlineVanillaStats.MentalityCostTrait)
            : data.MentalityCostPerLevel * level;

        string text = string.Empty;

        // ★ 数值按正负着色：> 0 红（不利）/ < 0 绿（有利）——
        //   本轮需求：**整行**（标签 + 数值 + 「（每级+…）」）统一变色，而不是只染数字。
        if (data.SexTimePerLevel != 0)
            text += "<BR>- " + ColorWholeLine(
                string.Format(Loc("Bloodline_Line_MonsterSexTimePerLevel"), Signed(sexTime) + "s", data.SexTimePerLevel),
                sexTime);
        else
            text += "<BR>- " + ColorWholeLine(
                string.Format(Loc("Bloodline_Line_MonsterSexTime"), Signed(sexTime) + "s"),
                sexTime);

        if (data.MentalityCostPerLevel != 0f)
            text += "<BR>- " + ColorWholeLine(
                string.Format(Loc("Bloodline_Line_MonsterMentalityPerLevel"), Signed(mentality),
                    data.MentalityCostPerLevel.ToString("0.##")),
                mentality);
        else
            text += "<BR>- " + ColorWholeLine(
                string.Format(Loc("Bloodline_Line_MonsterMentality"), Signed(mentality)),
                mentality);

        return text;
    }

    /// <summary>怪物专属效果行（无效果返回空字符串）。</summary>
    private static string BuildMonsterEffectLine(BloodlineData data, int level)
    {
        switch (data.MonsterEffect)
        {
            case EMonsterBloodlineEffect.PartnerSexSpeed:
                return string.Format("<BR>- " + Loc("Bloodline_Line_MonsterSpeed"),
                    Signed(data.MonsterEffectValue), Signed(data.MonsterEffectValue * level));
            case EMonsterBloodlineEffect.PartnerShieldDrain:
                // 防护结界「减少」，因此显示为负值
                return string.Format("<BR>- " + Loc("Bloodline_Line_MonsterShield"),
                    Signed(-data.MonsterEffectValue), Signed(-data.MonsterEffectValue * level));
            case EMonsterBloodlineEffect.PartnerTraitUpgrade:
                return string.Format("<BR>- " + Loc("Bloodline_Line_MonsterUpgrade"),
                    data.MonsterEffectValue, data.MonsterEffectValue * level);
            case EMonsterBloodlineEffect.PartnerStaminaRestore:
                return string.Format("<BR>- " + Loc("Bloodline_Line_MonsterStamina"),
                    data.MonsterEffectValue, data.MonsterEffectValue * level);
            default:
                return string.Empty;
        }
    }

    // ─── 工具 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// 诊断日志：仅在模组菜单开启 EnableLogging（或调试模式）时输出。
    /// 血脉写入 / 等级提升 / 数值版本迁移都会按角色高频触发（读档时数百条），
    /// 默认关闭以免刷屏 output_log.txt / ModLoader.log。
    /// </summary>
    private static void LogDebug(string msg)
    {
        if (DepravedShield.ModConfig.EnableLogging || ComplexBreedingRedux.Utilities.GameManagerUtils.DEBUG_MODE)
            Debug.Log(msg);
    }

    /// <summary>
    /// 诊断日志：打印一次血脉遗传的判定依据（母亲 / 父本的血脉与等级、是否按「人类客户」处理、
    /// 是否允许 +1、最终继承结果）。仅在模组日志（EnableLogging）或调试模式下输出。
    /// </summary>
    private static void LogInheritance(
        Character child, Character? mother, Character? father,
        BloodlineData? motherData, int motherLevel,
        BloodlineData? fatherData, int fatherLevel,
        bool fatherIsClient, bool levelUpEligible, EBloodline inherited, int level)
    {
        if (!DepravedShield.ModConfig.EnableLogging && !ComplexBreedingRedux.Utilities.GameManagerUtils.DEBUG_MODE)
            return;

        Debug.Log($"[Bloodline] 遗传判定 {child.DisplayName}："
            + $"母亲={DescribeParent(mother, motherData, motherLevel)}；"
            + $"父本={DescribeParent(father, fatherData, fatherLevel)}（人类客户={fatherIsClient}）"
            + $"；允许+1={levelUpEligible} → 继承="
            + (inherited == EBloodline.None ? "无（由物种最低等级兜底）" : inherited + "+" + level));
    }

    /// <summary>把亲本描述成「类型名(名称)/血脉+等级」（供诊断日志使用）。</summary>
    private static string DescribeParent(Character? character, BloodlineData? data, int level)
    {
        if (character == null)
            return "<未知>";
        string who = character.GetType().Name + "(" + character.DisplayName + ")";
        return data == null ? who + "/无血脉" : who + "/" + data.InternalName + "+" + level;
    }

    private static string Signed(float value)
        => (value > 0f ? "+" : string.Empty) + value.ToString("0.##");

    private static string Signed(int value)
        => (value > 0 ? "+" : string.Empty) + value.ToString();

    /// <summary>百分比显示（比例 → 百分比，例如 -1 → "-100%"）。</summary>
    private static string SignedPercent(float ratio)
        => Signed(Mathf.RoundToInt(ratio * 100f)) + "%";

    private static string Loc(string key)
        => Strings.ResourceManager.GetString(key, Strings.Culture) ?? key;
}
