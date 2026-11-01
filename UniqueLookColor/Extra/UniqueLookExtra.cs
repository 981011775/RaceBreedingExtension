#nullable enable
using MBMScripts;
using UnityEngine;

namespace RaceBreedingExtension.UniqueLookColor.Extra
{
    /// <summary>
    /// 独特角色的扩展外观数据。头/瞳色直接使用游戏自带的 <see cref="Character.HairColor"/> /
    /// <see cref="Character.EyeColor"/>（随存档持久化），此处只保存游戏没有的「服色」。
    /// 结构参照 MoreGraphicAttachments.ExtensionField.CharacterExtra。
    /// </summary>
    public class UniqueLookExtra
    {
        private readonly Character m_Character;

        /// <summary>默认白色 = 不染色（乘染语义下白色不改变原贴图）。</summary>
        private Color m_ClothesColor = Color.white;

        /// <summary>
        /// 是否被<b>显式设置</b>过服色（含玩家把颜色拖成白色的「取消染色」，以及存档读入的值）。
        /// 用途：① 存档需要保存「显式白色」，否则读档后会被物种初始服色重新填上；
        ///       ② 物种初始服色（见 <see cref="UniqueLookColorSystem.ApplySpeciesClothesColor"/>）
        ///          不得覆盖玩家的显式选择。
        /// </summary>
        private bool m_ClothesColorSet;

        public Color ClothesColor
        {
            get
            {
                SeqDataBinding.Instance.RegisterFlag(m_Character);
                return m_ClothesColor;
            }
            set
            {
                if (m_ClothesColor == value)
                    return;

                // 触发一次数据脏标记 + 部件重建，使新颜色立刻反映到骨架槽位
                SeqDataBinding.Instance.DirtyFlag(m_Character);
                m_ClothesColor = value;
                m_ClothesColorSet = true;
                Unit.RemovePartData(m_Character);
            }
        }

        public UniqueLookExtra(Character character)
        {
            m_Character = character;
        }

        public UniqueLookExtra(Character character, int unitId, string? clothesColorHex)
        {
            m_Character = character;
            ApplyLoadedClothesColor(clothesColorHex);
        }

        /// <summary>读档时直接写入字段（不触发 DirtyFlag，避免初始化阶段重复重建部件）。</summary>
        internal void ApplyLoadedClothesColor(string? clothesColorHex)
        {
            if (string.IsNullOrEmpty(clothesColorHex))
                return;
            if (ColorUtility.TryParseHtmlString(clothesColorHex, out Color color))
            {
                m_ClothesColor = color;
                // 存档中存在该角色的服色记录 ⇒ 曾被显式设置（包括显式白色 = 取消染色）
                m_ClothesColorSet = true;
            }
        }

        /// <summary>是否设置了服色覆盖（白色视为未覆盖）。</summary>
        public bool HasClothesColorOverride => m_ClothesColor != Color.white;

        /// <summary>是否曾被显式设置过服色（含显式设为白色 = 取消染色）。</summary>
        public bool ClothesColorSet => m_ClothesColorSet;

        /// <summary>
        /// 运行期标记（不存档）：上一帧是否真的把服色写进了骨架槽位。
        /// 用于「取消服色」时补一次白色还原，同时避免对从未设置过服色的角色覆盖原版染色。
        /// </summary>
        public bool ClothesTintApplied { get; set; }

        public bool NeedSave() => ClothesColorSet;

        public string ToColorHex() => "#" + ColorUtility.ToHtmlStringRGBA(m_ClothesColor);
    }
}
