#nullable enable
using DarkTonic.MasterAudio;
using DepravedShield.Helpers;
using MBM.ModLoader.Core;
using MBMScripts;
using System;
using UnityEngine;

namespace RaceBreedingExtension.VoiceReplay
{
    /// <summary>
    /// 重制语音播放核心（VoiceReplay）。
    ///
    /// 1) 完全复刻原版所有女性角色语音播放列表：
    ///    - PlayDailyVoice   复刻 GameManager.PlayVoiceSound（日常语音）
    ///    - PlaySexVoice     复刻 GameManager.PlayVoiceSoundInSex（性爱语音）
    ///    - PlayTouchVoice   复刻 GameManager.PlayTouchSound（触摸语音）
    ///    - 包含不同种族（Loli/Human/Elf/Dwarf/Furry/SmallFurry/Dragonian）、
    ///      怪物种族（Goblin/Orc/Werewolf/Minotaur/Salamander/Horse）、
    ///      特殊角色（Amilia/Flora/Niel/SenaLena/Barbara）等全部音效。
    ///
    /// 2) 保留原版 VoiceType（A,B,C,D,E,F,G）切换语音功能：
    ///    语音名始终按 character.VoiceType 选择，原版 UI 按钮切换 VoiceType 后
    ///    会调用 PlayVoiceSound / PlayVoiceSoundInSex，本系统随即播放对应新音色。
    ///
    /// 3) 修改原版性爱语音逻辑：
    ///    在 EState.Sex / EState.SexDrained / EState.CumShotInside / EState.CumShotOutside 场景下，
    ///    改为根据 DepravedShield（堕落护盾）的性行为进度条播放语音：
    ///    - 进度 0-90%：按原版列表循环播放普通 Sex（X_sex_1）与 SexDrained（X_sex_2）语音
    ///                  （进度达到 SexDrainedThreshold 前为 Sex 音，之后为 SexDrained 音；堕落角色用 X_sex_3）；
    ///    - 进度 100% ：播放游戏列表 CumShotInside / CumShotOutside 相关（高潮/中出）语音
    ///                  （Human→woman_come、Loli→loli_come，其余种族→X_sex_3，特殊角色→X_sex_2）。
    /// </summary>
    internal static class VoiceReplaySystem
    {
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            try
            {
                // ★ 本地化：设置当前语言文化（必须在注册菜单描述之前）
                Properties.Strings.Culture = Localization.CurrentCulture;

                VoiceReplayConfig.Register();
                Localization.OnLanguageChanged += OnLanguageChanged;

                // 进度条到达 100%（高潮）时播放 CumShotInside / CumShotOutside 相关语音
                SexProgressManager.OnOrgasmCountChanged += OnOrgasmCountChanged;

                Log("Voice replay system initialized (progress-driven sex voice enabled).");
            }
            catch (Exception e)
            {
                LogError("Voice replay initialize failed: " + e);
            }
        }

        /// <summary>游戏语言切换时刷新本地化资源文化与模组菜单描述。</summary>
        private static void OnLanguageChanged(string langCode)
        {
            try
            {
                Properties.Strings.Culture = Localization.CurrentCulture;
                VoiceReplayConfig.OnLanguageChanged();
                Log("Language changed: " + langCode);
            }
            catch (Exception e)
            {
                LogError("Language change failed: " + e);
            }
        }

        // ==================== 日志 ====================

        internal static void Log(string msg)
        {
            if (VoiceReplayConfig.EnableLogging)
                Debug.Log($"[RaceBreedingExtension][VoiceReplay] {msg}");
        }

        internal static void LogError(string msg)
            => Debug.LogError($"[RaceBreedingExtension][VoiceReplay] {msg}");

        // ==================== 播放辅助 ====================

        /// <summary>在角色位置播放音频（与原版 PlayVoiceSoundInSex 相同的方式）。</summary>
        private static PlaySoundResult PlayAt(string soundName, Character character)
        {
            if (string.IsNullOrEmpty(soundName) || character == null) return null;
            return GameManager.Instance.PlayAudio(soundName, character.Position);
        }

        /// <summary>停止当前语音并播放新语音（用于高潮/中出语音，接管 PlaySoundResult 生命周期）。</summary>
        private static PlaySoundResult PlayReplacing(Character character, string soundName)
        {
            if (character == null || string.IsNullOrEmpty(soundName)) return null;

            PlaySoundResult current = character.PlaySoundResult;
            if (current?.ActingVariation != null)
                current.ActingVariation.Stop(false, false);

            PlaySoundResult result = GameManager.Instance.PlayAudio(soundName, character.Position);
            character.PlaySoundResult = result;
            if (result != null)
            {
                result.ActingVariation.SoundFinished += () => { character.PlaySoundResult = null; };
            }
            return result;
        }

        // ==================== 场景判断（EState.Sex/SexDrained/CumShotInside/CumShotOutside） ====================

        /// <summary>
        /// 判断是否处于需要“根据进度条播放语音”的性爱场景：
        /// 角色自身处于 EState.Sex / SexDrained / CumShotInside / CumShotOutside，
        /// 或性伴侣处于 EState.CumShotInside / CumShotOutside（女性在男性中出期间仍保持 Sex 状态）。
        /// </summary>
        private static bool IsSexContext(Character character)
        {
            if (character == null) return false;

            switch (character.State)
            {
                case EState.Sex:
                case EState.SexDrained:
                case EState.CumShotInside:
                case EState.CumShotOutside:
                    return true;
            }

            Character partner = character.Partner;
            if (partner == null) return false;

            switch (partner.State)
            {
                case EState.CumShotInside:
                case EState.CumShotOutside:
                    return true;
            }

            return false;
        }

        // ==================== 日常语音（复刻原版 GameManager.PlayVoiceSound） ====================

        public static PlaySoundResult PlayDailyVoice(Character character)
        {
            if (character == null || character.IsBaby || character.IsDead) return null;

            // ---- 怪物种族（原版顺序）----
            if (character is Goblin) return PlayAt(VoiceCatalog.MonsterDailyVoices[0].sound, character);
            if (character is Orc) return PlayAt(VoiceCatalog.MonsterDailyVoices[1].sound, character);
            if (character is Werewolf) return PlayAt(VoiceCatalog.MonsterDailyVoices[2].sound, character);
            if (character is Minotaur) return PlayAt(VoiceCatalog.MonsterDailyVoices[3].sound, character);
            if (character is Salamander) return PlayAt(VoiceCatalog.MonsterDailyVoices[4].sound, character);
            if (character is Horse) return PlayAt(VoiceCatalog.MonsterDailyVoices[5].sound, character);

            // ---- 特殊角色（原版顺序）----
            if (character is Amilia || character is Amilia2) return PlayAt(VoiceCatalog.SpecialNpcDailyVoices[0].sound, character);
            if (character is Flora || character is Flora2) return PlayAt(VoiceCatalog.SpecialNpcDailyVoices[1].sound, character);
            if (character is Niel || character is Niel2) return PlayAt(VoiceCatalog.SpecialNpcDailyVoices[2].sound, character);
            if (character is SenaLena || character is Sena2 || character is Lena2) return PlayAt(VoiceCatalog.SpecialNpcDailyVoices[3].sound, character);
            if (character is Barbara || character is Barbara2) return PlayAt(VoiceCatalog.SpecialNpcDailyVoices[4].sound, character);

            if (!(character is Female)) return null;

            // ---- 按 VoiceType(A-G) 复刻原版 switch（default → Loli）----
            switch (VoiceCatalog.ClampVoiceType(character.VoiceType))
            {
                case 0: return DailyLoli(character);
                case 1: return DailyHuman(character);
                case 2: return DailyElf(character);
                case 3: return DailyDwarf(character);
                case 4: return DailyFurry(character);
                case 5: return DailySmallFurry(character);
                case 6: return DailyDragonian(character);
                default: return DailyLoli(character);
            }
        }

        // ---- 原版公共状态判断 ----
        private static bool IsUntrained(Character c) => c.Training < GameManager.ConfigData.MaxTraining;
        private static bool IsHealthy(Character c) => c.Health > GameManager.ConfigData.PercentThatChangesToDrain;
        private static bool IsRestOrDrained(Character c) => c.State >= EState.Rest && c.State <= EState.Drained;
        private static bool IsMilk(Character c) => c.State >= EState.MilkStart && c.State <= EState.MilkIdle;

        private static PlaySoundResult DailyLoli(Character c)
        {
            if (IsUntrained(c))
            {
                if (c.IsDragging) return PlayAt(IsHealthy(c) ? "loli_sex (5)" : "loli_sex (7)", c);
                if (c.State == EState.Idle) return PlayAt("loli_sex (1)", c);
                if (IsRestOrDrained(c) || IsMilk(c)) return PlayAt("loli_sex (4)", c);
            }
            else
            {
                if (c.IsDragging) return PlayAt("loli_sex (8)", c);
                if (c.State == EState.Idle) return PlayAt("loli_sex (9)", c);
                if (IsMilk(c)) return PlayAt("loli_sex (10)", c);
            }
            return null;
        }

        private static PlaySoundResult DailyHuman(Character c)
        {
            if (IsUntrained(c))
            {
                if (c.IsDragging) return PlayAt(IsHealthy(c) ? "woman_sex (8)" : "woman_sex (6)", c);
                // 原版：非拖动时播放但不返回（返回 null）
                if (c.State != EState.Idle)
                {
                    if (IsRestOrDrained(c) || IsMilk(c)) PlayAt("woman_sex (4)", c);
                }
                else
                {
                    PlayAt("woman_sex (2)", c);
                }
                return null;
            }
            if (c.IsDragging) return PlayAt("woman_sex (11)", c);
            if (c.State == EState.Idle || IsMilk(c)) return PlayAt("woman_sex (13)", c);
            return null;
        }

        private static PlaySoundResult DailyElf(Character c)
        {
            if (IsUntrained(c))
            {
                if (c.IsDragging) return PlayAt(IsHealthy(c) ? "elf_rh_11" : "elf_s_3", c);
                if (c.State == EState.Idle) return PlayAt("elf_s_4", c);
                if (IsRestOrDrained(c) || IsMilk(c)) return PlayAt("elf_s_3", c);
            }
            else
            {
                if (c.IsDragging) return PlayAt("elf_s_7", c);
                if (c.State == EState.Idle) return PlayAt("elf_s_16", c);
                if (IsMilk(c)) return PlayAt("elf_wh_23", c);
            }
            return null;
        }

        private static PlaySoundResult DailyDwarf(Character c)
        {
            if (IsUntrained(c))
            {
                if (c.IsDragging) return PlayAt(IsHealthy(c) ? "dwarf_rh_11" : "dwarf_rh_10", c);
                if (c.State == EState.Idle) return PlayAt("dwarf_s_4", c);
                if (IsRestOrDrained(c) || IsMilk(c)) return PlayAt("dwarf_s_3", c);
            }
            else
            {
                if (c.IsDragging) return PlayAt("dwarf_s_11", c);
                if (c.State == EState.Idle) return PlayAt("dwarf_s_9", c);
                if (IsMilk(c)) return PlayAt("dwarf_wh_23", c);
            }
            return null;
        }

        private static PlaySoundResult DailyFurry(Character c)
        {
            if (IsUntrained(c))
            {
                if (c.IsDragging) return PlayAt(IsHealthy(c) ? "furry_rh_11" : "furry_wh_1", c);
                if (c.State == EState.Idle) return PlayAt("furry_s_4", c);
                if (IsRestOrDrained(c) || IsMilk(c)) return PlayAt("furry_s_3", c);
            }
            else
            {
                if (c.IsDragging) return PlayAt("furry_s_16", c);
                if (c.State == EState.Idle) return PlayAt("furry_s_6", c);
                if (IsMilk(c)) return PlayAt("furry_wh_22", c);
            }
            return null;
        }

        private static PlaySoundResult DailySmallFurry(Character c)
        {
            if (IsUntrained(c))
            {
                if (c.IsDragging) return PlayAt(IsHealthy(c) ? "smfurry_rh_11" : "smfurry_s_3", c);
                if (c.State == EState.Idle) return PlayAt("smfurry_s_4", c);
                if (IsRestOrDrained(c) || IsMilk(c)) return PlayAt("smfurry_s_3", c);
            }
            else
            {
                if (c.IsDragging) return PlayAt("smfurry_wh_22", c);
                if (c.State == EState.Idle) return PlayAt("smfurry_s_8", c);
                if (IsMilk(c)) return PlayAt("smfurry_wh_22", c);
            }
            return null;
        }

        private static PlaySoundResult DailyDragonian(Character c)
        {
            if (IsUntrained(c))
            {
                if (c.IsDragging) return PlayAt(IsHealthy(c) ? "dra_rh_11" : "smfurry_s_3", c);
                if (c.State == EState.Idle) return PlayAt("dra_s_4c", c);
                if (IsRestOrDrained(c) || IsMilk(c)) return PlayAt("dra_s_4b", c);
            }
            else
            {
                if (c.IsDragging) return PlayAt("dra_s_7", c);
                if (c.State == EState.Idle) return PlayAt("dra_s_6", c);
                if (IsMilk(c)) return PlayAt("dra_wh_11", c);
            }
            return null;
        }


        // ==================== 性爱语音（复刻原版 GameManager.PlayVoiceSoundInSex） ====================

        public static PlaySoundResult PlaySexVoice(Character character)
        {
            if (character == null || character.IsBaby || character.IsDead) return null;

            // 在 EState.Sex/SexDrained/CumShotInside/CumShotOutside 场景下，按堕落护盾进度条播放（常驻功能）
            if (IsSexContext(character))
                return PlaySexVoiceByProgress(character);

            // 其余情况完全复刻原版选择逻辑（含原版 VoiceType A-G 切换预览）
            return PlayVanillaSexVoice(character);
        }

        /// <summary>原版 PlayVoiceSoundInSex 逻辑：特殊角色 → Female 按 VoiceType 选择 X_sex_1/2/3。</summary>
        private static PlaySoundResult PlayVanillaSexVoice(Character character)
        {
            string? npc = GetSpecialNpcNormalSexName(character);
            if (npc != null) return PlayAt(npc, character);
            if (!(character is Female)) return null;

            int vt = VoiceCatalog.ClampVoiceType(character.VoiceType);
            if (character.IsDepraved) return PlayAt(VoiceCatalog.SexVoices[vt][2], character);

            bool drained = character.Health <= GameManager.ConfigData.PercentThatChangesToDrain;
            return PlayAt(VoiceCatalog.SexVoices[vt][drained ? 1 : 0], character);
        }

        /// <summary>
        /// 根据堕落护盾（DepravedShield）进度条选择性爱语音：
        ///   进度 0-90%  → 按原版列表循环播放普通 Sex（X_sex_1）与 SexDrained（X_sex_2）；
        ///                 进度达到 SexDrainedThreshold 前为 Sex 音，达到后为 SexDrained 音；
        ///                 堕落角色固定使用 X_sex_3（与原版一致）。
        ///   进度 100%   → 播放 CumShotInside / CumShotOutside 相关（高潮/中出）语音。
        /// </summary>
        private static PlaySoundResult PlaySexVoiceByProgress(Character character)
        {
            float progress = SexProgressManager.GetProgress(character);

            // 进度 100% → 高潮/中出语音
            if (progress >= 1f)
                return PlayCumshotVoice(character);

            // 特殊角色（原版语音 + 高潮变体 _2）
            string? npc = GetSpecialNpcNormalSexName(character, out string? npcIntense);
            if (npc != null)
            {
                bool intense = character.IsDepraved || progress >= VoiceReplayConfig.SexDrainedThreshold;
                return PlayAt(intense ? (npcIntense ?? npc) : npc, character);
            }

            if (!(character is Female)) return null;

            int vt = VoiceCatalog.ClampVoiceType(character.VoiceType);

            // 堕落角色 → 原版 X_sex_3
            if (character.IsDepraved)
                return PlayAt(VoiceCatalog.SexVoices[vt][2], character);

            // 进度 0-90%：普通 Sex / SexDrained 循环
            bool drainedPhase = progress >= VoiceReplayConfig.SexDrainedThreshold;
            return PlayAt(VoiceCatalog.SexVoices[vt][drainedPhase ? 1 : 0], character);
        }

        // ==================== 触摸语音（复刻原版 GameManager.PlayTouchSound） ====================

        public static PlaySoundResult PlayTouchVoice(Character character)
        {
            if (character == null || character.IsBaby || character.IsDead) return null;

            if (character is Amilia2) return PlayAt("amilia_sex", character);
            if (character is Flora2) return PlayAt("flora_sex", character);
            if (character is Niel2) return PlayAt("niel_sex", character);
            if (character is Sena2 || character is Lena2) return PlayAt("sena_sex", character);
            if (character is Barbara2) return PlayAt("barbara_sex", character);
            return null;
        }

        // ==================== 高潮/中出（CumShotInside / CumShotOutside）语音 ====================

        public static PlaySoundResult PlayCumshotVoice(Character character)
        {
            if (character == null || character.IsBaby || character.IsDead) return null;

            // 特殊角色 → X_sex_2（各特殊场景中的高潮/激烈变体）
            string? npc = GetSpecialNpcNormalSexName(character, out string? npcIntense);
            if (npc != null)
                return PlayReplacing(character, npcIntense ?? npc);

            if (!(character is Female)) return null;

            int vt = VoiceCatalog.ClampVoiceType(character.VoiceType);
            string[] list = VoiceCatalog.CumshotVoices[vt];
            if (list == null || list.Length == 0) return null;

            // 从该种族的高潮语音列表中随机选择（增加变化）
            string name = list[UnityEngine.Random.Range(0, list.Length)];
            return PlayReplacing(character, name);
        }

        /// <summary>进度条达到 100%（高潮，DepravedShield 触发 OnOrgasmCountChanged）时播放高潮/中出语音。</summary>
        private static void OnOrgasmCountChanged(Character character)
        {
            try
            {
                if (!VoiceReplayConfig.Enable) return;
                if (character == null || character.IsDead) return;
                if (!IsSexContext(character)) return;

                Log($"{character.DisplayName} 进度 100% → 播放 CumShotInside/CumShotOutside 相关（高潮/中出）语音");
                PlayCumshotVoice(character);
            }
            catch (Exception e)
            {
                LogError("OnOrgasmCountChanged failed: " + e);
            }
        }

        // ==================== 特殊角色语音名 ====================

        /// <summary>返回特殊角色（Amilia2/Flora2/Niel2/Sena2/Lena2/Barbara2）的原版性爱语音名；非特殊角色返回 null。</summary>
        private static string? GetSpecialNpcNormalSexName(Character character)
            => GetSpecialNpcNormalSexName(character, out _);

        private static string? GetSpecialNpcNormalSexName(Character character, out string? intense)
        {
            intense = null;
            if (character is Amilia2) { intense = VoiceCatalog.SpecialNpcSexVoices[0].intense; return VoiceCatalog.SpecialNpcSexVoices[0].normal; }
            if (character is Flora2) { intense = VoiceCatalog.SpecialNpcSexVoices[1].intense; return VoiceCatalog.SpecialNpcSexVoices[1].normal; }
            if (character is Niel2) { intense = VoiceCatalog.SpecialNpcSexVoices[2].intense; return VoiceCatalog.SpecialNpcSexVoices[2].normal; }
            if (character is Sena2 || character is Lena2) { intense = VoiceCatalog.SpecialNpcSexVoices[3].intense; return VoiceCatalog.SpecialNpcSexVoices[3].normal; }
            if (character is Barbara2) { intense = VoiceCatalog.SpecialNpcSexVoices[4].intense; return VoiceCatalog.SpecialNpcSexVoices[4].normal; }
            return null;
        }
    }
}


