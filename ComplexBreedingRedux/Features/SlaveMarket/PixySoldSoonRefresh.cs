#nullable enable
using System.Collections.Generic;
using MBMScripts;
using RaceBreedingExtension.HunterCamp;

namespace ComplexBreedingRedux.Features.SlaveMarket;

/// <summary>
/// 妖精（Pixy）自动出售：**纯读取层**的判定集合（已不再写任何出售标记）。
///
/// 背景：出售与否完全取决于 <see cref="Unit.SoldSoon"/>，而妖精的搬运流程
/// （游戏源码 <c>Character.UpdatePixy</c>：雄性系 70920、雌性系 37032）只要求
/// 「StateTime ≥ 3 秒、不在拖动中、Room 非空」，**既不看单位是否在动作、也不看房间**：
///   · 婴儿：在产房 / 观赏房里且 <c>MovedSoon</c> → 搬进育婴笼（雌性 → SlaveCage，
///     雄性 → MonsterCage / MonsterCage2）养大；若 <c>SoldSoon</c> 为真则直接送 Depot 卖掉；
///   · 非婴儿（Child / Adult）：所在房间不是产房 / 观赏房且 <c>SoldSoon</c> 为真 → 送 Depot 卖掉。
///
/// 本 mod 的策略（只做两件事）：
///   ① 标记**只来自游戏原版**：出生时若 <c>FloraPixyCount &gt; 0</c>，由 GiveBirth 给新生儿打上
///      （源码 22121-22131：<c>SoldSoon = PixySell &amp;&amp; FloraPixySell</c>、
///      <c>MovedSoon = PixyBaby &amp;&amp; FloraPixyBaby</c>）。mod **不再做任何补发 / 刷新**：
///      早期版本会在「召唤妖精」（女巫等职业的 SummonPixy 工作）后全场重写标记，把玩家手动
///      「取消出售」过的单位也一起重新标记并卖掉，现已彻底删除该刷新；
///   ② 卖不卖由读取层决定（<see cref="SoldSoonPatch"/> 的 Unit.SoldSoon getter 补丁调用本类）：
///      动作中（只是延后）、婴儿期（**成长完成后才卖**）、猎手营地俘虏、以及不是
///      「在本设施出生长大」的单位（搬进来的成体 / 买来的奴隶）→ 一律读出为 false。
///
/// ⚠ 教训（务必不要重复）：**不要按房间做保护**。奴笼 / 怪物笼 / 产房 / 观赏房都只是妖精的搬运
///   目的地（游戏默认把雌性安置到 SlaveCage、雄性安置到 MonsterCage / MonsterCage2、婴儿安置到
///   母亲所在的 DeliveryRoom / BirthShowRoom），一旦把这些房间列为保护区，就等价于
///   「所有子代都卖不掉」（上一版就是这样炸的）。
/// </summary>
internal static class PixySoldSoonRefresh
{
    /// <summary>
    /// 「正在进行动作」的状态：此时绝不能标记出售（否则会在动作中途被妖精搬走）。
    /// 含性行为 / 射精 / 自慰 / 生产 / 移动 / 挤奶 / 互动 / 卡片与胶囊等非静止状态。
    /// </summary>
    private static readonly HashSet<EState> ActionStates = new HashSet<EState>
    {
        EState.Sex, EState.SexDrained, EState.CumShotInside, EState.CumShotOutside,
        EState.Masturbation, EState.MasturbationCumShot, EState.MasturbationBirth,
        EState.Ejaculation, EState.FaceSitting, EState.Touch, EState.NightTouch, EState.NightSmoke,
        EState.Birth, EState.BirthDrained, EState.AfterBirth, EState.AfterBirthDrained, EState.Fetus,
        EState.NewbornBabyUmbilicalCord, EState.Dead,
        EState.Move, EState.MoveDrained, EState.MoveDead, EState.BabyMove,
        EState.MilkStart, EState.MilkEnd, EState.Action,
        EState.Card, EState.CardBaby, EState.Portrait, EState.DeadPortrait, EState.Commission,
        EState.FloraWorkplace, EState.NielWorkplace, EState.Coffin,
        EState.CapsuleStart, EState.CapsuleEnd, EState.Disintegration, EState.BabyDisintegration,
        EState.Effect,
    };

    /// <summary>该单位此刻是否处于「进行中的动作」状态（妖精不得在此时标记 / 搬运它）。</summary>
    internal static bool IsInAction(Character? character)
        => character != null && ActionStates.Contains(character.State);

    /// <summary>该状态是否允许被妖精标记出售（静止 / 休息 / 婴儿待机 / 疲劳 等）。</summary>
    internal static bool IsSellableIdleState(EState state)
        => !ActionStates.Contains(state);

    /// <summary>
    /// 该单位是否「在本设施出生长大」（有父母记录）。
    ///
    /// 游戏里 <c>MotherUnitId</c> / <c>FatherUnitId</c> **只在出生时赋值**（源码 36462 / 70700 两处），
    /// 所以：本设施出生的后代 → true；买来的奴隶、从外部获得后搬进来的成体 → false。
    /// 这正是「自己养大的后代可以卖、搬进来的成体不卖」的判定依据。
    /// </summary>
    internal static bool IsRaisedHere(Character? character)
        => character != null && (character.MotherUnitId > 0 || character.FatherUnitId > 0);

    /// <summary>
    /// 该单位此刻是否受保护、不得被妖精搬走出售：
    ///   ① 正在动作（性行为 / 射精 / 生产 / 移动 / 挤奶…）→ 延后（只是推迟，不是取消）；
    ///   ② 仍在婴儿期 → 等成长完成后再卖（原版会把 <c>SoldSoon</c> 的婴儿直接送 Depot）；
    ///   ③ 猎手营地俘虏 → 永不自动出售；
    ///   ④ 不是「在本设施出生长大」的单位（无父母记录的成体）→ 永不自动出售。
    /// 供 <see cref="SoldSoonPatch"/>（Unit.SoldSoon 读取层）与刷新共用。
    /// </summary>
    internal static bool IsProtectedFromPixySale(Character? character)
    {
        if (character == null)
            return false;

        return IsInAction(character)                 // ① 动作中：延后到动作结束
            || character.IsBaby                      // ② 婴儿期：成长完成后才允许出售
            || HunterCampSystem.IsCaptive(character) // ③ 营地俘虏：永不自动出售
            || !IsRaisedHere(character);             // ④ 搬进来的成体（无父母记录）：永不自动出售
    }

}
