#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using ComplexBreedingRedux.Data;
using MBMScripts;
using UnityEngine;

namespace RaceBreedingExtension.Compat
{
    /// <summary>
    /// 模组自用特质槽位的「合法化」修复（修复原版每帧空引用刷屏）。
    ///
    /// 【问题】模组把血脉 / 职业 / 保证种族特质存进了原版特质槽位，但这三个槽位 Id
    /// （9 = 血脉、10 = 职业、30 = Princess 的保证种族特质）在 ETrait 枚举里并不存在，
    /// 原版 TraitData 数据库中也没有它们（Export\TraitData.json 只有 48 项：
    /// 44,45,47,51,52,56,59~67,70~77,80,82,83,85,87,90,92~110）。
    /// 而 Database&lt;TraitData&gt;.GetDataByDataId() 对不存在的 Id 返回 default(T) = null。
    ///
    /// 【后果】原版多处代码在遍历角色特质后直接访问 TraitData.IsPositive，且没有 null 判断：
    ///   - MBMScripts.Male.Idle（Trait70 分支，由 Male.OnFixedUpdate 每帧执行）
    ///   - Character.TryGetIsPositiveRaceTrait（种族特质着色）
    ///   - Character 的 Trait67 / Trait90 特质合并分支
    /// 于是每帧抛出 NullReferenceException: MBMScripts.Male.Idle (System.Single deltaTime)，
    /// 把 output_log.txt / ModLoader.log 刷屏，并中断单位的状态更新。
    ///
    /// 【修复】模组初始化时把这三个槽位以「中性且正向（IsPositive = true）」的 TraitData
    /// 注册进 Database&lt;TraitData&gt;.DataIdDictionary，使原版所有
    /// GetDataByDataId(...).IsPositive 访问都合法、不再空引用。
    ///   · 只写 DataIdDictionary（按 Id 查询用），不动 List / DataNameDictionary，
    ///     因此不会在「特质一览」之类枚举全部特质的原版界面上凭空多出条目；
    ///   · IsPositive = true 是刻意的：血脉编码 / 职业 Id 恒为正数，若被判为「负向特质」，
    ///     原版会把它当成方向错误的特质计入处罚（例如男性因此拒绝性行为）。
    /// </summary>
    internal static class ModTraitSlots
    {
        /// <summary>
        /// 需要注册进 TraitData 数据库的模组自用槽位 Id：
        ///   9  = 血脉槽位（Bloodline.BloodlineSystem.SlotTrait）
        ///   10 = 职业槽位（GameBalance.ProfessionTraitId）
        ///   30 = Princess 职业的保证种族特质（ProfessionRegistry.Princess）
        /// </summary>
        private static readonly int[] CustomSlotIds =
        {
            (int)RaceBreedingExtension.Bloodline.BloodlineSystem.SlotTrait,
            (int)GameBalance.ProfessionTraitId,
            30,
        };

        private static bool _installed;
        private static Dictionary<int, TraitData>? _idDictionary;

        /// <summary>
        /// 由 RaceBreedingExtension.ModEntry.Load() 在其它子系统初始化之前调用一次。
        /// 必须在任何代码写入这些槽位之前完成，否则原版读取时仍会空引用。
        /// </summary>
        public static void Install()
        {
            if (_installed) return;
            _installed = true;

            try
            {
                FieldInfo? field = typeof(Database<TraitData>).GetField(
                    "DataIdDictionary", BindingFlags.NonPublic | BindingFlags.Static);
                if (field == null)
                {
                    Debug.LogError("[RaceBreedingExtension] 未找到 TraitData 数据库的 DataIdDictionary 字段，自用特质槽位未注册。");
                    return;
                }

                _idDictionary = field.GetValue(null) as Dictionary<int, TraitData>;
                if (_idDictionary == null)
                {
                    Debug.LogError("[RaceBreedingExtension] TraitData 数据库尚未初始化，自用特质槽位未注册。");
                    return;
                }

                int added = 0;
                foreach (int id in CustomSlotIds)
                {
                    if (Register(id))
                        added++;
                }

                Debug.Log($"[RaceBreedingExtension] 自用特质槽位已注册进 TraitData 数据库：本次新增 {added} 个 / 共 {CustomSlotIds.Length} 个（9=血脉, 10=职业, 30=Princess 保证种族特质）。");
            }
            catch (Exception e)
            {
                Debug.LogError("[RaceBreedingExtension] 注册自用特质槽位失败：" + e);
            }
        }

        /// <summary>注册单个槽位（原版已有该 Id 或已注册过则跳过）。返回 true 表示本次新建了数据。</summary>
        private static bool Register(int id)
        {
            if (_idDictionary == null)
                return false;

            // 原版已有该 Id，或已被本方法注册过 → 不覆盖
            if (Database<TraitData>.GetDataByDataId(id) != null)
                return false;

            TraitData data = new TraitData();
            data.m_DataId = id;                        // Data.m_DataId / m_DataName 是 public 字段
            data.m_DataName = "ModTraitSlot_" + id;

            // 其余成员都是 private [SerializeField]，只能反射写入；
            // 即使个别字段改名/缺失也不影响主要修复（关键字段是 m_IsPositive）。
            SetField(data, "m_IsPositive", true);
            SetField(data, "m_IsMonsterTrait", false);
            SetField(data, "m_IsSlaveTrait", false);
            SetField(data, "m_Price", 0);
            SetField(data, "m_ValueList", new float[] { 0f, 0f });
            SetField(data, "m_Name", string.Empty);
            SetField(data, "m_Tooltip", string.Empty);

            _idDictionary[id] = data;
            return true;
        }

        /// <summary>反射写入 TraitData 的私有字段（失败只记日志，不影响其它字段）。</summary>
        private static void SetField(TraitData data, string fieldName, object value)
        {
            try
            {
                FieldInfo? field = typeof(TraitData).GetField(
                    fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
                if (field == null)
                {
                    if (fieldName == "m_IsPositive")
                        Debug.LogError("[RaceBreedingExtension] TraitData.m_IsPositive 字段不存在，自用特质槽位可能仍被原版判为负向特质。");
                    return;
                }

                field.SetValue(data, value);
            }
            catch (Exception e)
            {
                Debug.LogError($"[RaceBreedingExtension] 写入 TraitData.{fieldName} 失败：{e.Message}");
            }
        }
    }
}
