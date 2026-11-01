#nullable enable
using MBMScripts;
using UnityEngine;

namespace RaceBreedingExtension.UniqueLookColor.UIComponents
{
    /// <summary>
    /// 取色器 Updater：目标数据变化时把取色器颜色同步到目标颜色（参照 MGA UpdaterColorPicker）。
    /// </summary>
    public class UpdaterUniqueLookColorPicker : Updater
    {
        private FlexibleColorPicker m_ColorPicker = null!;

        protected override void Awake()
        {
            base.Awake();
            m_ColorPicker = GetComponent<FlexibleColorPicker>();
        }

        protected override void Display()
        {
            if (m_ColorPicker == null)
                return;
            foreach (Reference reference in ReferenceArray)
            {
                if (reference.ReferenceType == EReferenceType.Color)
                    m_ColorPicker.color = reference.GetColor();
            }
        }
    }
}
