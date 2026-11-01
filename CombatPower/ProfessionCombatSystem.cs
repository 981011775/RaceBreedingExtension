#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using ComplexBreedingRedux.Data.Professions;
using DepravedShield.Helpers;
using MBMScripts;
using RaceBreedingExtension.GroundCamp;
using RaceBreedingExtension.HunterCamp;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RaceBreedingExtension.CombatPower;

/// <summary>
/// 职业的战斗行为（依据「修改的职业(新).txt」）：
///   - 在 <b>触发 EState.Sex</b> 与 <b>Sex 场景结束</b> 两个时机各判定一次；
///   - 只在持有者「未堕落」（!IsDepraved）时生效；
///   - 单体模式：战斗力 &gt; 怪物 → KillChance% 秒杀；战斗力 ≥ 怪物 ×2 → 100% 秒杀；
///   - 房间模式：房间内所有战斗力低于自己的怪物 → KillChance% 秒杀
///     （猎手营地 / 大型猎手营地 / 交配室 / 大型交配室，只影响自己所在房间）；
///   - ★ <b>玩家角色（<see cref="Player"/>，EUnitType.Player）免疫上述全部斩杀效果</b>：
///     玩家本人也是一个 <see cref="Male"/>（可与奴隶发生性行为，见 <see cref="Player"/> 的状态机），
///     若不免疫就会被奴隶身上的战斗职业当成「怪物」直接斩杀（玩家小人死亡）。
/// </summary>
internal static class ProfessionCombatSystem
{
    /// <summary>斩杀时的淡出时长（秒），淡出结束后移除怪物。</summary>
    private const float KillFadeSeconds = 1f;

    /// <summary>
    /// 斩杀免疫：玩家角色（<see cref="Player"/>，EUnitType.Player）免疫所有职业的斩杀效果。
    ///
    /// 玩家自身是 <see cref="Male"/> 的子类，与奴隶发生性行为时会走同一套 <c>Male.Sex</c> 状态，
    /// 因而会被本系统的单体斩杀判定选中（战斗力通常远低于职业奴隶）→ 玩家小人在性行为中被秒杀。
    /// 单体斩杀、房间斩杀都在目标筛选阶段排除，<see cref="KillMonster"/> 再兜底一次。
    /// </summary>
    private static bool IsKillImmune(Character? target) => target is Player;

    /// <summary>Sex 开始（触发 EState.Sex）时的判定。</summary>
    public static void OnSexStarted(Female? female, Character? monster)
        => Evaluate(female, monster, "Sex 开始");

    /// <summary>Sex 场景结束时的判定。</summary>
    public static void OnSexEnded(Female? female, Character? monster)
        => Evaluate(female, monster, "Sex 结束");

    private static void Evaluate(Female? female, Character? monster, string timing)
    {
        try
        {
            if (female == null || female.IsDead || female.IsDepraved)
                return;

            // 触手房内不触发任何斩杀效果
            if (IsKillForbiddenRoom(female))
                return;

            IProfessionData? profession = CombatPowerSystem.TryGetProfession(female);
            if (profession == null || profession.KillMode == EProfessionKillMode.None)
                return;

            switch (profession.KillMode)
            {
                case EProfessionKillMode.SingleTarget:
                    EvaluateSingleTarget(female, monster, profession, timing);
                    break;
                case EProfessionKillMode.RoomWeakMonsters:
                    EvaluateRoomWeakMonsters(female, profession, timing);
                    break;
            }
        }
        catch (Exception e)
        {
            Debug.LogError("[CombatPower] Evaluate failed: " + e);
        }
    }

    // ─── 单体：战斗力 > 怪物 → 几率秒杀；≥2 倍 → 必杀 ────────────────────────

    private static void EvaluateSingleTarget(Female female, Character? monster, IProfessionData profession, string timing)
    {
        if (monster == null || monster.IsDead)
            return;

        // ★ 玩家角色免疫斩杀：绝不把玩家小人当作斩杀目标（连判定都不做）
        if (IsKillImmune(monster))
            return;

        float myPower = CombatPowerSystem.GetSlavePower(female);
        float enemyPower = CombatPowerSystem.GetMonsterPower(monster);
        if (myPower <= enemyPower)
            return;

        int chance = profession.KillChance > 0 ? profession.KillChance : 25;
        if (myPower >= enemyPower * 2f)
            chance = CombatPowerConfig.DoublePowerKillChance;

        if (Random.Range(1, 101) > chance)
            return;

        KillMonster(female, monster, profession, timing);
    }

    // ─── 房间：房间内所有弱于自己的怪物（猎手营地 / 大型猎手营地 / 交配室 / 大型交配室） ──

    private static void EvaluateRoomWeakMonsters(Female female, IProfessionData profession, string timing)
    {
        Room? room = female.Room;
        if (room == null || !IsKillEffectRoom(room))
            return; // 只影响此奴隶所在房间

        int chance = profession.KillChance > 0 ? profession.KillChance : 25;
        float myPower = CombatPowerSystem.GetSlavePower(female);

        PlayData? pd = GameManager.Instance?.PlayerData;
        if (pd == null)
            return;

        List<Character> targets = new List<Character>();
        foreach (Unit unit in pd.GetUnitList(ESector.Male, room.Slot))
        {
            if (unit is not Character monster || monster.IsDead || monster.IsBaby)
                continue;
            if (IsKillImmune(monster))
                continue;   // ★ 玩家角色免疫斩杀
            if (myPower > CombatPowerSystem.GetMonsterPower(monster))
                targets.Add(monster);
        }

        foreach (Character monster in targets)
        {
            if (Random.Range(1, 101) > chance)
                continue;
            KillMonster(female, monster, profession, timing);
        }
    }

    /// <summary>
    /// 允许“房间内斩杀”的房间：猎手营地 / 大型猎手营地（个人领地地面），
    /// 以及交配室 BreedingRoom / 大型交配室 BreedingRoom2。
    /// </summary>
    private static bool IsKillEffectRoom(Room room)
    {
        if (room.RoomType == ERoomType.BreedingRoom || room.RoomType == ERoomType.BreedingRoom2)
            return true;
        return room.IsPrivate && GroundCampSystem.IsGroundCamp(room);
    }

    /// <summary>触手房（TentacleRoom）内不触发任何斩杀效果（单体与房间内斩杀均取消）。</summary>
    private static bool IsKillForbiddenRoom(Female female)
    {
        Room? room = female.Room;
        return room != null && room.RoomType == ERoomType.TentacleRoom;
    }

    // ─── 击杀 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// 斩杀怪物：IsDead = true（游戏逻辑上立即视为死亡）+ 淡出后由原版 <c>PlayerData.RemoveUnit</c> 移除。
    /// <b>刻意不使用 EState.BabyDisintegration</b> —— 那会显示“婴儿尸体”解体动画。
    /// 文本只显示红色的「斩杀怪物」短提示。
    /// </summary>
    private static void KillMonster(Female killer, Character monster, IProfessionData profession, string timing)
    {
        if (monster == null || monster.IsDead)
            return;

        // ★ 兜底：玩家角色免疫所有斩杀效果（无论从哪条路径进入都不允许被斩杀）
        if (IsKillImmune(monster))
        {
            HunterCampSystem.Log($"[CombatPower] {monster.DisplayName} 为玩家角色，免疫斩杀（已忽略，{timing}）");
            return;
        }

        string monsterName = monster.DisplayName;
        float killerPower = CombatPowerSystem.GetSlavePower(killer);
        float monsterPower = CombatPowerSystem.GetMonsterPower(monster);

        // 死亡标记：雄性状态机没有 Dead 分支，会保持静止；不进入 BabyDisintegration（避免婴儿尸体动画）
        monster.IsDead = true;
        monster.IsNotClickable = true;
        monster.IsNotDraggable = true;
        monster.State = EState.Dead;
        monster.StateTime = 0f;
        monster.CompleteStateTime = 0f;

        // 淡出后移除（不使用解体状态 → 不出现婴儿尸体）
        monster.Fade(EFadeType.Out, 1f, 0f, false, KillFadeSeconds);
        ModCoroutineHelper.StartCoroutine(RemoveMonsterAfterFade(monster, KillFadeSeconds));

        // 女性奴隶身上播放与 Trait103 触发时相同的特效（仅特效，不触发 Trait103 的任何效果）
        PlayTrait103LikeEffect(killer);

        killer.PopUpMessage(Loc("Combat_KilledShort"), null, 0.3f);

        HunterCampSystem.Log(
            $"[CombatPower] {killer.DisplayName}（{profession.InternalName}，{timing}）斩杀 {monsterName}：" +
            $"战斗力 {killerPower:0.#} vs {monsterPower:0.#}");
    }

    /// <summary>淡出结束后把怪物从游戏数据中移除。</summary>
    private static IEnumerator RemoveMonsterAfterFade(Character monster, float seconds)
    {
        yield return new WaitForSeconds(seconds);
        try
        {
            if (monster != null)
                GameManager.Instance?.PlayerData?.RemoveUnit(monster);
        }
        catch (Exception e)
        {
            Debug.LogError("[CombatPower] RemoveMonsterAfterFade failed: " + e);
        }
    }

    /// <summary>
    /// 在角色位置播放「Trait103 触发时」使用的特效（原版为 EffectCure 特效器，时长 3 秒）。
    /// 仅播放特效，不触发 Trait103 的任何实际效果（回血/补乳/清零产仔数/治愈性病）。
    /// </summary>
    private static void PlayTrait103LikeEffect(Character? character)
    {
        if (character == null)
            return;

        try
        {
            Effector? effect = SeqObjectPoolManager.Instance?
                .GetPooledObject("EffectCure", null, null)?
                .GetComponent<Effector>();
            if (effect == null)
                return;

            effect.transform.position = character.Position;
            effect.Duration = 3f;
            effect.ActivateAsPooledObject();
        }
        catch (Exception e)
        {
            Debug.LogError("[CombatPower] Trait103-like effect failed: " + e);
        }
    }

    private static string Loc(string key)
        => ComplexBreedingRedux.Properties.Strings.ResourceManager.GetString(
            key, ComplexBreedingRedux.Properties.Strings.Culture) ?? key;
}
