#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ComplexBreedingRedux.Data;
using ComplexBreedingRedux.Data.Species;
using ComplexBreedingRedux.Data.SpeciesCore;
using HarmonyLib;
using MBMScripts;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ComplexBreedingRedux.Features;

/// <summary>
/// 集中式肤色管理系统。
/// 调色板、旧版 alpha 迁移、强制执行 —— 全部集中在此。
/// </summary>
internal static class SkinColorSystem
{
    // --- 原始字段访问 ---

    private static readonly FieldInfo RawSkinColorField =
        AccessTools.Field(typeof(Character), "m_SkinColor");

    // --- 调色板强制执行状态 ---

    private static readonly HashSet<int> _enforced = new();

    private static readonly HashSet<string> ExcludedTypes = new()
    {
        // 独特 NPC
        "Anna", "Aure", "Bella", "Claire", "Karen", "Nero", "Sylvia", "Vivi",
        "Amilia2", "Barbara2", "Flora2", "Lena2", "Niel2", "Sena2",
        // 妓院客人
        "Client", "TestClient",
    };

    /// <summary>距离调色板基准颜色的最大偏差（±15/255 ≈ ±0.05）。</summary>
    private const float Deviation = 0.05f;

    /// <summary>检查是否属于调色板的容差。必须 ≤ Deviation 加少量余量，以免退化后的颜色被误判为其他物种。</summary>
    private const float Tolerance = 0.06f;

    // --- 旧版迁移数据 ---

    private static readonly Dictionary<int, string> LegacyAlphaMap = new()
    {
        { 999111, "Goblin" },
        { 999112, "Horse" },
        { 999113, "Ghoul" },
        { 999114, "Angel" },
        { 999115, "Orc" },
        { 999116, "Vampire" },
        { 999117, "Slime" },
        { 999118, "Hobgoblin" },
        { 999119, "Redcap" },
        { 999121, "Wererabbit" },
        { 999122, "Fairy" },
        { 999123, "Mermaid" },
        { 999124, "Minotaur" },
        { 999125, "Drow" },
        { 999126, "Salamander" },
        { 999127, "Dragon" },
        { 999128, "Werewolf" },
        { 999129, "Dracolich" },
        { 999130, "Drake" },
        { 999131, "Succubus" },
        { 999132, "Kitsune" },
        { 999133, "Golem" },
        { 999134, "Devil" },
        { 999135, "Nekomata" },
    };

    /// <summary>
    /// 旧版基础物种（SpeciesCore）的 alpha 标记。
    /// 在旧版本中，全部 8 个基础物种共用同一个 alpha = 0.998111f。
    /// 不需要 buff —— 基础物种根据 Race 确定。
    /// </summary>
    private const int LegacyCoreAlpha = 998_111;

    // --- 公共方法 ---

    /// <summary>
    /// 一次性检查并对角色强制应用物种调色板。
    /// 从 <see cref="Patches.AppearancePatches.SkinColorPatch"/> 调用。
    /// 对 Male 角色作为存档加载时的安全网（新 Male 由 MaleAppearancePatch / MaleInitPatches 覆盖）。
    /// </summary>
    public static void EnforcePalette(Character character)
    {
        // 如果是纯基础种族且没有进化身份，直接跳过强制矫正
        if (GameData.TryGetSpeciesCore(character, out var core) && core != null)
        {
            if (string.IsNullOrEmpty(GameData.GetSpeciesIdentity(character)))
                return;
        }

        // ... 原有的强制逻辑 ...
        if (!_enforced.Add(character.GetHashCode()))
            return;

        string typeName = character.GetType().Name;
        if (ExcludedTypes.Contains(typeName))
            return;

        Color[]? palette = ResolvePalette(character, typeName);
        if (palette != null && palette.Length > 0)
        {
            Color current = GetRawSkinColor(character);
            if (!IsInPalette(current, palette))
            {
                Color chosen = PickRandom(palette);
                SetRawSkinColor(character, chosen);
            }
        }

        // HairColors —— 为 Male 的头发槽位单独提供调色板的物种（Minotaur：毛发，Salamander：鳞片）。
        EnforceHairPalette(character, typeName);

        // ★ 物种外观安全网：纠正旧存档中「物种定义的 Figure 已变更」的角色
        //   （例如 Golem 由 EFigure.Aure 取消独特外观、改为 EFigure.Woman）。
        EnforceSpeciesFigure(character);

        // ★ 物种「独特服色初始值」安全网：旧存档中从未设置过服色的角色（如天使/堕天使）
        //   在首次访问 SkinColor 时补上物种声明的初始服色（不会覆盖玩家自定义的颜色）。
        RaceBreedingExtension.UniqueLookColor.UniqueLookColorSystem.ApplySpeciesClothesColor(character);
    }

    /// <summary>
    /// 物种外观安全网（旧存档兼容）：
    /// 若角色带有物种身份、是<b>成人女性</b>，且当前 <c>RawFigure</c> 与物种定义的
    /// <see cref="ISpeciesData.Figure"/> 不一致，则纠正为物种定义的 Figure（setter 会自动 UpdateSpineData）。
    ///
    /// 用途：物种定义中的 Figure 被改动后（如 Golem 取消独特角色外观：
    /// <c>EFigure.Aure</c> → <c>EFigure.Woman</c>），旧存档里的既有角色仍保存着旧 Figure，
    /// 若不纠正就会继续以旧骨架（原版 Aure）渲染，看起来「改动没生效」。
    ///
    /// 生效范围限制（避免副作用）：
    /// 1. 仅成人女性 —— 儿童由游戏自身以 <c>EFigure.Girl</c> 呈现；男性后代保留原版男性骨架
    ///    （<c>BreedingSystem</c> 只写物种身份，不写 Figure）。
    /// 2. 仅<b>未声明 RigPrefix 的物种</b> —— 换皮物种的 Figure 同时决定换皮家族 token，
    ///    且其既有路径由换皮补丁处理，这里不强行改写。
    /// 3. 与 <see cref="EnforcePalette"/> 一样，每个角色每次运行只执行一次
    ///    （懒执行于首次访问 SkinColor 时，见 Patches/AppearancePatches/SkinColorPatch.cs）。
    /// </summary>
    public static void EnforceSpeciesFigure(Character character)
    {
        if (character is not Female female || female.IsChild)
            return;

        if (!GameData.TryGetSpecies(character, out ISpeciesData? species) || species == null)
            return;
        if (!string.IsNullOrEmpty(species.RigPrefix))
            return;                       // 换皮物种：保持既有换皮路径，不在此纠正
        if (species.Figure == EFigure.None)
            return;                       // 物种未定义外观
        if (female.RawFigure == species.Figure)
            return;                       // 已经一致

        EFigure before = female.RawFigure;
        female.Figure = species.Figure;   // setter → RemovePartData + UpdateSpineData
        UnityEngine.Debug.Log(
            $"[CBR] 物种外观纠正(旧存档): {species.InternalName} '{female.DisplayName}' {before} → {species.Figure}");
    }

    /// <summary>
    /// 从调色板中随机选择一个基准颜色，并在 RGB 每个通道上添加 ±<see cref="Deviation"/> 的小幅偏差。
    /// </summary>
    public static Color PickRandom(Color[] palette)
    {
        Color baseColor = palette[Random.Range(0, palette.Length)];
        return new Color(
            Mathf.Clamp01(baseColor.r + Random.Range(-Deviation, Deviation)),
            Mathf.Clamp01(baseColor.g + Random.Range(-Deviation, Deviation)),
            Mathf.Clamp01(baseColor.b + Random.Range(-Deviation, Deviation)),
            1f);
    }

    /// <summary>检查颜色是否属于调色板（RGB 容差 ±<see cref="Tolerance"/>）。</summary>
    public static bool IsInPalette(Color color, Color[] palette)
    {
        foreach (Color c in palette)
        {
            if (Mathf.Abs(color.r - c.r) < Tolerance
             && Mathf.Abs(color.g - c.g) < Tolerance
             && Mathf.Abs(color.b - c.b) < Tolerance)
                return true;
        }
        return false;
    }

    /// <summary>
    /// 解析角色的 SkinColors 调色板。
    /// 1) 进化物种（Evolved species）通过 buff / 迁移（Female 且带有 SpeciesIdentity）。
    /// 2) 进化物种通过类型名称（Male 怪物：Goblin、Orc……）。
    /// 3) 基础物种（Core species）通过种族。
    /// </summary>
    public static Color[]? ResolvePalette(Character character)
        => ResolvePalette(character, character.GetType().Name);

    /// <summary>
    /// 解析角色的 HairColors 调色板（为 Male 的头发槽位单独提供调色板的物种）。
    /// </summary>
    public static Color[]? ResolveHairPalette(Character character)
        => ResolveHairPalette(character, character.GetType().Name);

    // --- 旧版 alpha 迁移 ---

    /// <summary>
    /// 尝试将角色从旧版 alpha 方案迁移到 buff。
    /// 从 <see cref="GameData.TryGetSpecies(Character, out ISpeciesData?)"/> 中作为后备调用（当未找到 buff 时）。
    /// 返回物种的 InternalName 或 null。
    /// </summary>
    public static string? TryMigrate(Character character)
    {
        Color raw = GetRawSkinColor(character);
        int alphaId = (int)Math.Round(raw.a * 1_000_000f);

        if (alphaId == 1_000_000)
            return null;

        // 旧版基础物种标记 —— 仅清除 alpha，不需要 buff。
        if (alphaId == LegacyCoreAlpha)
        {
            raw.a = 1f;
            RawSkinColorField.SetValue(character, raw);
            return null;
        }

        if (!LegacyAlphaMap.TryGetValue(alphaId, out string? name))
        {
            return null;
        }

        GameData.SetSpeciesIdentity(character, name);
        raw.a = 1f;
        RawSkinColorField.SetValue(character, raw);
        return name;
    }

    // --- 内部方法 ---

    private static Color[]? ResolvePalette(Character character, string typeName)
    {
        if (GameData.TryGetSpecies(character, out ISpeciesData? species) && species != null)
            return species.SkinColors;

        if (GameData.TryGetSpecies(typeName, out species) && species != null)
            return species.SkinColors;

        if (GameData.TryGetSpeciesCore(character, out ISpeciesCoreData? core) && core != null)
            return core.SkinColors;

        return null;
    }

    private static Color[]? ResolveHairPalette(Character character, string typeName)
    {
        if (GameData.TryGetSpecies(character, out ISpeciesData? species) && species?.HairColors != null)
            return species.HairColors;

        if (GameData.TryGetSpecies(typeName, out species) && species?.HairColors != null)
            return species.HairColors;

        return null;
    }

    /// <summary>Male 角色中，头发槽位按 HairColors 调色板着色的物种。</summary>
    internal static readonly HashSet<string> MaleHairPaletteTypes = new()
    {
        "Minotaur",   // 体毛
        "Salamander", // 鳞片状头发
        "Werewolf",   // 毛皮
    };

    private static void EnforceHairPalette(Character character, string typeName)
    {
        if (character is not Male || !MaleHairPaletteTypes.Contains(typeName))
            return;

        Color[]? hairPalette = ResolveHairPalette(character, typeName);
        if (hairPalette == null || hairPalette.Length == 0)
            return;

        if (IsInPalette(character.HairColor, hairPalette))
            return;

        Color chosenHair = PickRandom(hairPalette);
        character.HairColor = chosenHair;
    }

    private static Color GetRawSkinColor(Character character)
        => (Color)RawSkinColorField.GetValue(character);

    private static void SetRawSkinColor(Character character, Color color)
    {
        color.a = 1f;
        RawSkinColorField.SetValue(character, color);
    }
}