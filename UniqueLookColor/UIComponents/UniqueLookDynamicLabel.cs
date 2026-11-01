#nullable enable
using System;
using MBMScripts;
using TMPro;
using UnityEngine;

namespace RaceBreedingExtension.UniqueLookColor.UIComponents
{
    /// <summary>
    /// 让"独特服色"槽的左侧标签随当前显示的角色变化：
    /// 普通独特角色 → 独特服色；雪狼人 / 魔兔人（BeastTailLook）→ 独特毛色（控制尾巴 / 兽耳）。
    /// 原版标签由 <c>ReferenceString</c> 驱动，本组件在文本被改写后会自动纠正回来（每帧比对，开销极低）。
    /// </summary>
    public class UniqueLookDynamicLabel : MonoBehaviour
    {
        /// <summary>根据角色返回标签文本。</summary>
        public Func<Character?, string>? ResolveText;

        private TMP_Text? m_Text;

        private void Awake()
        {
            m_Text = GetComponent<TMP_Text>() ?? GetComponentInChildren<TMP_Text>(true);
        }

        private void LateUpdate()
        {
            if (m_Text == null)
                return;

            Character? character = (GetComponentInParent<TargetUnit>()?.Unit) as Character;
            string want = ResolveText?.Invoke(character) ?? string.Empty;
            if (!string.IsNullOrEmpty(want) && m_Text.text != want)
                m_Text.text = want;
        }
    }
}
