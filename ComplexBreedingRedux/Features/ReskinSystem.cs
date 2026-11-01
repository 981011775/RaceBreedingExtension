#nullable enable
using ComplexBreedingRedux.Data;
using ComplexBreedingRedux.Data.Species;
using HarmonyLib;
using MBMScripts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ComplexBreedingRedux.Features;

/// <summary>
/// 换皮（Reskin）系统：让复用「独有 Figure」的物种，
/// 把 Spine 资产名中的原版家族 token 重定向为模组自制的换皮前缀。
///
/// 支持的家族（资产名 token → 对应 Figure）：
///   aure   → EFigure.Aure    （GolemRig / 元素裔 Genasi）
///   bella  → EFigure.Bella   （angel / 堕天使 fallen）
///   nero   → EFigure.Nero    （weretiger 虎人）
///   karen  → EFigure.Karen   （arcticwolf 雪狼人）
///   claire → EFigure.Claire  （moonelf 月精灵）
///   vivi   → EFigure.Vivi    （wererabbit 魔兔人）
///   anna   → EFigure.Anna    （devil 魔鬼）
///   sylvia → EFigure.Sylvia  （dairycow 奶牛）
///
/// 原理（对应游戏机制）：
/// 1. 游戏按 character.Figure.GetString() 查 SpineData，同 Figure 的角色共享同一份 SpineData；
/// 2. 该 SpineData.skeletonDataAssetName* 指向原版资产名（aure_stand / bella_card / bed_nero_1 …）；
/// 3. 我们无法改共享对象（会连累所有同 Figure 角色），所以克隆一份，把资产名里的家族 token
///    替换成物种声明的 RigPrefix（bella_stand → angel_stand、bed_nero_1 → bed_weretiger_1）；
/// 4. 新名字必须已登记进游戏 *.animation 清单（Name→文件夹路径），否则回退原名（保持原版外观，不崩溃）。
/// </summary>
internal static class ReskinSystem
{
    private static readonly MethodInfo _cloneMethod =
        AccessTools.Method(typeof(object), "MemberwiseClone")
        ?? throw new InvalidOperationException("MemberwiseClone not found");

    /// <summary>克隆缓存：(原 SpineData, 前缀) → 克隆。SpineData 加载后不可变，缓存安全；避免每帧反射克隆。</summary>
    internal static readonly Dictionary<(SpineData, string), SpineData> CloneCache = new();

    internal static void ClearCache() => CloneCache.Clear();

    /// <summary>角色所属物种若声明了换皮前缀（RigPrefix）则返回，否则返回 null（普通角色直接跳过）。</summary>
    public static string? GetRigPrefix(Character? c)
    {
        if (c == null) return null;
        // 婴儿、儿童阶段不启用换皮
        if (c.GrowthState == EGrowthState.Baby || c.GrowthState == EGrowthState.Child)
            return null;

        if (!GameData.TryGetSpecies(c, out ISpeciesData? species) || species == null)
            return null;
        string? prefix = species.RigPrefix;
        return string.IsNullOrEmpty(prefix) ? null : prefix;
    }

    /// <summary>EFigure → 资产名中的家族 token（仅这些 Figure 使用独特家族骨架）。</summary>
    private static readonly Dictionary<EFigure, string> FigureTokens = new()
    {
        [EFigure.Aure] = "aure",
        [EFigure.Bella] = "bella",
        [EFigure.Nero] = "nero",
        [EFigure.Karen] = "karen",
        [EFigure.Claire] = "claire",
        [EFigure.Vivi] = "vivi",
        [EFigure.Anna] = "anna",
        [EFigure.Sylvia] = "sylvia",
    };

    /// <summary>取 Figure 对应的资产家族 token；非独特 Figure 返回 null。</summary>
    public static string? GetBaseToken(EFigure figure)
        => FigureTokens.TryGetValue(figure, out string? token) ? token : null;

    /// <summary>
    /// 取角色的换皮信息：<b>RigPrefix 与其 Figure 对应的家族 token 必须同时有效</b>。
    /// 只有「本物种声明的前缀」+「本 Figure 的家族 token」成对出现时才做替换，
    /// 这是避免换皮串味（角色变成其它人物、部件缺失）的关键。
    /// </summary>
    public static bool TryGetRigInfo(Character? c, out string prefix, out string baseToken)
    {
        prefix = string.Empty;
        baseToken = string.Empty;
        if (c == null)
            return false;

        string? p = GetRigPrefix(c);
        if (string.IsNullOrEmpty(p))
            return false;

        string? token = GetBaseToken(c.Figure);
        if (string.IsNullOrEmpty(token))
            return false;

        prefix = p!;
        baseToken = token!;
        return true;
    }

    /// <summary>原始字段访问（惰性获取 + 容错；避免调用属性 getter 触发数据绑定副作用）。</summary>
    private static AccessTools.FieldRef<Unit, SpineData>? _spineDataRef;

    private static SpineData? GetRawSpineData(Character character)
    {
        try
        {
            _spineDataRef ??= AccessTools.FieldRefAccess<Unit, SpineData>("m_SpineData");
            return _spineDataRef(character);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 资产名是否确属该角色当前 SpineData 的 4 个资产名之一。
    /// 用于在「按名字兜底替换」时做确定性归属判定：只有名字属于这个角色才改写，
    /// 从而避免把别的角色（如原版同 Figure 的独特 NPC）的资产也改掉。
    /// </summary>
    public static bool IsAssetNameOfCharacter(Character? c, string? name)
    {
        if (c == null || string.IsNullOrEmpty(name))
            return false;

        SpineData? sd = GetRawSpineData(c);
        if (sd == null)
            return false;

        return NameEquals(sd.skeletonDataAssetName, name)
            || NameEquals(sd.skeletonDataAssetName1, name)
            || NameEquals(sd.skeletonDataAssetName2, name)
            || NameEquals(sd.skeletonDataAssetName3, name);
    }

    private static bool NameEquals(string? a, string? b)
        => !string.IsNullOrEmpty(a) && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 把 SpineData 中的原版家族资产名替换为换皮前缀资产名。
    /// <paramref name="baseToken"/> 为该角色 Figure 对应的家族 token —— 只有包含该 token 的资产名才会被替换，
    /// 因此不同家族（aure/bella/nero/…）之间绝不会互相串味。有替换返回克隆；无关的 SpineData 原样返回。
    /// </summary>
    public static SpineData Apply(SpineData sd, string prefix, string baseToken)
    {
        if (CloneCache.TryGetValue((sd, prefix), out SpineData? cached))
            return cached!;

        string? a0 = MapAssetName(sd.skeletonDataAssetName, prefix, baseToken);
        string? a1 = MapAssetName(sd.skeletonDataAssetName1, prefix, baseToken);
        string? a2 = MapAssetName(sd.skeletonDataAssetName2, prefix, baseToken);
        string? a3 = MapAssetName(sd.skeletonDataAssetName3, prefix, baseToken);

        // 无需克隆
        if (ReferenceEquals(a0, sd.skeletonDataAssetName) &&
            ReferenceEquals(a1, sd.skeletonDataAssetName1) &&
            ReferenceEquals(a2, sd.skeletonDataAssetName2) &&
            ReferenceEquals(a3, sd.skeletonDataAssetName3))
            return sd;

        // 浅克隆
        var clone = (SpineData)_cloneMethod.Invoke(sd, null);

        // ★★★ 关键修复：确保所有私有 List<string> 字段非空 ★★★
        // 这些字段可能被后续渲染代码访问，若为 null 会导致 NullReferenceException 卡死
        string[] listFieldNames = new[]
        {
            "MonsterShowPartList",
            "MonsterSkinColorPartList",
            "MonsterHairColorPartList",
            "MonsterEyeColorPartList",
            "CondomColorPartList"
        };

        foreach (string fieldName in listFieldNames)
        {
            // 查找字段（忽略大小写，适配可能的命名差异）
            // 本版的这些扩展列表是 public 字段且为 camelCase（monsterShowPartList …），
            // 旧查找只用 NonPublic + PascalCase，永远找不到（日志刷新「未找到字段」）。
            string camel = char.ToLowerInvariant(fieldName[0]) + fieldName.Substring(1);
            var field = AccessTools.Field(typeof(SpineData), fieldName)
                        ?? AccessTools.Field(typeof(SpineData), camel)
                        ?? typeof(SpineData).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                             .FirstOrDefault(f => string.Equals(f.Name, fieldName, StringComparison.OrdinalIgnoreCase)
                                                  && f.FieldType == typeof(List<string>));

            if (field == null)
            {
                Debug.LogWarning($"[CBR] 未找到字段 {fieldName}，跳过复制。");
                continue;
            }

            // 获取源列表
            var srcList = field.GetValue(sd) as List<string>;
            // 目标列表：复制源内容，或创建空列表
            var destList = srcList != null ? new List<string>(srcList) : new List<string>();
            // 设置到克隆对象
            field.SetValue(clone, destList);
        }

        // 替换资产名（通过反射或 Traverse）
        SetPrivateStringField(clone, "skeletonDataAssetName", a0);
        SetPrivateStringField(clone, "skeletonDataAssetName1", a1);
        SetPrivateStringField(clone, "skeletonDataAssetName2", a2);
        SetPrivateStringField(clone, "skeletonDataAssetName3", a3);

        // ★★★ 复制 MGA Extra（关键修复，确保列表非 null）★★★
        CopyMgaExtra(sd, clone);

        CloneCache[(sd, prefix)] = clone;
        Debug.Log($"[CBR] Reskin 生效: {sd.skeletonDataAssetName} → {clone.skeletonDataAssetName} (prefix={prefix})");
        return clone;
    }

    /// <summary>
    /// 复制 MGA (MoreGraphicAttachments) 附加到 SpineData 上的扩展数据（如果 MGA 已加载）。
    /// 确保克隆体的 slaveClothesColorPartList 永远非 null。
    /// </summary>
    private static void CopyMgaExtra(SpineData source, SpineData target)
    {
        try
        {
            // 动态检测 MGA 程序集
            Type extraType = Type.GetType("MoreGraphicAttachments.ExtensionSpineData.SpineDataExtra, MoreGraphicAttachments");
            Type extraSystemType = Type.GetType("MoreGraphicAttachments.ExtensionSpineData.SpineDataExtraSystem, MoreGraphicAttachments");
            if (extraType == null || extraSystemType == null)
                return;

            MethodInfo extraMethod = extraSystemType.GetMethod("Extra", new[] { typeof(SpineData) });
            if (extraMethod == null)
                return;

            // 获取源 Extra（可能为 null，但 MGA 应该为每个 SpineData 创建）
            object sourceExtra = extraMethod.Invoke(null, new object[] { source });
            if (sourceExtra == null)
                return;

            FieldInfo listField = extraType.GetField("slaveClothesColorPartList");
            if (listField == null)
                return;

            // 获取源列表，若为 null 则用空列表代替
            List<string> sourceList = (listField.GetValue(sourceExtra) as List<string>) ?? new List<string>();
            if (sourceList == null)
                sourceList = new List<string>();

            // 获取目标 Extra（会自动创建）
            object targetExtra = extraMethod.Invoke(null, new object[] { target });
            if (targetExtra == null)
                return;

            // 赋予新列表（确保非 null）
            listField.SetValue(targetExtra, new List<string>(sourceList));
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[CBR] 复制 MGA Extra 失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 设置 SpineData 的资产名字段（游戏里是 public 字段，旧代码只按 NonPublic 找，会漏；
    /// 这里 public/非 public 都找，并且找不到时给出明确警告——避免"只替换了一半"造成部件缺失）。
    /// </summary>
    private static void SetPrivateStringField(SpineData target, string fieldName, string? value)
    {
        var field = AccessTools.Field(typeof(SpineData), fieldName)
                 ?? typeof(SpineData).GetField(fieldName,
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (field != null && field.FieldType == typeof(string))
        {
            field.SetValue(target, value);
            return;
        }

        try
        {
            var trav = Traverse.Create(target).Field(fieldName);
            if (trav.FieldExists())
            {
                trav.SetValue(value);
                return;
            }
        }
        catch
        {
            // 落到下面统一警告
        }

        if (_fieldWarned.Add(fieldName))
            Debug.LogError($"[CBR] Reskin: SpineData 字段 '{fieldName}' 未找到，换皮可能只替换了一半（会出现部件缺失）！");
    }

    /// <summary>已警告过的字段名（避免刷屏）。</summary>
    private static readonly HashSet<string> _fieldWarned = new();

    /// <summary>原版独有 Figure 家族在资产名中的 token（aure_stand / bella_card / bed_nero_1 …）。</summary>
    public static readonly string[] BaseFigureTokens =
    {
        "aure", "bella", "nero", "karen", "claire", "vivi", "anna", "sylvia",
    };

    /// <summary>本模组已登记的换皮前缀（用于诊断日志过滤）。</summary>
    public static readonly string[] RegisteredPrefixes =
    {
        "GolemRig", "angel", "fallen", "arcticwolf", "moonelf", "dairycow", "weretiger", "wererabbit", "devil",
    };

    /// <summary>资产名是否属于某个原版独有 Figure 家族（aure / bella / nero / …）。</summary>
    public static bool ContainsBaseFigureToken(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return false;
        for (int i = 0; i < BaseFigureTokens.Length; i++)
        {
            if (name!.IndexOf(BaseFigureTokens[i], StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
    }

    /// <summary>是否与本模组换皮相关（原版家族 token 或已登记换皮前缀），仅用于诊断日志。</summary>
    public static bool IsReskinAssetName(string? name)
    {
        if (ContainsBaseFigureToken(name))
            return true;
        if (string.IsNullOrEmpty(name))
            return false;
        for (int i = 0; i < RegisteredPrefixes.Length; i++)
        {
            if (name!.IndexOf(RegisteredPrefixes[i], StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
    }

    /// <summary>
    /// 单个资产名映射：把<b>该角色 Figure 对应的家族 token</b> 替换为换皮前缀。
    /// 例：bella_stand → angel_stand；bed_nero_1 → bed_weretiger_1；goblin_anna_1 → goblin_devil_1。
    /// 名字不包含该 token（属于别的家族/别的角色）时**原样返回**，绝不替换；
    /// 并且仅当新名字已登记在 *.animation 清单时才替换，否则回退原名（保持原版外观，不崩溃）。
    /// </summary>
    public static string? MapAssetName(string? name, string prefix, string baseToken)
    {
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(prefix) || string.IsNullOrEmpty(baseToken))
            return name;

        // ★ 家族一致性：名字里必须含有本角色的家族 token，才允许替换（防串味）
        if (name!.IndexOf(baseToken, StringComparison.OrdinalIgnoreCase) < 0)
            return name;

        string mapped = ReplaceIgnoreCase(name!, baseToken, prefix);
        if (GameManager.Instance != null
            && GameManager.Instance.AnimationDataDictionary.ContainsKey(mapped))
            return mapped;

        // 未登记的动作：回退原名并只提示一次，避免刷屏
        if (_missingLogged.Add(mapped))
            Debug.LogWarning($"[CBR] Reskin: '{mapped}' 未在 .animation 清单中登记，回退使用 '{name}'");
        return name;
    }

    /// <summary>已提示过的"未登记"资产名（避免每帧刷日志）。</summary>
    private static readonly HashSet<string> _missingLogged = new();

    /// <summary>大小写不敏感替换（资产名大小写不完全统一，例如 GolemRig_stand）。</summary>
    private static string ReplaceIgnoreCase(string source, string oldValue, string newValue)
    {
        var sb = new System.Text.StringBuilder(source.Length);
        int start = 0;
        while (true)
        {
            int idx = source.IndexOf(oldValue, start, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                sb.Append(source, start, source.Length - start);
                break;
            }
            sb.Append(source, start, idx - start);
            sb.Append(newValue);
            start = idx + oldValue.Length;
        }
        return sb.ToString();
    }

    /// <summary>
    /// 资产名是否<b>仍需</b>改名：含本角色家族的 token，且映射后的名字与原名不同。
    /// 用于「渲染前最后一道防线」判断是否还需要把 SpineData 换成换皮克隆。
    /// </summary>
    public static bool NeedsRemap(string? name, string prefix, string baseToken)
    {
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(prefix) || string.IsNullOrEmpty(baseToken))
            return false;

        if (name!.IndexOf(baseToken, StringComparison.OrdinalIgnoreCase) < 0)
            return false;

        string mapped = ReplaceIgnoreCase(name!, baseToken, prefix);
        return !string.Equals(mapped, name, StringComparison.Ordinal);
    }

    /// <summary>取角色的「物种 + 换皮前缀 + Figure」描述，仅用于诊断日志。</summary>
    public static string DescribeCharacter(Character? c)
    {
        if (c == null)
            return "<null>";
        try
        {
            string prefix = GetRigPrefix(c) ?? "-";
            string speciesName = GameData.TryGetSpecies(c, out ISpeciesData? species) && species != null
                ? species.InternalName
                : "?";
            return $"{speciesName}({prefix}/{c.Figure})";
        }
        catch
        {
            return "?";
        }
    }

    /// <summary>SpineData 的关键信息（资产名 + 状态 + 动画），仅用于诊断日志。</summary>
    public static string DescribeSpineData(SpineData? sd)
    {
        if (sd == null)
            return "<null>";
        try
        {
            string s0 = JoinStates(sd.Figure0StateNameList);
            string s1 = JoinStates(sd.Figure1StateNameList);
            return $"name='{sd.skeletonDataAssetName}' state=[{s0}|{s1}] anim='{sd.AnimationName}'";
        }
        catch
        {
            return "name='" + sd.skeletonDataAssetName + "'";
        }
    }

    private static string JoinStates(List<string>? list)
    {
        if (list == null || list.Count == 0)
            return "-";
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < list.Count; i++)
        {
            if (string.IsNullOrEmpty(list[i]))
                continue;
            if (sb.Length > 0)
                sb.Append('/');
            sb.Append(list[i]);
        }
        return sb.Length == 0 ? "-" : sb.ToString();
    }
}