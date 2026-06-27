using HarmonyLib;
using MBMScripts;
using MBM.ModLoader;
using MBM.ModLoader.Settings;
using UnityEngine;
using TMPro;
using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace DepravedPenaltyMod
{
    // ========================== 日志控制 ==========================
    public static class ModLogger
    {
        public static bool EnableLogging = false;

        public static void Log(string message)
        {
            if (EnableLogging)
                Debug.Log(message);
        }

        public static void LogFormat(string format, params object[] args)
        {
            if (EnableLogging)
                Debug.LogFormat(format, args);
        }
    }

    // ========================== 模组入口 ==========================
    public class ModEntry
    {
        private static AudioClip _hitSound;
        private static bool _soundLoaded = false;
        private const string AUDIO_RESOURCE_KEY = "hit.wav";

        public static void Load()
        {
            // 注册日志开关（默认关闭）
            ModSettings.RegisterBool("DepravedPenaltyMod", "EnableLogging", false, "启用调试日志（会略微影响性能）");
            ModLogger.EnableLogging = ModSettings.GetBool("DepravedPenaltyMod", "EnableLogging");

            // 注册功能开关和惩罚值
            ModSettings.RegisterBool("DepravedPenaltyMod", "Enable", true, "启用堕落伴侣扣血惩罚");
            ModSettings.RegisterInt("DepravedPenaltyMod", "HealthPenalty", 50, "每次扣减的生命值 (1~200)");

            ModLogger.Log("[DepravedPenaltyMod] 已加载！");
            CoroutineHelper.StartCoroutine(LoadEmbeddedAudioCoroutine());
        }

        // ---------- 从嵌入资源加载音频（使用 WWW 兼容旧版 Unity） ----------
        private static IEnumerator LoadEmbeddedAudioCoroutine()
        {
            var asm = typeof(ModEntry).Assembly;
            string[] resourceNames = asm.GetManifestResourceNames();
            string wavResourceName = null;
            foreach (string name in resourceNames)
            {
                if (name.IndexOf(AUDIO_RESOURCE_KEY, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    wavResourceName = name;
                    break;
                }
            }

            if (string.IsNullOrEmpty(wavResourceName))
            {
                Debug.LogError("[DepravedPenaltyMod] 未在 DLL 中找到嵌入音频资源（应为 .wav 文件，生成操作为“嵌入的资源”）");
                yield break;
            }

            ModLogger.Log($"[DepravedPenaltyMod] 找到资源: {wavResourceName}");

            byte[] audioBytes;
            using (Stream stream = asm.GetManifestResourceStream(wavResourceName))
            {
                if (stream == null)
                {
                    Debug.LogError("[DepravedPenaltyMod] 无法读取资源流");
                    yield break;
                }
                audioBytes = new byte[stream.Length];
                stream.Read(audioBytes, 0, audioBytes.Length);
            }

            // 写入临时文件
            string tempPath = Path.Combine(Application.temporaryCachePath, "mod_hit_temp.wav");
            File.WriteAllBytes(tempPath, audioBytes);

            // 使用 WWW 加载
            string url = "file://" + tempPath;
            using (WWW www = new WWW(url))
            {
                yield return www;
                if (string.IsNullOrEmpty(www.error))
                {
                    _hitSound = www.GetAudioClip(false, false, AudioType.WAV);
                    _soundLoaded = true;
                    ModLogger.Log("[DepravedPenaltyMod] 音频加载成功！");
                }
                else
                {
                    Debug.LogError($"[DepravedPenaltyMod] 音频加载失败: {www.error}");
                }
            }

            // 清理临时文件
            try { File.Delete(tempPath); } catch { }
        }

        /// <summary>
        /// 播放 3D 音效，自动衰减，仅近处可闻
        /// </summary>
        public static void PlayHitSound(Vector3 position, float minDistance = 1.5f, float maxDistance = 2.0f)
        {
            if (!_soundLoaded || _hitSound == null)
                return;

            GameObject tempGO = new GameObject("ModHitSound");
            tempGO.transform.position = position;
            AudioSource source = tempGO.AddComponent<AudioSource>();
            source.clip = _hitSound;
            source.spatialBlend = 1.0f;               // 完全 3D
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            source.dopplerLevel = 0f;                 // 忽略多普勒效应
            source.volume = 1.0f;
            source.Play();

            // 音效播放完毕后自动销毁
            UnityEngine.Object.Destroy(tempGO, _hitSound.length);
        }
    }

    // ========================== 辅助：KokiRoom 检查 ==========================
    public static class RoomCheckHelper
    {
        public static bool IsInKokiRoom(Character character)
        {
            if (character == null) return false;
            Room room = character.Room;
            return room is KokiRoom;
        }
    }

    // ========================== 补丁1：CumShot 触发扣血 ==========================
    [HarmonyPatch(typeof(Character), "UpdateState")]
    public static class Patch_UpdateState_DepravedPenalty
    {
        static void Postfix(Character __instance)
        {
            if (!ModSettings.GetBool("DepravedPenaltyMod", "Enable"))
                return;

            Character partner = __instance.Partner;
            if (RoomCheckHelper.IsInKokiRoom(__instance) || RoomCheckHelper.IsInKokiRoom(partner))
                return;

            if (__instance.State != EState.CumShotInside && __instance.State != EState.CumShotOutside)
                return;

            if (partner == null)
                return;

            // ---- 非堕落逻辑（25% 几率 20 伤害） ----
            if (!partner.IsDepraved)
            {
                if (UnityEngine.Random.value < 0.25f)
                {
                    partner.Health -= 20;
                    SexPeriodicDamageHelper.IncrementOrgasmCount(partner);
                    SexPeriodicDamageHelper.CheckAndUpgradeTrait(partner);
                    PopupHelper.ShowPopup(__instance, "高潮");
                    ModLogger.Log($"[DepravedPenaltyMod] 非Depraved惩罚：对 {partner.DisplayName} 造成 15 伤害，剩余 HP {partner.Health}");
                }
                return;
            }

            // ---- 堕落逻辑（使用设置中的 HealthPenalty） ----
            int penalty = ModSettings.GetInt("DepravedPenaltyMod", "HealthPenalty");
            if (penalty < 1) penalty = 1;
            if (penalty > 200) penalty = 200;

            partner.Health -= penalty;
            SexPeriodicDamageHelper.IncrementOrgasmCount(partner);
            SexPeriodicDamageHelper.CheckAndUpgradeTrait(partner);
            PopupHelper.ShowPopup(__instance, "高潮");
            ModLogger.Log($"[DepravedPenaltyMod] 对堕落伴侣扣除 {penalty} HP，剩余 {partner.Health}");

            // 播放音效（在受害者位置）
            ModEntry.PlayHitSound(partner.Position);
        }
    }

    // ========================== 补丁2：Male.Sex 持续伤害 ==========================
    [HarmonyPatch(typeof(Male), "Sex")]
    public static class Patch_Sex_DepravedPeriodicDamage_Male
    {
        static void Postfix(Male __instance, float deltaTime)
        {
            SexPeriodicDamageHelper.ApplyPeriodicDamage(__instance, deltaTime);
        }
    }

    // ========================== 持续伤害核心逻辑（含阈值、样式同步） ==========================
    public static class SexPeriodicDamageHelper
    {
        private static readonly Dictionary<int, float> _lastStateTime = new Dictionary<int, float>();
        private const float CHECK_INTERVAL = 5f;          // 每5秒检查一次
        private const int PRIMARY_DAMAGE = 40;
        private const int SECONDARY_DAMAGE = 120;
        private const int SECOND_TRIGGER_MIN_COUNT = 200;
        private const float SECOND_CHANCE_MAX = 0.99f;
        private const int MIN_ORGASM_COUNT = 20;          // 持续伤害最低触发次数阈值

        public static void ApplyPeriodicDamage(Character __instance, float deltaTime)
        {
            if (!ModSettings.GetBool("DepravedPenaltyMod", "Enable"))
                return;

            Character partner = __instance.Partner;
            if (RoomCheckHelper.IsInKokiRoom(__instance) || RoomCheckHelper.IsInKokiRoom(partner))
                return;

            if (__instance.State != EState.Sex)
                return;

            if (partner == null || !partner.IsDepraved)
                return;

            int orgasmCount = GetOrgasmCount(partner);
            if (orgasmCount <= 0)
            {
                ModLogger.Log($"[Debug] OrgasmCount=0，跳过检查");
                return;
            }

            // ★ 未达到最低阈值则跳过
            if (orgasmCount < MIN_ORGASM_COUNT)
            {
                ModLogger.Log($"[Debug] OrgasmCount={orgasmCount} 低于阈值 {MIN_ORGASM_COUNT}，跳过");
                return;
            }

            // 概率 = min(计数 * 0.003, 0.9) —— 最低1%，最高90%
            float chance = Mathf.Clamp(orgasmCount * 0.003f, 0.01f, 0.9f);

            float currentStateTime = __instance.StateTime;

            // 检查间隔
            if (_lastStateTime.TryGetValue(partner.UnitId, out float lastTime))
            {
                float elapsed = currentStateTime - lastTime;
                if (elapsed < CHECK_INTERVAL)
                {
                    ModLogger.Log($"[Debug] 间隔未到，elapsed={elapsed:F2}s，跳过");
                    return;
                }
            }

            float randomValue = UnityEngine.Random.value;
            ModLogger.Log($"[Debug] OrgasmCount={orgasmCount}, chance={chance:P}, random={randomValue:F3}, stateTime={currentStateTime:F2}");

            if (randomValue > chance)
            {
                ModLogger.Log("[Debug] 随机未命中，不触发");
                return;
            }

            // ---- 触发主要伤害 ----
            ModLogger.Log("[Debug] ★ 触发伤害！");
            ApplyDamage(partner, __instance, PRIMARY_DAMAGE, "高潮");
            _lastStateTime[partner.UnitId] = currentStateTime;

            // ---- 二次触发判定 ----
            if (orgasmCount >= SECOND_TRIGGER_MIN_COUNT)
            {
                float secondChance = Mathf.Min(orgasmCount * 0.001f, SECOND_CHANCE_MAX);
                if (UnityEngine.Random.value < secondChance)
                {
                    CoroutineHelper.StartCoroutine(DelayedDamage(partner, __instance, 0.2f));
                    ModLogger.Log($"[Debug] 二次触发，概率={secondChance:P}");
                }
            }
        }

        private static IEnumerator DelayedDamage(Character victim, Character source, float delay)
        {
            yield return new WaitForSeconds(delay);
            ApplyDamage(victim, source, SECONDARY_DAMAGE, "连续高潮");
            ModLogger.Log("[Debug] 延迟第二次伤害执行");
        }

        private static void ApplyDamage(Character victim, Character source, int damage, string popupText)
        {
            if (RoomCheckHelper.IsInKokiRoom(victim) || RoomCheckHelper.IsInKokiRoom(source))
                return;

            victim.Health -= damage;
            ModLogger.Log($"[Debug] {victim.DisplayName} 受到 {damage} 伤害，剩余 HP {victim.Health}");

            IncrementOrgasmCount(victim);
            CheckAndUpgradeTrait(victim);
            PopupHelper.ShowPopup(source, popupText);
            ModEntry.PlayHitSound(victim.Position);
        }

        public static void IncrementOrgasmCount(Character character)
        {
            string key = $"OrgasmCount_{character.UnitId}";
            int count = ModSaveData.GetInt("DepravedPenaltyMod", key, 0);
            count++;
            ModSaveData.SetInt("DepravedPenaltyMod", key, count);
            ModLogger.Log($"[Debug] {character.DisplayName} 的 OrgasmCount 增加至 {count}");
        }

        public static void CheckAndUpgradeTrait(Character character)
        {
            string key = $"OrgasmCount_{character.UnitId}";
            int count = ModSaveData.GetInt("DepravedPenaltyMod", key, 0);
            if (count % 20 == 0 && count > 0)
            {
                character.UpgradeTrait();
                // 使用统一样式的弹窗（粉色小字）
                PopupHelper.ShowPopup(character, "深度堕落");
                ModLogger.Log($"[Debug] {character.DisplayName} 触发特质升级！");
            }
        }

        private static int GetOrgasmCount(Character character)
        {
            string key = $"OrgasmCount_{character.UnitId}";
            return ModSaveData.GetInt("DepravedPenaltyMod", key, 0);
        }
    }



    // ========================== 协程辅助类 ==========================
    public static class CoroutineHelper
    {
        private static MonoBehaviour _handler;
        private static void EnsureHandler()
        {
            if (_handler == null)
            {
                var go = new GameObject("CoroutineHelper");
                UnityEngine.Object.DontDestroyOnLoad(go);
                _handler = go.AddComponent<CoroutineHandler>();
            }
        }
        public static Coroutine StartCoroutine(IEnumerator routine)
        {
            EnsureHandler();
            return _handler.StartCoroutine(routine);
        }
        private class CoroutineHandler : MonoBehaviour { }
    }

    // ========================== 补丁4：名字显示 Orgasm 计数 ==========================
    [HarmonyPatch(typeof(ReferenceCharacter), "GetString")]
    public static class Patch_ReferenceCharacter_GetString
    {
        static void Postfix(ReferenceCharacter __instance, ref string __result)
        {
            try
            {
                var field = AccessTools.Field(typeof(ReferenceCharacter), "m_DataType");
                if (field == null) return;
                var dataTypeValue = field.GetValue(__instance);
                if (dataTypeValue == null) return;

                var enumType = AccessTools.TypeByName("MBMScripts.ReferenceCharacter+EDataType");
                if (enumType == null) return;

                object displayNameEnum = null;
                foreach (var enumVal in Enum.GetValues(enumType))
                {
                    if (Enum.GetName(enumType, enumVal) == "DisplayName")
                    {
                        displayNameEnum = enumVal;
                        break;
                    }
                }
                if (displayNameEnum == null) return;
                if (!dataTypeValue.Equals(displayNameEnum)) return;

                var updater = AccessTools.Property(__instance.GetType(), "Updater")?.GetValue(__instance);
                if (updater == null) return;
                var targetUnitProp = AccessTools.Property(updater.GetType(), "TargetUnit");
                if (targetUnitProp == null) return;
                var targetUnit = targetUnitProp.GetValue(updater) as TargetUnit;
                if (targetUnit == null) return;
                var character = targetUnit.Unit as Character;
                if (character == null) return;

                string key = $"OrgasmCount_{character.UnitId}";
                int count = ModSaveData.GetInt("DepravedPenaltyMod", key, 0);
                if (count > 0)
                {
                    __result += $" <size=60%><color=#D9006C>(高潮次数: {count})</color></size>";
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DepravedPenaltyMod] GetString patch error: {ex.Message}");
            }
        }
    }

    // ========================== 弹窗辅助类 ==========================
    public static class PopupHelper
    {
        public static void ShowPopup(Character source, string text)
        {
            if (source == null) return;

            source.PopUpMessage(text, null, 0.25f);

            // 修改 PopUpText 的样式（粉色，小号）
            var field = typeof(Unit).GetField("m_PopUpText", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                var popUpText = field.GetValue(source) as PopUpText;
                if (popUpText != null)
                {
                    var textProp = typeof(PopUpText).GetProperty("Text");
                    if (textProp != null)
                    {
                        var tmpText = textProp.GetValue(popUpText) as TMP_Text;
                        if (tmpText != null)
                        {
                            tmpText.text = text;
                            tmpText.color = Color.magenta;
                            tmpText.fontSize = 1.5f;
                            tmpText.SetAllDirty();
                        }
                    }
                }
            }
        }
    }
}