#nullable enable
using MBMScripts;
using UnityEngine;
using RaceBreedingExtension.UniqueLookColor.Extra;

namespace RaceBreedingExtension.UniqueLookColor.UIComponents
{
    /// <summary>
    /// 读取独特角色目标颜色（发色 / 瞳色 / 服色）的 Reference。
    /// 兼容两种用法：
    ///   - 显示颜色值（AsColorText=false，绑定 BinderTextMeshPro）
    ///   - 显示本地化颜色名文本（AsColorText=true，绑定 BinderTextMeshProText）
    /// 参照 MoreGraphicAttachments.UIComponents.ReferenceClothes。
    /// </summary>
    public class ReferenceUniqueLook : Reference
    {
        public UniqueLookPart Part = UniqueLookPart.Hair;
        public bool AsColorText;

        public override void Initialize()
        {
            ReferenceType = AsColorText ? EReferenceType.String : EReferenceType.Color;
        }

        public override Color GetColor()
        {
            if (GetCharacter() is Character c)
            {
                switch (Part)
                {
                    case UniqueLookPart.Eye:
                        return c.EyeColor;
                    case UniqueLookPart.Clothes:
                        return c.Extra().ClothesColor;
                    default:
                        return c.HairColor;
                }
            }
            return Color.white;
        }

        public override string GetString()
        {
            if (GetCharacter() is Character)
            {
                Color color = GetColor();
                // 服色默认白色 = 未设置，显示为"无"
                if (Part == UniqueLookPart.Clothes && color == Color.white)
                    return SeqLocalization.Localize("#None");
                return SeqLocalization.Localize(GetColorText(color));
            }
            return string.Empty;
        }

        private Character? GetCharacter()
        {
            TargetUnit targetUnit = base.Updater?.TargetUnit;
            return (targetUnit?.Unit) as Character;
        }

        /// <summary>把颜色归入游戏内建的 #Color_* 颜色名键（与 MGA 相同算法）。</summary>
        public static string GetColorText(Color color)
        {
            Color.RGBToHSV(color, out float H, out float S, out float V);
            if (V < 0.1f) return "#Color_Black";
            if (S < 0.17f) return "#Color_Gray";
            if (H < 0.0305555f) return "#Color_Red";
            if (H < 0.125f) return V > 0.75f ? "#Color_Orange" : "#Color_Brown";
            if (H < 0.1777777f) return "#Color_Yellow";
            if (H < 0.4166666f) return "#Color_Green";
            if (H < 0.5f) return "#Color_Cyan";
            if (H < 17f / 24f) return "#Color_Blue";
            if (H < 31f / 36f) return "#Color_Purple";
            if (H < 17f / 18f) return "#Color_Pink";
            if (H < 0.975f) return V > 0.7f ? "#Color_Red" : "#Color_Pink";
            return "#Color_Red";
        }
    }
}
