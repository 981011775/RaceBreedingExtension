#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using ComplexBreedingRedux.Data;
using ComplexBreedingRedux.Data.Config;
using ComplexBreedingRedux.Data.Factions;
using ComplexBreedingRedux.Data.Professions;
using ComplexBreedingRedux.Data.Species;
using ComplexBreedingRedux.Data.SpeciesCore;
using ComplexBreedingRedux.Features;
using ComplexBreedingRedux.Properties;
using ComplexBreedingRedux.Utilities;
using MBM.ModLoader.Core;
using MBMScripts;
using RaceBreedingExtension.Bloodline;
using RaceBreedingExtension.Bloodline.Data;
using RaceBreedingExtension.CombatPower;
using RaceBreedingExtension.GroundCamp;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

namespace RaceBreedingExtension.HunterCamp;

/// <summary>
/// 猎手营地（个人领地地面营地）自动捕获系统。
///
/// 需求实现对应关系：
///  1) 旧版“房间升级(RoomUpgrades)猎手营地”整体删除：不再有任何房间升级补丁，
///     也不再生成/显示猎手营地房间技能（老存档残留的 102/103 技能会被自动清理）；
///     奴隶改由「猎手营地 / 大型猎手营地」房间自动生成，每次生成消耗金币。
///  2) 生成时机：营地内**没有奴隶**时，等待 SpawnDelaySeconds(默认 5 秒) 后自动生成。
///  3) 新生奴隶 Training = 0。
///  4) 房内没有怪物 → EscapeWithoutMonsterSeconds(默认 60 秒) 后逃跑消失；
///     房内有怪物 → 把奴隶移动到怪物座位，由**原版 AI** 触发性行为；
///     累计 SexCount 达到所需次数（默认 5，可被怪物特性增加，见 HunterCampConfig.SexEscapeTraitBonus）后逃跑。
///  5) 俘虏在正常状态下 IsNotDraggable = true（同 Flora2/Niel2：不能拖动、不能移动，只能查看/触发性行为）；
///     进入乏力(Drained / SexDrained)、**耐力低于 30%（乏力阈值，即使状态是 Rest）**、
///     堕落(Depraved)或死亡状态时解除锁定，同时不再逃跑。
///  6) 逃跑会损失大量金币（HunterCampConfig.EscapeGoldPenalty）。
///  8) 非 sex 状态下把俘虏的显示层级放到怪物前面。
///
/// 关于“无法移动”：本模组只负责拖动锁（IsNotDraggable）与逃跑；原版会移动奴隶的只有
/// 皮克西搬运逻辑（按房间类型筛选，怪物牢笼类房间不会被选中），营地房间因此天然不会被搬走。
/// </summary>
internal static class HunterCampSystem
{
    /// <summary>旧版（房间升级版）奴隶营地技能 ID，仅用于清理老存档残留。</summary>
    private static readonly ERoomSkill LegacySlaveCampSkill = (ERoomSkill)102;

    /// <summary>旧版（房间升级版）怪物营地技能 ID，仅用于清理老存档残留。</summary>
    private static readonly ERoomSkill LegacyMonsterCampSkill = (ERoomSkill)103;

    /// <summary>各营地的生成周期状态（房间 UnitId → 周期）。不落盘：读档后重新开始计时。</summary>
    private static readonly Dictionary<int, CampCycle> _campCycles = new Dictionary<int, CampCycle>();

    /// <summary>生成周期阶段。</summary>
    private enum ECampPhase
    {
        /// <summary>房间空置，正在等待触发。</summary>
        Waiting,

        /// <summary>正在按容量把房间填满。</summary>
        Filling,

        /// <summary>本周期已结束（已填满，或玩家自己放了奴隶）；只有房间被完全清空后才会重新开始。</summary>
        Done,
    }

    private sealed class CampCycle
    {
        public ECampPhase Phase = ECampPhase.Waiting;
        public float Elapsed;
        public int Failures;
    }

    /// <summary>单次生成尝试的结果。</summary>
    private enum ESpawnResult
    {
        Success,
        Failed,
    }

    private static bool _languageSubscribed;
    private static bool _legacySkillCleaned;

    /// <summary>俘虏显示层级相对怪物的前后偏移（z 兜底用，越小越靠前）。</summary>
    private const float CaptiveLayerZDelta = 0.1f;

    /// <summary>俘虏渲染顺序相对“房内最靠前角色（怪物等）”的增量。</summary>
    private const int CaptiveLayerOrderOffset = 1;

    private static readonly EState[] SexStates =
    {
        EState.Sex,
        EState.SexDrained,
        EState.CumShotInside,
        EState.CumShotOutside,
        EState.FaceSitting,
        EState.Ejaculation,
        EState.Masturbation,
        EState.MasturbationCumShot,
        EState.MasturbationBirth,
    };

    // ============================ 初始化 ============================

    public static void Initialize()
    {
        HunterCampConfig.Register();

        if (!_languageSubscribed)
        {
            Localization.OnLanguageChanged += OnLanguageChanged;
            _languageSubscribed = true;
        }

        Log("Hunter camp (ground camp captive) subsystem initialized.");
    }

    private static void OnLanguageChanged(string langCode)
    {
        try
        {
            HunterCampConfig.OnLanguageChanged();
        }
        catch (Exception e)
        {
            LogError("OnLanguageChanged failed: " + e);
        }
    }

    // ============================ 主循环（Room.OnTickUpdate 调用） ============================

    /// <summary>每个房间每 tick 调用一次；只处理个人领地地面的猎手营地。</summary>
    public static void ProcessRoom(Room? room, float tickTime)
    {
        if (!HunterCampConfig.Enable || room == null || tickTime <= 0f)
            return;
        if (!GroundCampSystem.IsGroundCamp(room) || !room.IsPrivate)
            return;

        try
        {
            PlayData? pd = GameManager.Instance?.PlayerData;
            if (pd == null)
                return;

            CleanupLegacyCampSkillsOnce(pd);

            int femaleCount = 0;
            foreach (Unit unit in pd.GetUnitList(ESector.Female, room.Slot).ToList())
            {
                if (unit is not Female female || female.IsBaby)
                    continue;

                femaleCount++;
                if (IsCaptive(female))
                    UpdateCaptive(pd, room, female, tickTime);
            }

            UpdateSpawnCycle(pd, room, femaleCount, tickTime);
        }
        catch (Exception e)
        {
            LogError("ProcessRoom failed: " + e);
        }
    }

    /// <summary>
    /// 营地生成周期（需求：按房间容量满值生成；把守卫全部清空后才触发下一次生成）：
    ///   Waiting → 房间完全空置并等待 SpawnDelaySeconds → Filling（每 tick 生成 1 名直到满值）→ Done；
    ///   Done 状态下不再补充，只有房间被**完全清空**后才会回到 Waiting 重新计时。
    /// </summary>
    private static void UpdateSpawnCycle(PlayData pd, Room room, int femaleCount, float tickTime)
    {
        int capacity = GroundCampSystem.GetSlaveCapacity(room);
        if (capacity <= 0)
            return;

        if (!_campCycles.TryGetValue(room.UnitId, out CampCycle cycle))
        {
            cycle = new CampCycle();
            _campCycles[room.UnitId] = cycle;
        }

        switch (cycle.Phase)
        {
            case ECampPhase.Done:
                if (femaleCount > 0)
                    return;
                // 房间已全部清空 → 重新开始等待
                cycle.Phase = ECampPhase.Waiting;
                cycle.Elapsed = 0f;
                cycle.Failures = 0;
                return;

            case ECampPhase.Waiting:
                if (femaleCount > 0)
                {
                    // 玩家自己往营地里放了奴隶 → 本周期不生成，直到房间清空
                    cycle.Phase = ECampPhase.Done;
                    cycle.Elapsed = 0f;
                    return;
                }
                cycle.Elapsed += tickTime;
                if (cycle.Elapsed < HunterCampConfig.SpawnDelaySeconds)
                    return;

                cycle.Elapsed = 0f;
                cycle.Failures = 0;
                cycle.Phase = ECampPhase.Filling;
                Log($"营地 {room.Slot} 开始生成（容量 {capacity}）");
                return;
        }

        // ─── Filling：每 tick 最多生成 1 名，直到填满 ───
        if (femaleCount >= capacity)
        {
            cycle.Phase = ECampPhase.Done;
            Log($"营地 {room.Slot} 已填满（{femaleCount}/{capacity}），等待全部清空后才会再次生成");
            return;
        }

        switch (TrySpawnCaptive(pd, room))
        {
            case ESpawnResult.Success:
                cycle.Failures = 0;
                break;

            default:
                cycle.Failures++;
                if (cycle.Failures >= 5)
                {
                    cycle.Phase = ECampPhase.Done;
                    Log($"营地 {room.Slot} 连续生成失败，暂停本周期");
                }
                break;
        }
    }

    // ============================ 自动生成奴隶 ============================

    /// <summary>
    /// 在营地中自动生成一名奴隶（需求 1/2/3）：
    /// 随机种族/职业/属性（受派系声望影响）→ Training = 0 → 打上俘虏标记 →
    /// 按「战斗力 ÷ 30」扣金币（不再有金币不足过滤：不足时直接把金币扣成负数）。
    /// </summary>
    private static ESpawnResult TrySpawnCaptive(PlayData pd, Room room)
    {
        CapturedRoll? roll = RollCapturedSlave();
        if (roll == null)
            return ESpawnResult.Failed;

        // 第一名奴隶直接生成在怪物座位上（原版 AI 会立刻开始性行为）；
        // 之后生成的奴隶由游戏自动选空位（怪物座位已被占用时同样交给游戏选位）。
        int seat = -1;
        if (HunterCampConfig.AutoPairWithMonster)
        {
            int monsterSeat = FindMonsterSeat(pd, room);
            if (monsterSeat >= 0 && pd.GetUnit(ESector.Female, room.Slot, monsterSeat) == null)
                seat = monsterSeat;
        }

        // ★ 种族由「供应派系 + 该派系声望」决定（见 RollCapturedSlave / ComputeCaptiveSpeciesCandidates）：
        //   基础种族（无血脉）直接用它对应的原版种族类型创建；物种（有血脉的进化种族、无血脉的哥布林）
        //   统一创建为 Human，随后由 SetSpecies 重写外观与数值。
        if (pd.NewCharacter(roll.UnitType, ESector.Female, room.Slot, seat, true) is not Female produced)
            return ESpawnResult.Failed;

        bool succeeded = false;
        try
        {
            if (roll.Species != null)
                FemaleSpeciesSetup.SetSpecies(produced, roll.Species);

            // ★ 职业（派系声望加权）+ 派系记录 + 属性（Trait93~100）+ 体格倍率
            ApplyProducedProfile(produced, roll.Species, roll);

            produced.DisplayName = LocalizationSystem.ComposeSpeciesProfessionDisplayName(
                roll.Species?.Name, roll.Profession?.Name, produced.RaceName);

            produced.Training = 0;      // 需求 3：训练值固定为 0
            produced.SortTrait();

            // ★ 生成费用 = 该奴隶的战斗力 ÷ 30（整数除法）；金币不足时直接扣成负数，不再过滤
            int goldCost = HunterCampConfig.ComputeSpawnCost(produced);
            pd.Gold -= goldCost;

            TagCaptive(produced);
            RefreshCaptiveLock(produced);

            string msg = string.Format(Loc("HunterCamp_SpawnLog"), produced.DisplayName, goldCost);
            GameManagerUtils.AddLogMessage(msg);

            Log($"营地 {room.Slot} 生成奴隶：{produced.DisplayName}"
                + $"（派系：{roll.Faction?.Name ?? "无"}，声望系数 {roll.ReputationNormalized:0.00}"
                + $"，种族：{roll.Species?.Name ?? (roll.Core?.Name ?? produced.RaceName)}"
                + $"，血脉：{(roll.HasBloodline ? "有" : "无")}"
                + $"，职业：{roll.Profession?.Name ?? "无"}"
                + $"，战斗力 {CombatPowerSystem.GetCombatPower(produced)}，花费 {goldCost} 金）");
            succeeded = true;
            return ESpawnResult.Success;
        }
        catch (Exception e)
        {
            LogError("TrySpawnCaptive failed: " + e);
            return ESpawnResult.Failed;
        }
        finally
        {
            if (!succeeded && produced != null)
                pd.RemoveUnit(produced);
        }
    }

    // ============================ 俘虏状态维护 ============================

    private static void UpdateCaptive(PlayData pd, Room room, Female female, float tickTime)
    {
        // ─── 死亡 / 堕落：永久解除俘虏身份（可拖动、不再逃跑） ───
        if (female.IsDead || female.IsDepraved)
        {
            female.IsNotDraggable = false;
            ReleaseCaptive(female, female.IsDead ? "死亡" : "堕落");
            return;
        }

        // ─── 拖动锁（需求 5）：乏力 / 耐力低于 30% 解锁；其它正常状态锁死；原版自管的状态不干预 ───
        bool drained = female.State == EState.Drained || female.State == EState.SexDrained;
        // ★ 耐力（体力百分比）低于乏力阈值（默认 30%）时同样解除拖动锁 ——
        //   即使当前状态停留在 Rest（休息）也允许玩家拖动。
        bool lowStamina = IsStaminaLow(female);
        if (drained || lowStamina)
            female.IsNotDraggable = false;
        else if (IsVanillaManagedState(female.State))
            { /* 交给原版逻辑（分娩/死亡/卡片/工作等） */ }
        else
            female.IsNotDraggable = true;

        List<Character> monsters = GetLivingMonsters(pd, room);

        if (monsters.Count == 0)
        {
            // 房内没有怪物 → 累计倒计时；乏力状态下暂停逃跑（需求 5：“无法再逃跑”）
            if (!drained)
            {
                float elapsed = GetNoMonsterTimer(female) + tickTime;
                SetNoMonsterTimer(female, elapsed);
                if (elapsed >= HunterCampConfig.EscapeWithoutMonsterSeconds)
                {
                    Escape(pd, room, female, "房内没有怪物");
                    return;
                }
            }
        }
        else
        {
            SetNoMonsterTimer(female, 0f);

            if (HunterCampConfig.AutoPairWithMonster && !drained)
                TryPairWithMonster(pd, room, female, monsters[0]);

            if (!drained)
            {
                int required = GetRequiredSexCount(monsters);
                if (female.SexCount >= required)
                {
                    Escape(pd, room, female, $"性行为 {female.SexCount}/{required} 次");
                    return;
                }
            }
        }

        // 需求 8：非 sex 状态下显示在怪物前面
        ApplyCaptiveLayerOrder(female, room);
    }

    /// <summary>把俘虏移动到怪物的座位：原版 Male.OnFixedUpdate 会取“同座位”的女性作为 Partner，
    /// 随后 Male.Idle → State = Sex，Female.Idle 检测到 Partner 处于 Sex 后执行 ChangeToSex，
    /// 于是完全使用原版性行为流程（动画 / 中出 / 训练值增长 / 怀孕等）。</summary>
    private static void TryPairWithMonster(PlayData pd, Room room, Female female, Character monster)
    {
        if (female.Seat == monster.Seat || female.IsDragging)
            return;

        pd.Allocate(female, ESector.Female, room.Slot, monster.Seat, false, -1, true);
    }

    // ============================ 逃跑 ============================

    /// <summary>
    /// 逃跑：奴隶消失 + 损失金币（需求 4 / 6）。
    /// 损失金额 = 该奴隶的战斗力（<see cref="HunterCampConfig.ComputeEscapePenalty"/>），
    /// 因此必须在把她从场景中移除之前结算。
    /// 若她怀有胎儿，一并清理（与游戏死亡处理一致），避免残留孤儿胎儿。
    /// </summary>
    private static void Escape(PlayData pd, Room room, Female female, string reason)
    {
        string name = female.DisplayName;
        Vector3 position = female.Position;
        int penalty = HunterCampConfig.ComputeEscapePenalty(female);   // ★ 战斗力必须在移除前结算

        try
        {
            foreach (int fetusId in female.FetusUnitIdList.ToList())
                pd.RemoveUnit(fetusId);
            female.FetusUnitIdList.Clear();
            female.PregnantType = 0;
        }
        catch (Exception e)
        {
            LogError("清理胎儿失败: " + e);
        }

        RemoveCaptiveMark(female);
        pd.RemoveUnit(female);

        if (penalty > 0)
            pd.Gold -= penalty;

        string msg = string.Format(Loc("HunterCamp_EscapeLog"), name, penalty);
        GameManagerUtils.AddLogMessage(msg);
        PopUpAt(string.Format(Loc("HunterCamp_EscapePop"), penalty), position, "Object[resource_gold_128]");

        Log($"奴隶逃跑：{name}（{reason}），损失 {penalty} 金");
    }

    /// <summary>
    /// 逃跑所需的性行为次数：基础值（默认 5）+ 怪物特性加成（配置表）+ 怪物职业加成
    /// （Brigand+1 / Thief+2 / Marauder+3 / Plunderer+4 / Trainer+4）。
    /// 职业加成对营地内**所有**奴隶起效，但只在怪物位于该房间内时参与计算。
    /// </summary>
    public static int GetRequiredSexCount(List<Character> monsters)
    {
        int required = HunterCampConfig.SexEscapeBaseCount;
        if (monsters == null)
            return Mathf.Max(1, required);

        foreach (Character monster in monsters)
        {
            if (monster == null)
                continue;

            if (HunterCampConfig.SexEscapeTraitBonus.Count > 0)
            {
                foreach (KeyValuePair<ETrait, int> kv in HunterCampConfig.SexEscapeTraitBonus)
                {
                    if (monster.TraitContains(kv.Key))
                        required += kv.Value;
                }
            }

            // ★ 怪物职业加成（「职业在怪物身上时」）
            IProfessionData? profession = CombatPowerSystem.TryGetProfession(monster);
            if (profession != null && profession.CampSexCountBonus != 0)
                required += profession.CampSexCountBonus;
        }
        return Mathf.Max(1, required);
    }

    // ============================ 俘虏标记（跨存档） ============================

    /// <summary>是否为营地俘虏（带自定义标记 Buff）。</summary>
    public static bool IsCaptive(Character? character)
        => character != null && !character.IsBaby && character.GetBuff(CampBuffs.CaptiveMark) != null;

    /// <summary>标记为营地俘虏（Infinity + 不显示图标）。</summary>
    private static void TagCaptive(Female female)
    {
        if (female.GetBuff(CampBuffs.CaptiveMark) != null)
            return;

        female.AddBuff(CampBuffs.CaptiveMark, 0f, true, false, null);
    }

    /// <summary>解除俘虏标记（之后她就是普通奴隶）。</summary>
    private static void ReleaseCaptive(Female female, string reason)
    {
        if (female.GetBuff(CampBuffs.CaptiveMark) == null)
            return;

        RemoveCaptiveMark(female);
        Log($"解除俘虏标记：{female.DisplayName}（{reason}）");
    }

    private static void RemoveCaptiveMark(Character character)
    {
        try
        {
            if (character.GetBuff(CampBuffs.CaptiveMark) != null)
                character.RemoveBuff(CampBuffs.CaptiveMark);
        }
        catch (Exception e)
        {
            LogError("RemoveCaptiveMark failed: " + e);
        }
    }

    /// <summary>立刻按当前状态刷新一次拖动锁（生成 / 读档后调用）。</summary>
    public static void RefreshCaptiveLock(Female? female)
    {
        if (female == null || !IsCaptive(female))
            return;

        bool unlocked = female.IsDead || female.IsDepraved
                        || female.State == EState.Drained || female.State == EState.SexDrained
                        || IsStaminaLow(female);   // ★ 耐力低于 30% → 即使 Rest 也可拖动

        if (unlocked)
            female.IsNotDraggable = false;
        else if (!IsVanillaManagedState(female.State))
            female.IsNotDraggable = true;
    }

    /// <summary>房内无怪物时的逃跑倒计时（存放在标记 Buff 的 MaxTime，可跨存档）。</summary>
    private static float GetNoMonsterTimer(Character character)
    {
        Buff? buff = character.GetBuff(CampBuffs.CaptiveMark);
        return buff?.MaxTime ?? 0f;
    }

    private static void SetNoMonsterTimer(Character character, float seconds)
    {
        Buff? buff = character.GetBuff(CampBuffs.CaptiveMark);
        if (buff != null)
            buff.MaxTime = seconds;
    }

    // ============================ 显示层级（需求 8） ============================

    /// <summary>
    /// 非 sex 状态下把俘虏放到房间内**所有角色显示层级之前**（重点是怪物），
    /// 避免她与怪物共用/相邻座位时被遮挡（Stand / Rest 等所有非 sex 状态均生效）。
    ///
    /// 实现：直接改写渲染顺序（sortingOrder / sortingLayerID）。游戏的角色使用 Spine
    /// SkeletonRenderer（其 meshRenderer 的排序完全由我们控制，Spine 不会覆盖）；
    /// 若角色自身（或其子物体）挂了 SortingGroup 则改写该 SortingGroup；同时保留 z 偏移兜底。
    ///
    /// 注意：基准值只取“怪物 + 非俘虏女性”，**排除俘虏自己**，否则会形成逐帧自增的反馈。
    /// </summary>
    public static void ApplyCaptiveLayerOrder(Female? female, Room? room)
    {
        if (female == null || room == null || female.IsBaby || !IsCaptive(female))
            return;
        if (SexStates.Contains(female.State))
            return;

        UnitComponent? component = female.UnitComponent;
        if (component == null)
            return;

        if (!TryGetRoomFrontSorting(room, female, out int frontOrder, out int frontLayer))
            return;   // 房内没有其它角色：无需前置

        int targetOrder = frontOrder + CaptiveLayerOrderOffset;

        SortingGroup? group = component.GetComponent<SortingGroup>()
                              ?? component.GetComponentInChildren<SortingGroup>(true);
        if (group != null)
        {
            if (group.sortingLayerID != frontLayer)
                group.sortingLayerID = frontLayer;
            if (group.sortingOrder != targetOrder)
                group.sortingOrder = targetOrder;
        }

        foreach (Renderer renderer in component.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.sortingLayerID != frontLayer)
                renderer.sortingLayerID = frontLayer;
            if (renderer.sortingOrder != targetOrder)
                renderer.sortingOrder = targetOrder;
        }

        // z 兜底（渲染顺序被其它系统接管时仍能生效）
        Vector3 position = component.transform.position;
        float targetZ = GetFrontMostCharacterZ(room, female) - CaptiveLayerZDelta;
        if (!Mathf.Approximately(position.z, targetZ))
            component.transform.position = new Vector3(position.x, position.y, targetZ);
    }

    /// <summary>
    /// 统计房内所有角色（Male 全部 + Female 中**非俘虏**）中最靠前的渲染顺序与 SortingLayer。
    /// 排除俘虏自身与其它俘虏，保证结果稳定、不会逐帧自增。
    /// </summary>
    private static bool TryGetRoomFrontSorting(Room room, Female self, out int order, out int layerId)
    {
        order = int.MinValue;
        layerId = 0;

        PlayData? pd = GameManager.Instance?.PlayerData;
        if (pd == null)
            return false;

        CollectFrontSorting(pd.GetUnitList(ESector.Male, room.Slot), null, ref order, ref layerId);
        CollectFrontSorting(pd.GetUnitList(ESector.Female, room.Slot), self, ref order, ref layerId);

        return order != int.MinValue;
    }

    private static void CollectFrontSorting(IEnumerable<Unit> units, Female? exclude, ref int order, ref int layerId)
    {
        foreach (Unit unit in units)
        {
            // 注意：死亡的怪物/角色依然会渲染（尸体），因此这里不排除死亡单位。
            if (unit is not Character character || character.IsBaby)
                continue;
            if (exclude != null && ReferenceEquals(character, exclude))
                continue;
            if (character is Female other && IsCaptive(other))
                continue;   // 其它俘虏：忽略，避免互相抬高

            UnitComponent? component = character.UnitComponent;
            if (component == null)
                continue;

            SortingGroup? group = component.GetComponent<SortingGroup>()
                                  ?? component.GetComponentInChildren<SortingGroup>(true);
            if (group != null)
            {
                if (group.sortingOrder > order)
                {
                    order = group.sortingOrder;
                    layerId = group.sortingLayerID;
                }
                continue;
            }

            foreach (Renderer renderer in component.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.sortingOrder > order)
                {
                    order = renderer.sortingOrder;
                    layerId = renderer.sortingLayerID;
                }
            }
        }
    }

    /// <summary>房内所有非俘虏角色中最靠前的 z（最小 z = 最靠前），用于 z 兜底。</summary>
    private static float GetFrontMostCharacterZ(Room room, Female self)
    {
        PlayData? pd = GameManager.Instance?.PlayerData;
        if (pd == null)
            return 0f;

        float frontZ = 0f;
        bool found = false;

        foreach (Unit unit in pd.GetUnitList(ESector.Male, room.Slot))
        {
            // 死亡的怪物（尸体）同样参与前后判定
            if (unit is not Character male || male.IsBaby)
                continue;
            UnitComponent? component = male.UnitComponent;
            if (component == null)
                continue;

            float z = component.transform.position.z;
            if (!found || z < frontZ)
            {
                frontZ = z;
                found = true;
            }
        }

        return found ? frontZ : 0f;
    }

    private static List<Character> GetLivingMonsters(PlayData pd, Room room)
    {
        List<Character> monsters = new List<Character>();
        foreach (Unit unit in pd.GetUnitList(ESector.Male, room.Slot))
        {
            if (unit is Character male && !male.IsDead && !male.IsBaby)
                monsters.Add(male);
        }
        return monsters;
    }

    private static int FindMonsterSeat(PlayData pd, Room room)
    {
        foreach (Unit unit in pd.GetUnitList(ESector.Male, room.Slot))
        {
            if (unit is Character male && !male.IsDead && !male.IsBaby)
                return male.Seat;
        }
        return -1;
    }

    private static bool IsVanillaManagedState(EState state)
    {
        switch (state)
        {
            case EState.Birth:
            case EState.BirthDrained:
            case EState.AfterBirth:
            case EState.AfterBirthDrained:
            case EState.Fetus:
            case EState.NewbornBaby:
            case EState.NewbornBabyUmbilicalCord:
            case EState.BabyIdle:
            case EState.BabyMove:
            case EState.Coffin:
            case EState.Disintegration:
            case EState.BabyDisintegration:
            case EState.Card:
            case EState.CardBaby:
            case EState.Portrait:
            case EState.DeadPortrait:
            case EState.Commission:
            case EState.FloraWorkplace:
            case EState.NielWorkplace:
            case EState.CapsuleStart:
            case EState.CapsuleEnd:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// 耐力是否低于「乏力阈值」：耐力 = <see cref="Character.Health"/>（体力总量），
    /// 百分比 = <see cref="Character.MentalityPercent"/> = Health / MaxHealth；
    /// 阈值 = <c>ConfigData.PercentThatChangesToDrain</c>（默认 0.3 = 30%，即原版
    /// 「体力低于 30% 就进入乏力」的那条分界线，原版用 <c>MentalityPercent &gt; 阈值</c>
    /// 决定 Rest / Drained）。
    ///
    /// ★ 用途（需求：耐力低于 30% 时 Rest 状态也能拖动）：
    /// 原版只有在「状态已经是 Drained / SexDrained」时才算乏力，但部分情况下耐力已经低于
    /// 30%（例如刚结束性行为、状态机尚未切到 Drained 而停留在 Rest），此时俘虏仍被拖动锁
    /// 卡住。这里直接按耐力数值判定，让「耐力 &lt; 30%」和「乏力状态」等价 ——
    /// 无论当前是 Rest 还是 Drained，都解除拖动锁（可被玩家拖走）。
    /// </summary>
    public static bool IsStaminaLow(Female? female)
    {
        try
        {
            if (female == null)
                return false;

            float threshold = GameManager.ConfigData.PercentThatChangesToDrain;
            return female.MaxHealth > 0f && female.MentalityPercent < threshold;
        }
        catch (Exception e)
        {
            LogError("IsStaminaLow failed: " + e);
            return false;
        }
    }

    // ============================ 旧版房间技能清理（需求 9） ============================

    /// <summary>
    /// 老存档里由“房间升级”获得的猎手营地技能（102 / 103）会被清理掉，
    /// 避免残留技能继续显示或产生误解。每次进入游戏只执行一次。
    /// </summary>
    private static void CleanupLegacyCampSkillsOnce(PlayData pd)
    {
        if (_legacySkillCleaned)
            return;
        _legacySkillCleaned = true;

        try
        {
            int removed = 0;
            foreach (Unit unit in pd.GetUnitList(ESector.Room))
            {
                Room? room = unit as Room;
                if (room?.RoomSkillSeqList == null)
                    continue;

                for (int i = room.RoomSkillSeqList.Count - 1; i >= 0; i--)
                {
                    ERoomSkill skill = room.RoomSkillSeqList[i].Item1;
                    if (skill == LegacySlaveCampSkill || skill == LegacyMonsterCampSkill)
                    {
                        room.RoomSkillSeqList.RemoveAt(i);
                        removed++;
                    }
                }
            }

            if (removed > 0)
                Log($"已清理老存档中的猎手营地房间技能 {removed} 个");
        }
        catch (Exception e)
        {
            LogError("CleanupLegacyCampSkills failed: " + e);
        }
    }

    // ============================ 飘字提示 ============================

    private static void PopUpAt(string text, Vector3 position, string spriteFullName)
    {
        try
        {
            PopUpText popUp = SeqObjectPoolManager.Instance
                .GetPooledObject("PopUpText", null, null)
                .GetComponent<PopUpText>();
            popUp.transform.position = position;
            popUp.ReferenceAddressablesSprite1.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
            popUp.ReferenceAddressablesSprite1.AddressablesSpriteFullName = spriteFullName;
            popUp.ReferenceFormattingText.Value = text;
            popUp.ReferenceString1.Value = string.Empty;
            popUp.ReferenceString2.Value = string.Empty;
            popUp.Setup(PopUpText.ETweenType.Top, GameManager.Instance.OrthographicSizeRate);
            popUp.ActivateAsPooledObject();
        }
        catch (Exception e)
        {
            LogError("PopUpAt failed: " + e);
        }
    }

    // ============================ 生成参数随机（猎手营地 / 职业造物共用） ============================

    /// <summary>
    /// 生成参数（职业 + 供应派系 + 体格倍率）。
    /// 猎手营地俘虏与「职业生成的傀儡（Golem，见 <see cref="ComplexBreedingRedux.Features.ProfessionSystem"/>）」
    /// 共用同一套随机规则：属性随机、职业随机、受派系声望影响。
    /// </summary>
    public class ProducedProfile
    {
        /// <summary>本次生成的「供应派系」（已解锁派系中随机；null = 无派系，按声望 0 处理）。</summary>
        public FactionData? Faction { get; set; }

        /// <summary>供应派系的综合声望归一化值（0~1），决定职业权重与属性强度。</summary>
        public float ReputationNormalized { get; set; }

        /// <summary>随机到的职业（可能为 null = 无职业）。</summary>
        public IProfessionData? Profession { get; set; }

        /// <summary>体格（最大生命）随机倍率（随声望提高）。</summary>
        public float HealthMultiplier { get; set; } = 1f;
    }

    private sealed class CapturedRoll : ProducedProfile
    {
        /// <summary>物种（有血脉的进化种族，或无血脉的哥布林）；基础种族时为 null。</summary>
        public ISpeciesData? Species { get; set; }

        /// <summary>基础种族核心（无物种身份、无血脉）；物种候选时为 null。</summary>
        public ISpeciesCoreData? Core { get; set; }

        /// <summary>生成单位时使用的原版种族类型（物种一律 Human，随后由 SetSpecies 重写外观/数值）。</summary>
        public EUnitType UnitType { get; set; } = EUnitType.Human;

        /// <summary>本次是否为「带血脉」的物种（基础种族与哥布林为 false）。</summary>
        public bool HasBloodline { get; set; }
    }

    /// <summary>
    /// 营地生成奴隶的种族候选：有血脉的物种 / 无血脉的物种（哥布林）/ 基础种族核心（无物种、无血脉）。
    /// </summary>
    public sealed class CaptiveSpeciesCandidate
    {
        public ISpeciesData? Species { get; set; }
        public ISpeciesCoreData? Core { get; set; }
        public EUnitType UnitType { get; set; } = EUnitType.Human;
        public bool HasBloodline { get; set; }

        /// <summary>出现概率（%）。</summary>
        public float WeightPercent { get; set; }

        /// <summary>唯一键：物种 InternalName 或「Base:核心 InternalName」。</summary>
        public string Key => Species != null ? Species.InternalName : "Base:" + (Core?.InternalName ?? "?");
    }

    /// <summary>
    /// 随机决定生成奴隶的种族，并掷出「职业 / 派系 / 体格」生成参数。
    /// ★ 种族规则（本轮修改，与职业同源）：先掷「供应派系」（已解锁派系随机），
    ///   再由该派系的**血脉**（<see cref="FactionData.Bloodlines"/>）与**该派系声望**共同决定种族。
    /// </summary>
    private static CapturedRoll? RollCapturedSlave()
    {
        // ① 供应派系（种族与职业都以它为准，职业不再重新掷派系）
        FactionData? faction = FactionSystem.RollUnlockedFaction();
        int reputationScore = faction != null ? FactionSystem.GetFactionReputationScore(faction.Id) : 0;

        // ② 按「派系血脉 + 该派系声望」掷种族
        CaptiveSpeciesCandidate? candidate = RollCaptiveSpecies(faction, reputationScore);
        if (candidate == null)
            return null;

        // ③ 职业 / 属性强度 / 体格（猎手营地俘虏与职业傀儡共用同一套规则）
        ProducedProfile profile = RollProducedProfile(candidate.Species, faction, candidate.Core?.Race);

        return new CapturedRoll
        {
            Species = candidate.Species,
            Core = candidate.Core,
            UnitType = candidate.UnitType,
            HasBloodline = candidate.HasBloodline,
            Faction = profile.Faction,
            ReputationNormalized = profile.ReputationNormalized,
            Profession = profile.Profession,
            HealthMultiplier = profile.HealthMultiplier,
        };
    }

    /// <summary>按候选概率（<see cref="CaptiveSpeciesCandidate.WeightPercent"/>）抽取一个种族。</summary>
    private static CaptiveSpeciesCandidate? RollCaptiveSpecies(FactionData? faction, int reputationScore)
    {
        List<CaptiveSpeciesCandidate> candidates = ComputeCaptiveSpeciesCandidates(faction, reputationScore);
        return candidates.Count == 0 ? null : PickWeighted(candidates, c => c.WeightPercent);
    }

    /// <summary>
    /// 核心种族 → 原版 EUnitType（Human → Human、Gnome → Dwarf…），用于生成基础种族奴隶。
    /// </summary>
    private static bool TryGetUnitTypeForCore(ISpeciesCoreData core, out EUnitType unitType)
    {
        foreach (EUnitType candidate in Enum.GetValues(typeof(EUnitType)))
        {
            if (!GameData.TryGetCoreInternalNameByUnitType(candidate, out string? internalName))
                continue;
            if (string.Equals(internalName, core.InternalName, StringComparison.OrdinalIgnoreCase))
            {
                unitType = candidate;
                return true;
            }
        }
        unitType = EUnitType.Human;
        return false;
    }

    /// <summary>
    /// 计算「供应派系 + 该派系声望」下所有种族候选的出现概率（%）。规则：
    ///   1) 种族池 = 该派系允许的血脉（<see cref="FactionData.Bloodlines"/>）对应的物种 ——
    ///      王国：自然 / 野性；恶魔：恶魔；怪物：怪物 / 龙之；宗教教团：神圣；奥秘议会：魔力 / 元素；
    ///   2) 传说（品阶 5）每个物种 2% → 20%、史诗（品阶 4）每个 4% → 40%
    ///      （随该派系声望 0 → 30000+ 线性提升，见 <see cref="HunterCampConfig.ComputeSpeciesTierChance"/>）；
    ///      两者之和超过 <see cref="HunterCampConfig.SpeciesHighTierTotalCap"/> 时按同比例整体缩小；
    ///   3) 剩余概率 =「无血脉种族」组 +「品阶 1~3 物种」组；前者占比随声望由 50% 降到 10%
    ///      —— 声望越低，不含血脉的基础种族越常见；
    ///   4) 「无血脉种族」= 8 个基础种族核心（Human / Elf / Gnome / Imp / Lizard / Rabbit / Sheep / Wolf）
    ///      + 进化种族哥布林（物种存在但未映射血脉）；两组内部均匀随机。
    /// </summary>
    public static List<CaptiveSpeciesCandidate> ComputeCaptiveSpeciesCandidates(FactionData? faction, int reputationScore)
    {
        var bloodlineSpecies = new List<ISpeciesData>();
        var noBloodlineSpecies = new List<ISpeciesData>();
        foreach (ISpeciesData s in GameData.SpeciesDataList)
        {
            if (HunterCampConfig.ExcludedProducedSpecies.Contains(s.InternalName, StringComparer.OrdinalIgnoreCase))
                continue;

            if (SpeciesBloodlineMap.TryGetBySpeciesName(s.InternalName, out EBloodline bloodline, out _))
            {
                if (faction == null || faction.Bloodlines.Contains(bloodline))
                    bloodlineSpecies.Add(s);
            }
            else
            {
                noBloodlineSpecies.Add(s);   // 无血脉的物种（哥布林）
            }
        }

        var result = new List<CaptiveSpeciesCandidate>();
        List<ISpeciesData> highTier = bloodlineSpecies.Where(s => s.Tier >= 4).ToList();
        List<ISpeciesData> midTier = bloodlineSpecies.Where(s => s.Tier <= 3).ToList();

        // 高阶：每个物种各自按品阶算概率，求和后超上限则整体等比缩小
        float highTotal = 0f;
        foreach (ISpeciesData s in highTier)
            highTotal += HunterCampConfig.ComputeSpeciesTierChance(s.Tier, reputationScore);
        float highScale = highTotal > HunterCampConfig.SpeciesHighTierTotalCap
            ? HunterCampConfig.SpeciesHighTierTotalCap / highTotal
            : 1f;

        float rest = Mathf.Max(0f, 100f - highTotal * highScale);
        float noBloodlineShare = rest * HunterCampConfig.ComputeNoBloodlineShare(reputationScore) / 100f;
        float midShare = Mathf.Max(0f, rest - noBloodlineShare);

        foreach (ISpeciesData s in highTier)
        {
            result.Add(new CaptiveSpeciesCandidate
            {
                Species = s,
                UnitType = EUnitType.Human,
                HasBloodline = true,
                WeightPercent = HunterCampConfig.ComputeSpeciesTierChance(s.Tier, reputationScore) * highScale,
            });
        }

        if (midTier.Count > 0)
        {
            foreach (ISpeciesData s in midTier)
            {
                result.Add(new CaptiveSpeciesCandidate
                {
                    Species = s,
                    UnitType = EUnitType.Human,
                    HasBloodline = true,
                    WeightPercent = midShare / midTier.Count,
                });
            }
        }
        else
        {
            // 该派系没有品阶 1~3 物种（如宗教教团只有神裔 / 天使 / 堕天使）→ 余量并入「无血脉」组
            noBloodlineShare += midShare;
        }

        var noBloodline = new List<CaptiveSpeciesCandidate>();
        foreach (ISpeciesCoreData core in GameData.SpeciesCoreDataList)
        {
            if (!TryGetUnitTypeForCore(core, out EUnitType unitType))
                continue;
            noBloodline.Add(new CaptiveSpeciesCandidate
            {
                Core = core,
                UnitType = unitType,
                HasBloodline = false,
            });
        }
        foreach (ISpeciesData s in noBloodlineSpecies)
        {
            noBloodline.Add(new CaptiveSpeciesCandidate
            {
                Species = s,
                UnitType = EUnitType.Human,
                HasBloodline = false,
            });
        }

        if (noBloodline.Count > 0)
        {
            foreach (CaptiveSpeciesCandidate c in noBloodline)
                c.WeightPercent = noBloodlineShare / noBloodline.Count;
            result.AddRange(noBloodline);
        }
        else if (result.Count > 0)
        {
            // 兜底（理论上不会发生）：余量平均分给已有候选
            foreach (CaptiveSpeciesCandidate c in result)
                c.WeightPercent += noBloodlineShare / result.Count;
        }

        return result;
    }

    /// <summary>
    /// 掷出生成参数：供应派系（已解锁派系随机）→ 声望系数 → 职业（派系声望加权）→ 体格倍率（随声望提高）。
    /// 猎手营地俘虏与职业傀儡共用。
    /// </summary>
    public static ProducedProfile RollProducedProfile(ISpeciesData? species)
        => RollProducedProfile(species, FactionSystem.RollUnlockedFaction(), null);

    /// <summary>
    /// 掷出生成参数（**指定供应派系**，不重新随机派系）：
    /// 声望系数 → 职业（派系声望加权）→ 体格倍率（随声望提高）。
    /// <paramref name="baseRace"/> = 基础种族核心的 ERace（本次是无物种的基础种族时用于职业的
    /// 「禁用初始种族」判定；为 null 表示不额外判定）。
    /// </summary>
    public static ProducedProfile RollProducedProfile(ISpeciesData? species, FactionData? faction, ERace? baseRace)
    {
        // ★ 只用「模组派系声誉分数」（不含玩家原版声望）：
        //   声望越高 → 生成奴隶的属性越接近最优端、血脉等级越高
        float reputationNormalized = faction != null
            ? FactionSystem.GetFactionReputationNormalized(faction.Id)
            : 0f;

        return new ProducedProfile
        {
            Faction = faction,
            ReputationNormalized = reputationNormalized,
            Profession = RollProfession(species, baseRace, faction?.Id ?? -1),
            // 声望越高，生成单位的体格（最大生命）越好
            HealthMultiplier = Random.Range(HunterCampConfig.HealthRollMin, HunterCampConfig.HealthRollMax)
                               * (1f + reputationNormalized * HunterCampConfig.HealthReputationBoost),
        };
    }

    /// <summary>
    /// 应用生成参数：写入职业 → 记录供应派系 → 写入 Trait93~Trait100 属性（受声望影响）
    /// → 按体格倍率设置最大生命/体力。猎手营地俘虏与职业傀儡共用。
    /// </summary>
    public static void ApplyProducedProfile(Female female, ISpeciesData? species, ProducedProfile? profile)
    {
        if (female == null || profile == null)
            return;

        if (profile.Profession != null)
            AssignProfession(female, profile.Profession);

        if (profile.Faction != null)
            FactionSystem.SetBuffInt(ConfigBuffs.FactionCaptiveFaction, profile.Faction.Id + 1, female);

        ApplyProducedStatTraits(female, species, profile.ReputationNormalized);

        // ★ 血脉：种类按「物种」（SpeciesBloodlineMap，与市场/繁殖一致），等级按「供应派系声望」
        ApplyProducedBloodline(female, species, profile.ReputationNormalized);

        float baseHealth = species?.MaxHealth ?? female.MaxHealth;
        float maxHealth = Mathf.Clamp(profile.HealthMultiplier * baseHealth,
            GameManager.ConfigData.MinMaxMentality, GameManager.ConfigData.MaxMaxMentality);
        female.MaxHealth = maxHealth;
        female.Health = female.MaxHealth;
    }

    /// <summary>
    /// 按「派系声望加权」抽取职业：品阶基础权值（传说0.5 / 史诗1 / 稀有2 / 不凡12 / 普通30）
    /// + 声望向平均靠拢 + 稀有职业与派系关联职业加成（详见 <see cref="FactionProfessionAffinity"/>）；
    /// 同时过滤该种族的禁用职业。可能返回 null（无合适职业）。
    /// </summary>
    private static IProfessionData? RollProfession(ISpeciesData? species, ERace? baseRace, int factionId)
        => FactionProfessionAffinity.Roll(species, factionId, GameData.ProfessionDataList, baseRace);

    /// <summary>把职业写入角色的 ETrait(10) 一般特质槽位（与 CBR 职业系统一致）。</summary>
    private static void AssignProfession(Female female, IProfessionData profession)
    {
        if (!female.TraitContains(GameBalance.ProfessionTraitId))
            female.AddTrait(GameBalance.ProfessionTraitId);
        female.SetTraitValue(GameBalance.ProfessionTraitId, profession.Id);

        if (profession.GuaranteedTraits != null)
        {
            foreach (ETrait trait in profession.GuaranteedTraits)
            {
                if (!female.TraitContains(trait))
                    female.AddRaceTrait(trait);
            }
        }
    }

    /// <summary>
    /// 写入 Trait93~Trait100 属性块：SetSpecies 会清空一般特质，因此这里重新写入。
    /// 数值由「供应派系声誉分数（0~99999 → 0~1）」向各属性的最优端插值：
    ///   · 声望 99999：Trait93~Trait97 落在最优端最后 10% 区间
    ///     （Trait96 ≈ 9049~9999、Trait97 ≈ 9000~9999、Trait98 ≈ 899~999），
    ///     Trait99 交配时间 / Trait100 交配体力消耗 随机（不随声望变动）；
    ///   · 声望较低：可能出现负面属性（Trait93 受孕率 / Trait94 成长时间 /
    ///     Trait95 维护费 / Trait97 生育上限 的最差端为负）。
    /// 种族品阶只提供少量起点加成，不会突破最优端。
    /// </summary>
    private static void ApplyProducedStatTraits(Female female, ISpeciesData? species, float reputationNormalized)
    {
        if (female == null)
            return;

        float tierQualityBonus = HunterCampConfig.GetStatTraitTierQualityBonus(species?.Tier ?? 1);
        foreach (KeyValuePair<ETrait, (float Worst, float Best)> kv in HunterCampConfig.ProducedStatTraitRanges)
        {
            ETrait trait = kv.Key;
            float value = HunterCampConfig.RollProducedStatValue(trait, tierQualityBonus, reputationNormalized);

            if (!female.TraitContains(trait))
                female.AddTrait(trait);
            female.SetTraitValue(trait, value);
        }
    }

    /// <summary>
    /// ★ 写入血脉：**种类以物种为准**（<see cref="SpeciesBloodlineMap"/>，与市场/繁殖一致的物种↔血脉对应关系），
    /// 等级按「供应派系声望」插值：声望 99999 → 9~10 级；声望 0 → 1~2 级，且**不低于该物种的最低等级**
    /// （详见 HunterCampConfig.RollProducedBloodlineLevel）。
    /// 该物种未登记血脉时**不添加血脉**（保持与物种一致，不再随机给一种派系血脉）；
    /// 若希望某个基础种族也带血脉，请在 SpeciesBloodlineMap 里补一行（例如 "Goblin" → 怪物血脉 +1）。
    /// </summary>
    private static void ApplyProducedBloodline(Female female, ISpeciesData? species, float reputationNormalized)
    {
        if (female == null)
            return;

        string? speciesName = species?.InternalName ?? GameData.GetSpeciesIdentity(female);
        if (string.IsNullOrWhiteSpace(speciesName)
            || !SpeciesBloodlineMap.TryGetBySpeciesName(speciesName, out EBloodline bloodline, out int speciesMinLevel))
        {
            return;   // 该物种没有对应血脉 → 保持无血脉（不再随机）
        }

        int level = Mathf.Max(
            HunterCampConfig.RollProducedBloodlineLevel(reputationNormalized, BloodlineSystem.MaxLevel),
            speciesMinLevel);

        BloodlineSystem.SetBloodline(female, bloodline, level);

        Log($"{female.DisplayName} 血脉（按物种 {speciesName}）：{bloodline} +{level}");
    }

    /// <summary>
    /// 读取俘虏的「供应派系」（无记录时返回 null）。值 = 派系 Id + 1，见 ConfigBuffs.FactionCaptiveFaction。
    /// </summary>
    public static FactionData? GetCaptiveFaction(Female? female)
    {
        if (female == null)
            return null;
        int stored = FactionSystem.GetBuffInt(ConfigBuffs.FactionCaptiveFaction, female);
        if (stored <= 0)
            return null;
        FactionSystem.TryGetFaction(stored - 1, out FactionData? faction);
        return faction;
    }

    private static T PickWeighted<T>(List<T> source, Func<T, float> weight)
    {
        float total = 0f;
        foreach (T item in source)
            total += Mathf.Max(0f, weight(item));

        float roll = Random.Range(0f, Mathf.Max(0.001f, total));
        float cumulative = 0f;
        foreach (T item in source)
        {
            cumulative += Mathf.Max(0f, weight(item));
            if (roll < cumulative)
                return item;
        }
        return source[source.Count - 1];
    }

    // ============================ 本地化与日志 ============================

    private static string Loc(string key)
        => Strings.ResourceManager.GetString(key, Strings.Culture) ?? key;

    internal static void Log(string msg)
    {
        if (HunterCampConfig.EnableLogging)
            Debug.Log($"[RaceBreedingExtension][HunterCamp] {msg}");
    }

    internal static void LogError(string msg)
        => Debug.LogError($"[RaceBreedingExtension][HunterCamp] {msg}");

    // ============================ 全局维护（每秒一次） ============================

    private static float _sweepTimer;

    /// <summary>
    /// 每秒执行一次：
    ///  - 俘虏一旦离开了猎手营地（玩家在乏力状态下把她拖走，或营地被拆除），
    ///    视为“成功救出” → 解除俘虏标记并恢复可拖动，变成普通奴隶；
    ///  - 顺带按当前状态刷新拖动锁（读档后也能立即生效）。
    /// </summary>
    public static void SweepCaptives(PlayData? pd, float tickTime)
    {
        if (!HunterCampConfig.Enable || pd == null || tickTime <= 0f)
            return;

        _sweepTimer += tickTime;
        if (_sweepTimer < 1f)
            return;
        _sweepTimer = 0f;

        try
        {
            foreach (Unit unit in pd.GetUnitList(ESector.Female).ToList())
            {
                if (unit is not Female female || female.IsBaby)
                    continue;
                if (!IsCaptive(female))
                    continue;

                Room? room = female.Room;
                if (room == null)
                    continue;   // 读档/玩家搬动过程中不处理，避免误判

                bool inCamp = room.IsPrivate && GroundCampSystem.IsGroundCamp(room);
                if (inCamp)
                {
                    RefreshCaptiveLock(female);
                    continue;
                }

                ReleaseCaptive(female, "已离开猎手营地");
                female.IsNotDraggable = false;
            }
        }
        catch (Exception e)
        {
            LogError("SweepCaptives failed: " + e);
        }
    }

    /// <summary>
    /// 拖动前的最终校验：被锁定的俘虏不允许被拖动（兜底，主判定为 IsNotDraggable）。
    /// 乏力(SexDrained/Drained)、耐力低于 30%（即使处于 Rest）、堕落、死亡，
    /// 以及原版自管的状态（分娩/卡片/工作等）一律放行。
    /// </summary>
    public static bool CanStartDrag(Unit? unit)
    {
        if (unit is not Female female || !IsCaptive(female))
            return true;
        if (female.IsDead || female.IsDepraved)
            return true;
        if (female.State == EState.Drained || female.State == EState.SexDrained)
            return true;
        // ★ 耐力低于乏力阈值（默认 30%）时解除锁定：Rest 状态下也能拖动
        if (IsStaminaLow(female))
            return true;

        return IsVanillaManagedState(female.State);
    }

    /// <summary>供 Reposition 补丁调用：刷新俘虏显示层级。</summary>
    public static void RefreshLayerOrder(Female? female)
        => ApplyCaptiveLayerOrder(female, female?.Room);
}

