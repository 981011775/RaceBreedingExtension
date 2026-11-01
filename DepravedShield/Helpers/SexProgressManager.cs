using MBM.ModLoader.Settings;
using MBMScripts;
using ComplexBreedingRedux.Data;
using ComplexBreedingRedux.Data.Professions;
using System;
using System.Collections.Generic;
using RaceBreedingExtension.Bloodline;
using DepravedShield.Properties;
using UnityEngine;

namespace DepravedShield.Helpers
{
    public static class SexProgressManager
    {
        // 事件：高潮计数变化时触发（参数为角色）
        public static event Action<Character> OnOrgasmCountChanged;
        // 事件：防护结界变化时触发（参数为角色）
        public static event Action<Character> OnDepravedShieldChanged;

        private static Dictionary<int, float> _progress = new();

        // ★ 特殊NPC类型列表（这些NPC在高潮时不损失体力）
        private static readonly HashSet<EUnitType> SpecialNpcTypes = new HashSet<EUnitType>
        {
            EUnitType.Amilia2,
            EUnitType.Flora2,
            EUnitType.Niel2,
            EUnitType.Sena2,
            EUnitType.Lena2,
            EUnitType.Barbara2
        };

        // ★ 护盾特质映射表（特质ID → 护盾值），便于扩展
        private static readonly Dictionary<int, int> _shieldTraitValues = new();

        private static bool IsSpecialNpc(Character character)
        {
            return character != null && SpecialNpcTypes.Contains(character.UnitType);
        }

        public static float GetProgress(Character character)
        {
            if (character == null) return 0f;
            _progress.TryGetValue(character.UnitId, out float val);
            return val;
        }

        public static void ResetProgress(Character character)
        {
            if (character != null)
                _progress[character.UnitId] = 0f;
        }

        public static bool AddProgress(Character character, float amount)
        {
            if (character == null || amount <= 0f) return false;
            float current = GetProgress(character);
            current += amount;
            bool orgasm = CheckAndHandleOrgasm(character, ref current);
            _progress[character.UnitId] = current;
            return orgasm;
        }

        private static float CalculateFinalDamage(Character victim, float baseDamage)
        {
            // ★ 特殊NPC完全免疫高潮伤害
            if (IsSpecialNpc(victim))
                return 0f;

            // ★ Trait104 免疫：若受害者拥有 Trait104 且正在与触手性交，则伤害为 0
            if (victim.TraitContains(ETrait.Trait104) && victim.SexPartner != null
                && victim.SexPartner.UnitType == EUnitType.Tentacle)
            {
                return 0f;
            }

            float damage = baseDamage;

            // 应用其他伤害减免 Trait（根据原游戏逻辑，如 Trait45、Trait72 等）
            if (victim.TraitContains(ETrait.Trait45))
                damage *= GameManager.GetTraitValue(ETrait.Trait45, 0);
            if (victim.TraitContains(ETrait.Trait72))
                damage *= GameManager.GetTraitValue(ETrait.Trait72, 0);
            // 可根据实际需要添加其他减免 Trait（如 Trait83 等）

            // 堕落状态减免（与原修改一致）
            if (victim.IsDepraved && ModConfig.EnableMentalityHalf)
                damage *= 0.5f;

            // 房间技能影响（若有 DecreasingHealthOnSex 升级）
            Room room = victim.Room;
            if (room != null)
                damage *= (1f + room.GetUpgradeValue(ERoomSkill.DecreasingHealthOnSex));

            // 确保伤害不为负
            return Mathf.Max(0, damage);
        }

        // ★ 唯一的高潮处理方法（整合护盾扣减）
        private static bool CheckAndHandleOrgasm(Character character, ref float progress)
        {
            if (progress >= 1.0f)
            {
                // 计算最终伤害（基础 20，应用各种减免）
                float finalDamage = CalculateFinalDamage(character, 20f);
                character.Health -= finalDamage;

                SexPeriodicDamageHelper.IncrementOrgasmCount(character);
                SexPeriodicDamageHelper.CheckAndUpgradeTrait(character);
                // ★ 使用本地化弹窗
                PopupHelper.ShowPopup(character, Strings.Popup_Orgasm);

                // ★ 高潮时护盾 -10
                ReduceDepravedShield(character, 10);

                // ★ 血脉（Bloodline）高潮效果：堕落时按 5% × 等级 几率获得对应乳汁 / 特质升级药
                BloodlineSystem.OnOrgasm(character);

                progress = 0f;
                return true;
            }
            return false;
        }

        // ★ 进度速度上限（原为 0.15 → 现在 0.22）：Trainer 职业与血脉（Bloodline）加成同样计入该上限。
        public const float MaxSexProgressSpeed = 0.22f;

        // ★ 高潮次数加成：每 300 次高潮 +0.01，达到 1500 次（= +0.05）后不再增加。
        public const int OrgasmCountPerSpeedStep = 300;
        public const int OrgasmCountSpeedBonusMaxCount = 1500;
        public const float OrgasmCountSpeedStep = 0.01f;

        /// <summary>
        /// 高潮次数带来的进度速度加成（纯函数，便于校验）：
        /// 每 OrgasmCountPerSpeedStep 次高潮 +OrgasmCountSpeedStep，
        /// 上限为 OrgasmCountSpeedBonusMaxCount 次 = +0.05；超过后不再增加。
        /// </summary>
        public static float GetOrgasmCountSpeedBonus(int orgasmCount)
        {
            if (orgasmCount <= 0)
                return 0f;

            int maxSteps = OrgasmCountSpeedBonusMaxCount / OrgasmCountPerSpeedStep;  // 1500 / 300 = 5
            int steps = Mathf.Min(orgasmCount / OrgasmCountPerSpeedStep, maxSteps);
            return steps * OrgasmCountSpeedStep;
        }

        // ★ 计算实时进度速度（基于特质、堕落状态、春药Buff、高潮次数与伙伴加成）
        //   统一上限 = MaxSexProgressSpeed（0.22）：Trainer 职业与血脉（Bloodline）加成也受该上限限制。
        public static float GetSexProgressSpeed(Character character)
        {
            if (character == null) return 0f;

            float speed = 0.01f; // 基础速度

            // 特质加成
            if (character.TraitContains((ETrait)47)) speed += 0.02f; // Trait47
            if (character.TraitContains((ETrait)106)) speed += 0.02f; // Trait106
            if (character.TraitContains((ETrait)110)) speed += 0.01f; // Trait110
            if (character.TraitContains((ETrait)104)) speed += 0.01f; // Trait104

            // 堕落状态加成
            if (character.IsDepraved)
                speed += 0.02f;

            // ★ 春药Buff加成（使用游戏内置检测方法）
            if (character.BuffListContains(EBuffType.Aphrodisiac))
                speed += 0.06f;

            // ★ 高潮次数加成：每 300 次高潮 +0.01，1500 次达到 +0.05 后不再增加
            speed += GetOrgasmCountSpeedBonus(SexPeriodicDamageHelper.GetOrgasmCount(character));

            // 伙伴（怪物）加成（同样计入 MaxSexProgressSpeed 上限）：
            //   - Trainer 职业的怪物：女性 +0.09
            //   - 血脉（Bloodline）怪物专属效果：与其性交的女性 +0.01 × 血脉等级
            if ((character.State == EState.Sex || character.State == EState.SexDrained)
                && character.Partner is Male trainerPartner
                && GameData.TryGetProfession(trainerPartner, out IProfessionData? trainerProfession)
                && trainerProfession != null
                && trainerProfession.Id == ProfessionRegistry.TrainerId)
            {
                speed += 0.09f;
            }

            speed += BloodlineSystem.GetPartnerSexSpeedBonus(character);

            // ★ 统一上限（0.22）：Trainer / 血脉加成不再绕过上限
            return Mathf.Min(speed, MaxSexProgressSpeed);
        }

        public static void UpdateSexProgress(Character victim, float deltaTime)
        {
            if (victim == null) return;
            if (RoomCheckHelper.IsInKokiRoom(victim)) return;
            if (victim.State != EState.Sex && victim.State != EState.SexDrained)
                return;

            // 使用动态速度
            float speed = GetSexProgressSpeed(victim);
            float current = GetProgress(victim);
            current += speed * deltaTime;
            CheckAndHandleOrgasm(victim, ref current);
            _progress[victim.UnitId] = current;
        }

        // ===== 护盾相关方法（重构） =====
        /// <summary>
        /// 获取角色的护盾值。护盾由特定特质决定，返回最大值（若多个特质同时存在，取护盾值最高的）。
        /// 若没有任何相关特质，返回 0。
        public static int GetDepravedShield(Character character)
        {
            if (character == null) return 0;

            // 计算当前角色拥有的特质中，匹配 _shieldTraitValues 的最高护盾值
            int maxShield = 0;
            foreach (var kvp in _shieldTraitValues)
            {
                if (character.TraitContains((ETrait)kvp.Key))
                {
                    if (kvp.Value > maxShield)
                        maxShield = kvp.Value;
                }
            }

            // ★ 血脉（Bloodline）提供的护盾：女性每级获得 ShieldPerLevel 点（神圣/龙之 400、自然 200、魔力/元素 100）
            maxShield += BloodlineSystem.GetShieldBonus(character);

            // 如果没有护盾特质，直接返回 0
            if (maxShield == 0)
                return 0;

            // 从存档读取当前护盾值，若不存在则初始化为 maxShield
            string key = $"DepravedShield_{character.UnitId}";
            int currentShield = ModSaveData.GetInt(ModEntry.ModName, key, -1);
            if (currentShield == -1)
            {
                // 首次获取，初始化为该特质对应的最大护盾值
                currentShield = maxShield;
                ModSaveData.SetInt(ModEntry.ModName, key, currentShield);
            }
            else
            {
                // 确保存档中的护盾值不超过当前特质允许的最大值（防止特质变更后护盾值超限）
                if (currentShield > maxShield)
                {
                    currentShield = maxShield;
                    ModSaveData.SetInt(ModEntry.ModName, key, currentShield);
                }
            }

            return currentShield;
        }

        public static void ReduceDepravedShield(Character character, int amount)
        {
            if (character == null) return;
            int current = GetDepravedShield(character);
            if (current <= 0) return;

            int newValue = Mathf.Max(0, current - amount);
            string key = $"DepravedShield_{character.UnitId}";
            ModSaveData.SetInt(ModEntry.ModName, key, newValue);
            ModEntry.LogDebug($"{character.DisplayName} 护盾减少 {amount}，剩余 {newValue}");

            OnDepravedShieldChanged?.Invoke(character);
        }

        // ===== 高潮计数 & 特质升级 =====
        public static class SexPeriodicDamageHelper
        {
            public static int GetOrgasmCount(Character character)
            {
                if (character == null) return 0;
                string key = $"OrgasmCount_{character.UnitId}";
                return ModSaveData.GetInt(ModEntry.ModName, key, 0);
            }

            public static void IncrementOrgasmCount(Character character)
            {
                string key = $"OrgasmCount_{character.UnitId}";
                int count = ModSaveData.GetInt(ModEntry.ModName, key, 0);
                count++;
                ModSaveData.SetInt(ModEntry.ModName, key, count);

                // 触发事件，通知 UI 更新
                OnOrgasmCountChanged?.Invoke(character);
            }

            public static void CheckAndUpgradeTrait(Character character)
            {
                string key = $"OrgasmCount_{character.UnitId}";
                int count = ModSaveData.GetInt(ModEntry.ModName, key, 0);
                if (count % 20 == 0 && count > 0)
                {
                    // 改为调用游戏内置的特质升级方法
                    character.UpgradeTrait();
                    // ★ 使用本地化弹窗
                    PopupHelper.ShowPopup(character, Strings.Popup_DeepDepravity);
                    ModEntry.LogDebug($"{character.DisplayName} 特质升级成功！");
                }
            }
        }
    }
}
