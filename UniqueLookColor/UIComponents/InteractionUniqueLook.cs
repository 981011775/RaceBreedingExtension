#nullable enable
using System.Collections;
using MBMScripts;
using UnityEngine;
using RaceBreedingExtension.UniqueLookColor.Extra;

namespace RaceBreedingExtension.UniqueLookColor.UIComponents
{
    /// <summary>
    /// 颜色槽/取色器交互：启用时把取色器同步到角色当前目标色，拖动/取色后写回
    /// 角色的 HairColor / EyeColor（游戏自带字段，随存档持久化）
    /// 或服色（本模组扩展字段，随 ModSaveData 持久化）。
    /// 仅对 <see cref="UniqueLookColorSystem.IsEligible"/> 角色生效。
    /// </summary>
    public class InteractionUniqueLook : MonoBehaviour
    {
        public UniqueLookPart Part = UniqueLookPart.Hair;

        private FlexibleColorPicker m_FlexibleColorPicker = null!;
        private bool m_IsChangeable;

        private void Awake()
        {
            m_FlexibleColorPicker = GetComponent<FlexibleColorPicker>();
        }

        private void OnEnable()
        {
            StartCoroutine(EnableChangeAfterFrame());

            if (TryGetCharacter(out Character character) && m_FlexibleColorPicker != null)
                m_FlexibleColorPicker.color = GetCurrentColor(character);
        }

        private void OnDisable()
        {
            m_IsChangeable = false;
        }

        private IEnumerator EnableChangeAfterFrame()
        {
            yield return null;
            m_IsChangeable = true;
        }

        public void ChangeColor(Color color)
        {
            if (!m_IsChangeable)
                return;
            if (!TryGetCharacter(out Character character))
                return;
            if (!UniqueLookColorSystem.IsEligible(character))
                return;

            switch (Part)
            {
                case UniqueLookPart.Eye:
                    character.EyeColor = color;
                    break;
                case UniqueLookPart.Clothes:
                    UniqueLookColorSystem.SetClothesColor(character, color);
                    break;
                default:
                    character.HairColor = color;
                    break;
            }
        }

        private Color GetCurrentColor(Character character)
        {
            switch (Part)
            {
                case UniqueLookPart.Eye:
                    return character.EyeColor;
                case UniqueLookPart.Clothes:
                    return character.Extra().ClothesColor;
                default:
                    return character.HairColor;
            }
        }

        private bool TryGetCharacter(out Character character)
        {
            character = (GetComponentInParent<TargetUnit>()?.Unit as Character)!;
            return character != null;
        }
    }
}
