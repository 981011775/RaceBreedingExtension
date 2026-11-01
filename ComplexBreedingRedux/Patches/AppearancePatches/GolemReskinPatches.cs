#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using ComplexBreedingRedux.Features;
using HarmonyLib;
using MBM.ModLoader.Core;
using MBMScripts;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace ComplexBreedingRedux.Patches.AppearancePatches;

/// <summary>
/// 独特 Figure（aure / bella / nero / karen / claire / vivi / anna / sylvia 家族）换皮补丁，
/// 覆盖所有声明了 RigPrefix 的物种（GolemRig 元素裔 Genasi、angel/fallen、weretiger、arcticwolf、moonelf、
/// wererabbit、devil、dairycow 等）。
///
/// ★ 设计要点：**按角色确定性改名**（不再凭全局上下文猜"正在给谁渲染"）
///  1) Unit / UpdaterSpineCharacter / UpdaterSpinePrefab 的 SpineData setter —— 主力路径。
///     三者在赋值时都能拿到**确切的角色**（Unit 自身 / Updater 的 Female|Male），
///     再配合 ReskinSystem.TryGetRigInfo() 得到「前缀 + 本 Figure 的家族 token」成对信息，
///     只有两者同时有效才改名；且只替换含该 token 的资产名 → 不同家族永不串味。
///  2) SeqAsset.GetSkeletonDataAsset(...) prefix —— 兜底改名，但必须通过
///     ReskinSystem.IsAssetNameOfCharacter()（该名字确实属于这个角色当前的 SpineData）才动手，
///     因此不会把同 Figure 的原版 NPC / 其它角色也改掉。
///
///  已移除：SpineData.GetSpineData(...) 的 Postfix 改名。该路径无法确定目标角色，
///  过去依赖 CurrentCharacter 会在存档重载/多角色同帧刷新时串味（角色变成别人、眼睛腿部缺失）；
///  而游戏中全部 27 处 GetSpineData 调用都紧接着赋值给上面已打补丁的 setter，无需此兜底。
///
/// 注意：必须「手动注册」，不能依赖 [HarmonyPatch] 自动应用——SpineData 属性声明在基类 Unit 上，
/// Mono 下 Harmony 用 typeof(Character) 解析继承属性会抛异常，导致整个 PatchAll 崩溃。
/// </summary>
internal static class GolemReskinPatches
{
    /// <summary>
    /// 最近一次被设置 SpineData 的角色。**仅**用于 SeqAsset 兜底路径的归属判定
    /// （配合 ReskinSystem.IsAssetNameOfCharacter 做确定性校验），不再单独决定是否改名。
    /// </summary>
    public static Character? CurrentCharacter;

    public static void Register(Harmony harmony)
    {
        TryPatch(harmony, AccessTools.PropertySetter(typeof(Unit), "SpineData"),
            "Unit.SpineData setter",
            postfix: new HarmonyMethod(typeof(GolemRigCharacterSpineDataPatch), nameof(GolemRigCharacterSpineDataPatch.Postfix)));

        TryPatch(harmony, AccessTools.PropertySetter(typeof(UpdaterSpineCharacter), "SpineData"),
            "UpdaterSpineCharacter.SpineData setter",
            postfix: new HarmonyMethod(typeof(GolemRigUpdaterSpineCharacterPatch), nameof(GolemRigUpdaterSpineCharacterPatch.Postfix)));

        TryPatch(harmony, AccessTools.PropertySetter(typeof(UpdaterSpinePrefab), "SpineData"),
            "UpdaterSpinePrefab.SpineData setter",
            postfix: new HarmonyMethod(typeof(GolemRigUpdaterSpinePrefabPatch), nameof(GolemRigUpdaterSpinePrefabPatch.Postfix)));

        // 兜底 3：所有骨架加载的最终汇合点
        TryPatch(harmony, AccessTools.Method(typeof(SeqAsset), "GetSkeletonDataAsset",
                new[] { typeof(string), typeof(GameObject) }),
            "SeqAsset.GetSkeletonDataAsset",
            prefix: new HarmonyMethod(typeof(SeqAssetGetSkeletonDataAssetPatch), nameof(SeqAssetGetSkeletonDataAssetPatch.Prefix)),
            postfix: new HarmonyMethod(typeof(SeqAssetGetSkeletonDataAssetPatch), nameof(SeqAssetGetSkeletonDataAssetPatch.DiagnosticPostfix)));

        // 兜底 4：渲染前最后一道防线。
        // 世界角色的骨架是在 UpdaterSpineCharacter.Display() 里定下来的：
        //   ① Display() 先把 Female/Male/SpineData 清空；② 逐条执行 Reference 重新赋值；
        //   ③ 末尾 m_SkeletonDataAsset = SpineData.SkeletonDataAsset（异步加载）。
        // 若某条路径在赋值 SpineData 时拿不到角色（Female/Male 为空），名字就不会被换，
        // 移动(move)等动作会回退到原版家族贴图。此 Postfix 在 Display() 结束后按
        // **该渲染器自己的角色**（Female/Male/Unit）再校验一次，必要时就地换成换皮克隆，
        // 并同步已缓存的 m_SkeletonDataAsset，保证 OnChangeSpine 用新骨架。
        // 归因只用该实例自己的角色，不依赖全局 CurrentCharacter，因此不会串味。
        TryPatch(harmony, AccessTools.Method(typeof(UpdaterSpineCharacter), "Display"),
            "UpdaterSpineCharacter.Display",
            postfix: new HarmonyMethod(typeof(UpdaterSpineCharacterDisplayPatch), nameof(UpdaterSpineCharacterDisplayPatch.Postfix)));

        // 诊断：SeqStreamingAssets 的松散文件骨架加载（确认加载路径与结果）
        TryPatch(harmony, AccessTools.Method(typeof(SeqStreamingAssets), "GetSkeletonDataAssetInStreamingAssets",
                new[] { typeof(string), typeof(bool), typeof(GameObject) }),
            "SeqStreamingAssets.GetSkeletonDataAssetInStreamingAssets",
            postfix: new HarmonyMethod(typeof(SeqStreamingAssetsGetSkeletonDataAssetPatch), nameof(SeqStreamingAssetsGetSkeletonDataAssetPatch.Postfix)));

        // 【已移除】兜底 2：SpineData.GetSpineData 的改名 Postfix。
        // 原因：该路径无法确定「正在为哪个角色渲染」，过去依赖全局 CurrentCharacter，
        // 存档重载/多角色同帧刷新时会串味（角色变成其它人物、眼睛腿部缺失）。
        // 实测游戏中全部 27 处 GetSpineData 调用都紧接着赋值给下面已打补丁的三个
        // SpineData setter（Unit / UpdaterSpineCharacter / UpdaterSpinePrefab），
        // 因此由 setter 按角色确定性改名即可，无需此处兜底。

        // 兼容修复：MGA(MoreGraphicAttachments) 的 OnChangeSpine Postfix 在骨架异步初始化
        // （m_Skeleton 为 null）时会抛 NullReference。给它的 Postfix 加 prefix：骨架未就绪时跳过。
        // 注意：CBR 的 Load() 在 MGA 之前执行，此时 MGA 程序集尚未加载，TypeByName 会失败，
        // 所以还要在 Loader.OnAllModsLoaded（所有 mod 加载完成）时再注册一次。
        try
        {
            RegisterMgaGuard(harmony);
            Loader.OnAllModsLoaded += () => RegisterMgaGuard(harmony);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[CBR] Reskin: MGA guard 注册异常: " + e.Message);
        }

        Debug.Log("[CBR] Reskin patches applied (setters + GetSpineData + SeqAsset.GetSkeletonDataAsset)");
    }

    /// <summary>注册单个补丁；目标缺失或 patch 抛异常时只记日志，绝不让加载流程崩溃。</summary>
    private static void TryPatch(Harmony harmony, MethodBase? target, string label,
                                 HarmonyMethod? prefix = null, HarmonyMethod? postfix = null,
                                 HarmonyMethod? transpiler = null)
    {
        try
        {
            if (target == null)
            {
                Debug.LogError("[CBR] Reskin: 目标方法未找到 - " + label);
                return;
            }
            harmony.Patch(target, prefix: prefix, postfix: postfix, transpiler: transpiler);
            Debug.Log("[CBR] Reskin patch OK: " + label);
        }
        catch (Exception e)
        {
            Debug.LogError("[CBR] Reskin patch FAILED - " + label + ": " + e);
        }
    }

    // 1) 角色自身：所有 base.SpineData = SpineData.GetSpineData(...) 都经过 Unit.set_SpineData
    internal static class GolemRigCharacterSpineDataPatch
    {
        public static void Postfix(Unit __instance, ref SpineData value)
        {
            try
            {
                var character = __instance as Character;
                CurrentCharacter = character;
                if (value == null) return;
                // 前缀与家族 token 必须成对有效，否则完全不动（防串味）
                if (!ReskinSystem.TryGetRigInfo(character, out string prefix, out string baseToken)) return;
                LogAssignment("Unit", character, value, prefix, baseToken);
                value = ReskinSystem.Apply(value, prefix, baseToken);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[CBR] Reskin setter(Unit) 异常: " + e.Message);
            }
        }
    }

    // 2) UI/场景渲染器（SkeletonGraphic 系）
    internal static class GolemRigUpdaterSpineCharacterPatch
    {
        public static void Postfix(UpdaterSpineCharacter __instance, ref SpineData value)
        {
            try
            {
                Character? female = (Character?)__instance.Female;
                Character? male = (Character?)__instance.Male;
                Character? unit = (Character?)__instance.Unit;
                CurrentCharacter = female ?? male ?? unit;
                if (value == null) return;
                if (!TryGetRigInfoForPair(female, male, unit, out string prefix, out string baseToken)) return;
                LogAssignment("UpdaterSpineCharacter", female ?? male ?? unit, value, prefix, baseToken);
                value = ReskinSystem.Apply(value, prefix, baseToken);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[CBR] Reskin setter(UpdaterSpineCharacter) 异常: " + e.Message);
            }
        }
    }

    // 3) 场景渲染器（SpineCharacter Prefab 系）
    internal static class GolemRigUpdaterSpinePrefabPatch
    {
        public static void Postfix(UpdaterSpinePrefab __instance, ref SpineData value)
        {
            try
            {
                Character? female = (Character?)__instance.Female;
                Character? male = (Character?)__instance.Male;
                Character? unit = (Character?)__instance.Unit;
                CurrentCharacter = female ?? male ?? unit;
                if (value == null) return;
                if (!TryGetRigInfoForPair(female, male, unit, out string prefix, out string baseToken)) return;
                LogAssignment("UpdaterSpinePrefab", female ?? male ?? unit, value, prefix, baseToken);
                value = ReskinSystem.Apply(value, prefix, baseToken);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[CBR] Reskin setter(UpdaterSpinePrefab) 异常: " + e.Message);
            }
        }
    }

    /// <summary>
    /// 兜底 4：世界渲染器 Display() 结束后的最后校验（见 Register 中说明）。
    /// 只有当 SpineData 仍是「原版家族资产名」且**该渲染器自己能确定角色**时才改写，
    /// 因此不会把别的角色（同 Figure 的原版 NPC）改掉。
    /// </summary>
    internal static class UpdaterSpineCharacterDisplayPatch
    {
        /// <summary>m_SkeletonDataAsset 字段（懒加载 + 容错：字段缺失时只跳过同步，不影响渲染）。</summary>
        private static FieldInfo? _assetField;

        public static void Postfix(Updater __instance)
        {
            try
            {
                // 参数用基类 Updater 接收：即使 AccessTools 解析到基类 Display()（影响所有 Updater），
                // 这里也会安全跳过，绝不抛 InvalidCastException。
                var updater = __instance as UpdaterSpineCharacter;
                if (updater == null)
                    return;

                SpineData? sd = updater.SpineData;
                if (sd == null) return;

                Character? c = (Character?)updater.Female ?? (Character?)updater.Male ?? (Character?)updater.Unit;
                if (c == null) return;
                if (!ReskinSystem.TryGetRigInfo(c, out string prefix, out string baseToken)) return;
                if (!ReskinSystem.NeedsRemap(sd.skeletonDataAssetName, prefix, baseToken)) return;

                SpineData mapped = ReskinSystem.Apply(sd, prefix, baseToken);
                if (ReferenceEquals(mapped, sd)) return;

                // 属性赋值 → 触发我们的 setter 补丁（幂等）
                updater.SpineData = mapped;
                // Display() 末尾已把旧骨架缓存进 m_SkeletonDataAsset，这里同步为新骨架，
                // 否则紧随其后的 OnChangeSpine() 仍会用原版骨架。
                _assetField ??= AccessTools.Field(typeof(UpdaterSpineCharacter), "m_SkeletonDataAsset");
                _assetField?.SetValue(updater, mapped.SkeletonDataAsset);

                Debug.Log($"[CBR] Reskin(Display 兜底): {sd.skeletonDataAssetName} → {mapped.skeletonDataAssetName}"
                          + $" (prefix={prefix}, char={ReskinSystem.DescribeCharacter(c)})");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[CBR] Reskin Display 兜底异常: " + e.Message);
            }
        }
    }

    /// <summary>诊断日志开关（跟随 UniqueLookColor 的调试开关，默认关闭以避免刷屏）。</summary>
    private static bool DiagEnabled => RaceBreedingExtension.UniqueLookColor.UniqueLookColorConfig.EnableLogging;

    /// <summary>在骨架资产真正加载前改写名字（最终防线，覆盖所有路径）。</summary>
    internal static class SeqAssetGetSkeletonDataAssetPatch
    {
        private static readonly HashSet<string> _loadInfo = new();

        public static void Prefix(ref string name)
        {
            try
            {
                // 诊断：确认清单(AnimationDataDictionary)是否已加载（仅调试开关开启时）
                if (DiagEnabled && name != null && _loadInfo.Add(name))
                {
                    var gm = GameManager.Instance;
                    int count = gm != null ? gm.AnimationDataDictionary.Count : -1;
                    bool has = gm != null && gm.AnimationDataDictionary.ContainsKey(name);
                    bool hasStreaming = SeqStreamingAssets.Instance != null;
                    Debug.Log($"[CBR-DIAG] load '{name}' | animDict.Count={count} hasName={has} streamingInst={hasStreaming}");
                }

                if (name == null) return;
                Character? c = CurrentCharacter;
                if (c == null) return;
                if (!ReskinSystem.TryGetRigInfo(c, out string prefix, out string baseToken)) return;
                // ★ 确定性归属判定：只有当该名字属于这个角色当前的 SpineData 时才改写。
                // 否则（例如同 Figure 的原版独特 NPC）会串味：角色变成别人、眼睛/腿部缺失。
                if (!ReskinSystem.IsAssetNameOfCharacter(c, name))
                {
                    LogBlockedFallback(name, prefix, baseToken, c);
                    return;
                }
                string? mapped = ReskinSystem.MapAssetName(name, prefix, baseToken);
                if (mapped != null && !ReferenceEquals(mapped, name))
                    name = mapped;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[CBR] Reskin SeqAsset.Prefix 异常: " + e.Message);
            }
        }

        // 临时诊断：确认 Golem 相关骨架实际加载的目标（每个名字只打一次，避免刷屏）
        private static readonly HashSet<string> _diagLogged = new();

        public static void DiagnosticPostfix(string name, SkeletonDataAsset __result)
        {
            try
            {
                if (!DiagEnabled)
                    return;
                if (!ReskinSystem.IsReskinAssetName(name))
                    return;
                if (!_diagLogged.Add(name))
                    return;
                string tex = "?";
                if (__result != null && __result.atlasAssets != null && __result.atlasAssets.Length > 0)
                {
                    Material mat = __result.atlasAssets[0].PrimaryMaterial;
                    if (mat != null && mat.mainTexture != null)
                        tex = mat.mainTexture.name;
                }
                Debug.Log($"[CBR-DIAG] GetSkeletonDataAsset('{name}') -> asset='{(__result != null ? __result.name : "NULL")}' mainTex='{tex}'");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[CBR] Reskin SeqAsset.Diag 异常: " + e.Message);
            }
        }
    }

    /// <summary>诊断：SeqStreamingAssets 的松散文件骨架加载（确认加载路径与结果）。</summary>
    internal static class SeqStreamingAssetsGetSkeletonDataAssetPatch
    {
        private static readonly HashSet<string> _seen = new();

        public static void Postfix(string path, SkeletonDataAsset __result)
        {
            try
            {
                if (!DiagEnabled) return;
                if (path == null) return;
                if (!ReskinSystem.IsReskinAssetName(path)) return;
                if (!_seen.Add(path)) return;
                Debug.Log($"[CBR-DIAG] GetSkelInStreaming('{path}') -> {(__result != null ? __result.name : "NULL")}");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[CBR] Reskin streaming 诊断异常: " + e.Message);
            }
        }
    }

    // 【已移除】SpineData.GetSpineData 的 Postfix 改名（见上方注册处的说明）：
    // 该路径只能凭全局 CurrentCharacter 猜"当前在给谁渲染"，存档重载时会串味，
    // 且游戏中 27 处调用全部走已打补丁的 SpineData setter，故无需保留。

    /// <summary>
    /// 双人场景中 Female/Male 都可能是物种携带者；渲染器实例上的 Unit 也可能是同一个角色
    /// （Reference 执行时先赋 Unit 再赋 Female/Male）。三者必须成对有效，
    /// 否则不做任何替换（防串味）。
    /// </summary>
    private static bool TryGetRigInfoForPair(Character? female, Character? male, Character? unit,
                                             out string prefix, out string baseToken)
    {
        if (ReskinSystem.TryGetRigInfo(female, out prefix, out baseToken))
            return true;
        if (ReskinSystem.TryGetRigInfo(male, out prefix, out baseToken))
            return true;
        return ReskinSystem.TryGetRigInfo(unit, out prefix, out baseToken);
    }

    /// <summary>已完整记录过的 (来源, 原名)，避免每帧刷屏。</summary>
    private static readonly HashSet<string> _assignLogged = new();

    /// <summary>
    /// 诊断：记录「哪个渲染器、哪个角色、什么状态、原版名 → 换皮名」。
    /// 仅在模组菜单打开 UniqueLook_EnableLogging 时输出（每个组合只记一次）。
    /// </summary>
    private static void LogAssignment(string source, Character? c, SpineData sd, string prefix, string baseToken)
    {
        try
        {
            if (!DiagEnabled)
                return;
            string key = source + "|" + ReskinSystem.DescribeSpineData(sd);
            if (!_assignLogged.Add(key))
                return;
            string mapped = ReskinSystem.MapAssetName(sd.skeletonDataAssetName, prefix, baseToken) ?? sd.skeletonDataAssetName;
            Debug.Log($"[CBR-DIAG] {source} 赋值 {ReskinSystem.DescribeSpineData(sd)}"
                      + $" | char={ReskinSystem.DescribeCharacter(c)} prefix={prefix}/{baseToken}"
                      + $" | → '{mapped}'");
        }
        catch
        {
            // 诊断失败绝不影响渲染
        }
    }

    /// <summary>已被归属判定拦下的 (名字) 集合，避免刷屏。</summary>
    private static readonly HashSet<string> _blockedLogged = new();

    /// <summary>
    /// 诊断：兜底改名被「归属判定」拦下，但这次拦下<b>看起来</b>是本角色需要的改名时记一笔。
    /// 用于定位「move 等动作回退原版贴图」这类问题（如果日志里出现这条，说明该动作的
    /// SpineData 赋值路径没有角色上下文）。
    /// </summary>
    private static void LogBlockedFallback(string name, string prefix, string baseToken, Character? c)
    {
        try
        {
            if (!DiagEnabled)
                return;
            if (!ReskinSystem.NeedsRemap(name, prefix, baseToken))
                return;
            if (!_blockedLogged.Add(name))
                return;
            Debug.Log($"[CBR-DIAG] 兜底改名被拦: '{name}' 不属于 {ReskinSystem.DescribeCharacter(c)} 当前 SpineData"
                      + $"（该角色 prefix={prefix}，若这是它正在渲染的动作，请把此行反馈）");
        }
        catch
        {
        }
    }

    private static bool _mgaGuardRegistered;

    /// <summary>
    /// 给 MGA 的 UpdaterSpineCharacterPatch.Postfix 加 Prefix：骨架未就绪（m_Skeleton 为 null）时
    /// 跳过它的槽位着色，避免 NullReferenceException。可多次调用（幂等）。
    /// </summary>
    private static void RegisterMgaGuard(Harmony harmony)
    {
        try
        {
            if (_mgaGuardRegistered)
                return;

            var mgaType = AccessTools.TypeByName("MoreGraphicAttachments.Patches.ComponentPatches.UpdaterSpineCharacterPatch");
            if (mgaType == null)
                return; // MGA 未加载（CBR 在 MGA 之前 Load），等 OnAllModsLoaded 再试

            var mgaPostfix = AccessTools.Method(mgaType, "Postfix");
            if (mgaPostfix == null)
            {
                Debug.LogWarning("[CBR] Reskin: MGA Postfix 未找到，跳过兼容修复");
                return;
            }

            harmony.Patch(mgaPostfix,
                prefix: new HarmonyMethod(typeof(MgaGuardPatch), nameof(MgaGuardPatch.Prefix)));
            _mgaGuardRegistered = true;
            Debug.Log("[CBR] MGA compatibility guard applied (m_Skeleton null safe)");
        }
        catch (Exception e)
        {
            Debug.LogWarning("[CBR] Reskin: MGA guard 注册异常: " + e.Message);
        }
    }

    /// <summary>
    /// 兼容修复：MGA(MoreGraphicAttachments) 的 UpdaterSpineCharacterPatch.Postfix 在
    /// UpdaterSpineCharacter.OnChangeSpine 后无条件执行 ___m_Skeleton.FindSlot(...)。
    /// 当骨架尚未就绪（m_Skeleton 为 null，常见于松散文件资产首次异步加载/场景切换）时会抛
    /// NullReferenceException。本 Prefix 在 m_Skeleton 为 null 时返回 false，跳过 MGA 的着色，
    /// 下一帧骨架就绪后自动恢复正常。
    /// </summary>
    internal static class MgaGuardPatch
    {
        private static readonly AccessTools.FieldRef<UpdaterSpineCharacter, Skeleton> SkeletonRef =
            AccessTools.FieldRefAccess<UpdaterSpineCharacter, Skeleton>("m_Skeleton");

        public static bool Prefix(UpdaterSpineCharacter __instance)
        {
            if (__instance == null)
                return true;
            return SkeletonRef(__instance) != null;
        }
    }
}

