#nullable enable
using System.Collections.Generic;
using ComplexBreedingRedux.Data;
using ComplexBreedingRedux.Data.Species;
using ComplexBreedingRedux.Features;
using MBMScripts;
using MBM.ModLoader.Core;
using RaceBreedingExtension.UniqueLookColor.Extra;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace RaceBreedingExtension.UniqueLookColor
{
    /// <summary>
    /// 独特角色换色核心系统。
    ///
    /// 目标（对齐 MoreGraphicAttachments 的"衣服换色"架构的渲染部分）：
    ///   1. 换皮物种（RigPrefix = "GolemRig" 等）与可选的原版独特 Figure 角色在渲染时，
    ///      把角色身上的 HairColor / EyeColor 额外施加到「独特骨架的发/眼部件」。
    ///      （原版 Aure/GolemRig 的 SpineData 没有这些发/眼染色槽清单，直接改 HairColor 不会显色）
    ///   2. 由于各动作骨架（站立/卧姿/性爱/分娩…）槽位命名不同，按<b>槽位名前缀</b>匹配全部发/眼部件，
    ///      详见 <see cref="UniqueLookDefs.UniqueFigure"/>。
    ///   3. HairColor / EyeColor 使用游戏自带字段，天然随存档持久化，无需自建保存表。
    ///   4. 不注入任何 UI（画廊/信息窗口颜色槽已移除），颜色修改沿用游戏自带入口。
    ///
    /// 槽位规则说明见 <see cref="UniqueLookDefs"/>。
    /// </summary>
    internal static class UniqueLookColorSystem
    {
        /// <summary>已记录过"零匹配"的骨架名，避免日志刷屏。</summary>
        private static readonly HashSet<string> _zeroMatchLogged = new();

        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized)
                return;
            _initialized = true;

            try
            {
                UniqueLookColorConfig.Register();
                Extra.UniqueLookSaveData.Register();
                Localization.OnLanguageChanged += OnLanguageChanged;
                Log("Unique Look Color subsystem initialized.");
            }
            catch (System.Exception e)
            {
                LogError("Initialize failed: " + e);
            }
        }

        private static void OnLanguageChanged(string langCode)
        {
            try
            {
                UniqueLookColorConfig.OnLanguageChanged();
                Log($"language changed: {langCode}");
            }
            catch (System.Exception e)
            {
                LogError("OnLanguageChanged failed: " + e);
            }
        }

        // ==================== 判定 / 解析 ====================

        /// <summary>是否允许对该角色进行独特发/眼染色渲染。</summary>
        public static bool IsEligible(Character character)
        {
            if (character == null)
                return false;
            if (!UniqueLookColorConfig.Enable)
                return false;
            if (character.IsBaby || character.IsChild || character.IsDead)
                return false;
            if (character is not Female)
                return false;

            return TryGetLookData(character, out _, out _);
        }

        /// <summary>
        /// 解析角色对应的染色骨架定义。
        /// 优先级：换皮物种的 RigPrefix（GolemRig）→ （可选）原版独特 Figure（Aure）。
        /// </summary>
        public static bool TryGetLookData(Character character, out UniqueLookDef? def, out string? rigPrefix)
        {
            def = null;
            rigPrefix = null;
            if (character == null)
                return false;

            EFigure figure = character.Figure;

            try
            {
                // 1) 换皮物种（CBR ReskinSystem；内部已排除婴儿/儿童阶段）
                rigPrefix = ReskinSystem.GetRigPrefix(character);
            }
            catch (System.Exception e)
            {
                LogWarning($"ReskinSystem.GetRigPrefix failed: {e.Message}");
                rigPrefix = null;
            }

            if (!string.IsNullOrEmpty(rigPrefix))
            {
                def = UniqueLookDefs.ResolveDef(rigPrefix, figure);
                if (def != null)
                    return true;
            }

            // 2) 原版独特 Figure（仅在配置允许时）
            if (UniqueLookColorConfig.RecolorVanillaFigure)
            {
                def = UniqueLookDefs.ResolveDef(null, figure);
                if (def != null)
                    return true;
            }

            return false;
        }

        // ==================== 渲染 ====================

        /// <summary>读取角色的独特服色（扩展字段；默认白色表示不覆盖）。</summary>
        public static Color GetClothesColor(Character character)
            => character.Extra().ClothesColor;

        /// <summary>设置角色的独特服色（会触发骨架重建）。</summary>
        public static void SetClothesColor(Character character, Color color)
            => character.Extra().ClothesColor = color;

        /// <summary>
        /// 按物种声明的「独特服色初始值」（<see cref="ISpeciesData.ClothesColor"/>）设置角色服色。
        ///
        /// 调用时机（都是「物种被套用到角色」的时刻）：
        ///   · 角色创建：<c>FemaleSpeciesSetup.SetSpecies</c>（猎手营地俘虏 / 职业生成傀儡等）
        ///   · 后代出生：<c>BreedingSystem.ApplyFetus</c>（女胎）
        ///   · 成人外观刷新：<c>FemaleSpeciesSetup.RefreshSpeciesAppearance</c>
        ///   · 旧存档安全网：<c>SkinColorSystem.EnforcePalette</c>（每角色每次运行一次）
        ///
        /// 只在角色当前没有服色（白色 = 未设置）时写入，因此：
        ///   · 不会覆盖玩家在信息面板里自定义的服色；
        ///   · 不会覆盖存档中已保存的服色（读档在 <see cref="UniqueLookSaveData.ApplyAll"/> 中先行写入）。
        /// </summary>
        public static void ApplySpeciesClothesColor(Character character)
        {
            if (character == null)
                return;

            try
            {
                if (!GameData.TryGetSpecies(character, out ISpeciesData? species) || species == null)
                    return;
                Color? initial = species.ClothesColor;
                if (!initial.HasValue)
                    return;                     // 该物种未声明初始服色

                Extra.UniqueLookExtra extra = character.Extra();
                if (extra.ClothesColorSet)
                    return;                     // 已显式设置过（玩家自定义 / 显式取消 / 存档读取）→ 保持不变

                extra.ClothesColor = initial.Value;
                Log($"initial clothes color: {species.InternalName} '{character.DisplayName}' = {extra.ToColorHex()}");
            }
            catch (System.Exception e)
            {
                LogWarning("ApplySpeciesClothesColor failed: " + e.Message);
            }
        }

        /// <summary>
        /// 把角色当前 HairColor / EyeColor（以及可选服色）应用到独特骨架中的对应部件。
        /// 按槽位名前缀匹配（覆盖站立 / 卧姿 / 性爱 / 分娩等各动作骨架的不同命名）。
        /// 语义与原版一致：<c>slot.SetColor(color)</c> 直接乘染（不补偿）；
        /// 游戏对 Woman/Girl 等普通 Figure 的 SlaveHairColorPartList 也是直接乘染。
        /// </summary>
        public static void ApplyLookColors(Character character, Skeleton skeleton)
        {
            if (skeleton == null)
                return;
            if (!TryGetLookData(character, out UniqueLookDef? def, out string? rigPrefix) || def == null)
                return;

            Color hair = character.HairColor;
            Color eye = character.EyeColor;
            Color horn = character.HornColor;

            Extra.UniqueLookExtra extra = character.Extra();
            Color cloth = extra.ClothesColor;
            // 仅在「设置了服色」或「上一帧刚写过服色（需要还原）」时处理衣物部件，
            // 避免对从未设置服色的角色覆盖原版自身的衣物染色。
            bool applyCloth = extra.HasClothesColorOverride || extra.ClothesTintApplied;

            int hairCount = 0, eyeCount = 0, hornCount = 0, clothCount = 0;
            List<string>? hairNames = null, eyeNames = null, hornNames = null, clothNames = null;
            List<string>? suspicious = null;

            var slots = skeleton.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots.Items[i];
                string name = slot?.Data?.Name;
                if (string.IsNullOrEmpty(name))
                    continue;

                // 被 HidePartList 置为透明的部件（SetColor(clear)）保持隐藏，不要重新上色
                if (slot!.A <= 0.001f)
                    continue;

                if (HasPrefix(name, def.HairPrefixes))
                {
                    slot.SetColor(hair);
                    hairCount++;
                    if (UniqueLookColorConfig.EnableLogging)
                        (hairNames ??= new List<string>()).Add(name);
                }
                else if (HasPrefix(name, def.EyePrefixes))
                {
                    slot.SetColor(eye);
                    eyeCount++;
                    if (UniqueLookColorConfig.EnableLogging)
                        (eyeNames ??= new List<string>()).Add(name);
                }
                else if (HasPrefix(name, def.HornPrefixes))
                {
                    // 角颜色：同时作用于角、尾巴、兽耳（原版 SlaveHornColorPartList 语义）
                    slot.SetColor(horn);
                    hornCount++;
                    if (UniqueLookColorConfig.EnableLogging)
                        (hornNames ??= new List<string>()).Add(name);
                }
                else if (applyCloth && HasPrefix(name, def.ClothPrefixes))
                {
                    slot.SetColor(cloth);
                    clothCount++;
                    if (UniqueLookColorConfig.EnableLogging)
                        (clothNames ??= new List<string>()).Add(name);
                }
                else if (LooksLikeLookSlot(name))
                {
                    (suspicious ??= new List<string>()).Add(name);
                }
            }

            // 记录本帧是否写入了服色（用于取消服色时的白色还原）
            extra.ClothesTintApplied = extra.HasClothesColorOverride;

            if (hairCount + eyeCount + hornCount + clothCount > 0)
            {
                if (UniqueLookColorConfig.EnableLogging)
                {
                    Log($"{character.GetType().Name} '{character.DisplayName}' rig={rigPrefix} " +
                        $"hair={hairCount}[{string.Join(",", hairNames ?? new List<string>())}] " +
                        $"eye={eyeCount}[{string.Join(",", eyeNames ?? new List<string>())}] " +
                        $"horn={hornCount}[{string.Join(",", hornNames ?? new List<string>())}] " +
                        $"cloth={clothCount}[{string.Join(",", clothNames ?? new List<string>())}]");
                    if (suspicious != null && suspicious.Count > 0)
                        Log($"unmatched look-like slots: [{string.Join(",", suspicious)}]");
                }
            }
            else if (suspicious != null && suspicious.Count > 0)
            {
                // 该骨架看起来有发/眼/角部件但全部未命中：提示需要补充前缀（同一骨架只提示一次）
                string skeletonName = skeleton.Data?.Name ?? "(unknown)";
                if (_zeroMatchLogged.Add(skeletonName))
                    LogWarning($"No hair/eye/horn slot matched on skeleton '{skeletonName}' (rig={rigPrefix}); " +
                               $"candidates=[{string.Join(",", suspicious)}]. " +
                               "请在 UniqueLookDefs.UniqueFigure 的 HairPrefixes/EyePrefixes/HornPrefixes 中补充前缀。");
            }
        }

        /// <summary>OrdinalIgnoreCase 前缀匹配（槽位名固定小写，双保险）。</summary>
        private static bool HasPrefix(string name, string[] prefixes)
        {
            for (int i = 0; i < prefixes.Length; i++)
            {
                string prefix = prefixes[i];
                if (!string.IsNullOrEmpty(prefix) &&
                    name.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 槽位名是否"看起来像"发/眼/角部件（仅用于未命中诊断，不参与染色）。
        /// 排除已知的其它部件/控制骨：con_/ik_/ilk_ 控制骨、m_ 雄性部件、
        /// hairband 发饰、eyehair 睫毛/眉、chair 产椅。
        /// </summary>
        private static bool LooksLikeLookSlot(string name)
        {
            string lower = name.TrimStart().ToLowerInvariant();
            if (lower.StartsWith("con_") || lower.StartsWith("ik_") || lower.StartsWith("ilk_") || lower.StartsWith("m_"))
                return false;
            if (lower.Contains("hairband") || lower.Contains("eyehair") || lower.Contains("chair"))
                return false;

            return lower.Contains("hair") || lower.Contains("eyeball") || lower.Contains("eyeheart")
                || lower.Contains("horn") || lower.Contains("beastear") || lower.Contains("tail");
        }

        // ==================== 日志 ====================

        internal static void Log(string msg)
        {
            if (UniqueLookColorConfig.EnableLogging)
                Debug.Log($"[RBE][UniqueLook] {msg}");
        }

        internal static void LogWarning(string msg) => Debug.LogWarning($"[RBE][UniqueLook] {msg}");

        internal static void LogError(string msg) => Debug.LogError($"[RBE][UniqueLook] {msg}");
    }
}
