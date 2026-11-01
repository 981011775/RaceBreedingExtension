#nullable enable
using System;
using System.Collections.Generic;
using MBMScripts;

namespace RaceBreedingExtension.UniqueLookColor
{
    /// <summary>可换色部位。</summary>
    public enum UniqueLookPart
    {
        Hair,
        Eye,
        Clothes,
    }

    /// <summary>
    /// 某一类「独特 Figure / 换皮 Rig」骨架的头发 / 眼睛染色规则。
    /// 按<b>槽位名前缀</b>匹配（各动作骨架的槽位命名会变化：站立/卧姿/性爱/分娩等骨架前缀不同），
    /// 因此统一用前缀表覆盖，见 <see cref="UniqueLookDefs.UniqueFigure"/>。
    /// </summary>
    internal sealed class UniqueLookDef
    {
        /// <summary>头发色槽位前缀（OrdinalIgnoreCase）。</summary>
        public string[] HairPrefixes = [];

        /// <summary>眼睛色槽位前缀（OrdinalIgnoreCase）。</summary>
        public string[] EyePrefixes = [];

        /// <summary>角/尾巴/兽耳色槽位前缀（由 HornColor 驱动；原版 SlaveHornColorPartList 亦含尾巴与兽耳）。</summary>
        public string[] HornPrefixes = [];

        /// <summary>服色槽位前缀（OrdinalIgnoreCase；仅当角色设置了服色覆盖时生效）。</summary>
        public string[] ClothPrefixes = [];
    }

    /// <summary>
    /// 独特角色换色数据注册表。
    /// 覆盖全部原版独特 Figure 家族（aure / bella / nero / karen / claire / vivi / anna / sylvia）
    /// 及其换皮前缀（GolemRig、angel、fallen、arcticwolf、moonelf、dairycow、weretiger、wererabbit、devil）。
    /// 以后新增独特 Figure 或新换皮 Rig 时，在此登记骨架槽位前缀即可复用整条渲染管线。
    /// </summary>
    internal static class UniqueLookDefs
    {
        /// <summary>
        /// 各独特 Figure 家族共用的发/眼/服槽位规则（各族槽位命名一致）。
        ///
        /// 头发部件（不同动作骨架的命名）：
        ///   hair_            —— hair_back / hair_front / hair_left / hair_right …
        ///   hairbundle       —— hairbundle / hairbundle_left / hairbundle_right / hairbundle_back …
        ///   longhair         —— longhair_left / longhair_right（woman 系长发）
        ///   head_back        —— 卧姿骨架背发（及 s_ 变体）
        ///   s_hair_ / s_hairbundle / s_longhair / s_head_back —— 性爱/动作骨架（s_ 前缀）
        ///   pussyhair / s_pussyhair —— 阴毛，随发色（原版 SlaveHairColorPartList 亦如此）
        ///
        /// 眼睛部件：
        ///   eyeball / eyeball_* / s_eyeball —— 虹膜（eyeball 纹理只含虹膜区域）
        ///   eyeheart / s_eyeheart           —— 桃心瞳变体
        ///   （eye_back 为纯白眼白、eyeblink/eyehair 为瞳孔覆盖层/睫毛，不染色；
        ///     不匹配 con_/ik_/m_ 等控制骨或雄性部件，也不匹配 hairband 等发饰）
        ///
        /// 角 / 尾巴 / 兽耳部件（由 <c>Character.HornColor</c> 驱动，
        /// 对应原版 SlaveHornColorPartList：角、尾巴、兽耳同属"角颜色"框）：
        ///   horn / s_horn / tail / s_tail / beastear / s_beastear
        ///
        /// 服色部件（仅当角色设置了服色覆盖时染色）：
        ///   cloth_ / s_cloth_ —— 全部衣物部件
        /// </summary>
        public static readonly UniqueLookDef UniqueFigure = new()
        {
            HairPrefixes = new[]
            {
                "hair_",
                "hairbundle",
                "longhair",
                "head_back",
                "s_hair_",
                "s_hairbundle",
                "s_longhair",
                "s_head_back",
                "pussyhair",
                "s_pussyhair",
            },
            EyePrefixes = new[]
            {
                "eyeball",
                "s_eyeball",
                "eyeheart",
                "s_eyeheart",
            },
            // 角颜色（Horn color，来自 Item_CosmeticPill / Item_SlaveCosmeticPill 的换色框）：
            // 原版 SlaveHornColorPartList 同时包含角、尾巴与兽耳，这里沿用同一语义。
            HornPrefixes = new[]
            {
                "horn",
                "s_horn",
                "tail",
                "s_tail",
                "beastear",
                "s_beastear",
            },
            // 服色：所有衣物部件（站立与 s_ 动作骨架两套命名）
            ClothPrefixes = new[]
            {
                "cloth_",
                "s_cloth_",
            },
        };

        /// <summary>
        /// 兽系独特角色（雪狼人 ArcticWerewolf / 魔兔人 MagicWererabbit）专用规则：
        ///   * 这两个种族没有「角颜色(Horn color)」换色框，无法手动改变角/尾/兽耳色；
        ///   * 其衣物极简，用 cloth_ 服色渲染会非常不协调。
        /// 因此：
        ///   * 停用 cloth_ / s_cloth_ 渲染（不染衣物）；
        ///   * 把「独特服色」这一路颜色改为渲染 尾巴 / 兽耳（tail、s_tail、beastear、s_beastear）；
        ///   * 骨架无角，仅保留 horn / s_horn 由 HornColor 驱动（若某动作骨架确有角）。
        /// 仅影响 arcticwolf / wererabbit 两个前缀，其它角色仍使用 <see cref="UniqueFigure"/>。
        /// </summary>
        public static readonly UniqueLookDef BeastTailLook = new()
        {
            HairPrefixes = UniqueFigure.HairPrefixes,
            EyePrefixes = UniqueFigure.EyePrefixes,
            HornPrefixes = new[]
            {
                "horn",
                "s_horn",
            },
            ClothPrefixes = new[]
            {
                "tail",
                "s_tail",
                "beastear",
                "s_beastear",
            },
        };

        /// <summary>
        /// 换皮前缀 → 骨架定义（与 SpeciesRegistry 中物种的 RigPrefix 一一对应）。
        /// 各族骨架（aure/bella/nero/karen/claire/vivi/anna/sylvia）的发/眼/服槽位命名一致，
        /// 因此默认共用同一份 <see cref="UniqueFigure"/> 规则；特殊种族用独立定义（如 <see cref="BeastTailLook"/>）。
        /// </summary>
        public static readonly Dictionary<string, UniqueLookDef> ByRigPrefix =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["GolemRig"] = UniqueFigure,    // 元素裔 Genasi（魔像 Golem 已改用原版 Woman 外观，不再换皮）
                ["angel"] = UniqueFigure,       // 天使 Angel（Bella 骨架）
                ["fallen"] = UniqueFigure,      // 堕天使 FallenAngel（Bella 骨架）
                ["arcticwolf"] = BeastTailLook, // 雪狼人 ArcticWerewolf（Karen 骨架）：服色→尾巴/兽耳
                ["moonelf"] = UniqueFigure,     // 月精灵 MoonElf（Claire 骨架）
                ["dairycow"] = UniqueFigure,    // 奶牛 Dairycow（Sylvia 骨架）
                ["weretiger"] = UniqueFigure,   // 虎人 Weretiger（Nero 骨架）
                ["wererabbit"] = BeastTailLook, // 魔兔人 MagicWererabbit（Vivi 骨架）：服色→尾巴/兽耳
                ["devil"] = UniqueFigure,       // 魔鬼 Devil（Anna 骨架）
            };

        /// <summary>游戏原版独特 Figure → 骨架定义（仅当开启"允许修改原版独特角色"选项时生效）。</summary>
        public static readonly Dictionary<EFigure, UniqueLookDef> ByFigure = new()
        {
            [EFigure.Aure] = UniqueFigure,
            [EFigure.Bella] = UniqueFigure,
            [EFigure.Karen] = BeastTailLook,
            [EFigure.Claire] = UniqueFigure,
            [EFigure.Sylvia] = UniqueFigure,
            [EFigure.Nero] = UniqueFigure,
            [EFigure.Vivi] = BeastTailLook,
            [EFigure.Anna] = UniqueFigure,
        };

        /// <summary>
        /// 资料性列表：游戏原版全部独特 Figure（预留用途）。
        /// 注意：不参与渲染判定；渲染只由 RigPrefix / 配置的 ByFigure 决定。
        /// </summary>
        public static readonly HashSet<EFigure> VanillaUniqueFigures = new()
        {
            EFigure.Sylvia,
            EFigure.Claire,
            EFigure.Aure,
            EFigure.Karen,
            EFigure.Vivi,
            EFigure.Bella,
            EFigure.Amilia,
            EFigure.Flora,
            EFigure.Niel,
            EFigure.Rune,
            EFigure.SenaLena,
            EFigure.Barbara,
            EFigure.Anna,
            EFigure.Nero,
            EFigure.Amilia2,
            EFigure.Flora2,
            EFigure.Niel2,
            EFigure.Sena2,
            EFigure.Lena2,
            EFigure.Barbara2,
        };

        public static UniqueLookDef? ResolveDef(string? rigPrefix, EFigure figure)
        {
            if (!string.IsNullOrEmpty(rigPrefix) && ByRigPrefix.TryGetValue(rigPrefix, out UniqueLookDef? def))
                return def;
            if (ByFigure.TryGetValue(figure, out def))
                return def;
            return null;
        }
    }
}
