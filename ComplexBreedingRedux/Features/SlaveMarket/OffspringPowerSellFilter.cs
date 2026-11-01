#nullable enable
using System;
using MBM.ModLoader.Settings;
using MBMScripts;
using RaceBreedingExtension.CombatPower;
using UnityEngine;

namespace ComplexBreedingRedux.Features.SlaveMarket
{
    /// <summary>
    /// 子代战斗力过滤出售（模组菜单**第一行**开关，基于本模组的战斗力系统）。
    ///
    /// 规则（战斗力口径见 <see cref="CombatPowerSystem"/>）：
    ///   · 女性子代：自身战斗力 &gt; 母系奴隶（<see cref="Character.MotherUnitId"/>）的战斗力 → 妖精不出售；
    ///   · 怪物（雄性）子代：自身战斗力 &gt; 父系怪物（<see cref="Character.FatherUnitId"/>）的战斗力 → 妖精不出售；
    ///   · 亲代已不存在（已被出售 / 死亡）或没有父母记录 → 无从比较，不拦截（照原版规则出售）。
    ///
    /// 生效位置：<c>Unit.SoldSoon</c> 读取层补丁（<see cref="SoldSoonPatch"/>）——妖精（Pixy）自动出售的
    /// 唯一判定入口。因此本功能：
    ///   · 只影响「妖精自动出售」，不影响玩家手动出售；
    ///   · 只做读取层减法，**不改写任何存档字段**，关掉开关后行为立刻恢复原版；
    ///   · 判定过程整体 try/catch，绝不允许因异常中断妖精流程。
    /// </summary>
    internal static class OffspringPowerSellFilter
    {
        /// <summary>模组菜单开关键（ModName 由合并模组共用，键名保持唯一）。</summary>
        private const string KEY_ENABLE = "OffspringPowerSellFilter";

        /// <summary>是否启用「子代战斗力过滤出售」（模组菜单开关，默认关闭）。</summary>
        public static bool Enable { get; private set; }

        /// <summary>
        /// 注册模组菜单开关。由 <see cref="ComplexBreedingRedux.ModConfig.Register"/> 调用，
        /// 而 ModConfig.Register 是合并模组里**第一个**注册设置的地方
        /// （RaceBreedingExtension.ModEntry.Load() → ComplexBreedingRedux.Plugin.Initialize() →
        /// ModConfig.Register()，早于 UniqueLookColor / DepravedShield 等子系统），
        /// 因此本开关显示在模组菜单第一行。
        /// </summary>
        public static void Register()
        {
            ModSettings.RegisterBool(
                RaceBreedingExtension.ModEntry.ModName, KEY_ENABLE, false, OpsLoc.Config_OffspringPowerSellFilter);
            ModSettings.OnChanged(RaceBreedingExtension.ModEntry.ModName, KEY_ENABLE, _ => RefreshValues());
            RefreshValues();
        }

        /// <summary>
        /// 语言切换时刷新菜单描述。
        /// 由 ComplexBreedingRedux.Plugin.OnLanguageChanged → <see cref="ComplexBreedingRedux.ModConfig.UpdateDescriptions"/> 调用。
        /// </summary>
        public static void OnLanguageChanged()
        {
            ModSettings.SetDescription(
                RaceBreedingExtension.ModEntry.ModName, KEY_ENABLE, OpsLoc.Config_OffspringPowerSellFilter);
        }

        private static void RefreshValues()
        {
            Enable = ModSettings.GetBool(RaceBreedingExtension.ModEntry.ModName, KEY_ENABLE);
        }

        /// <summary>
        /// 该子代是否因「战斗力高于对应亲代」而不得被妖精自动出售。
        /// 女性子代与母系奴隶比较；怪物（雄性）子代与父系怪物比较。
        /// </summary>
        public static bool ShouldProtectFromSale(Character? offspring)
        {
            if (!Enable || offspring == null)
                return false;

            try
            {
                // 女性子代 → 母系奴隶；怪物（雄性）子代 → 父系怪物
                int parentUnitId = offspring is Female
                    ? offspring.MotherUnitId
                    : offspring.FatherUnitId;

                if (parentUnitId <= 0)
                    return false;

                if (GameManager.Instance?.PlayerData?.GetUnit(parentUnitId) is not Character parent)
                    return false;   // 亲代已不在（被出售 / 死亡）→ 无从比较，不拦截

                return CombatPowerSystem.GetCombatPower(offspring) > CombatPowerSystem.GetCombatPower(parent);
            }
            catch (Exception e)
            {
                // 读取层补丁绝不允许抛出异常（会中断妖精流程）：出错时按「不拦截」处理
                Debug.LogError("[CBR] 子代战斗力过滤出售判定失败：" + e);
                return false;
            }
        }
    }
}
