using System.Reflection;
using MBMScripts;
using TMPro;
using UnityEngine;

namespace DepravedShield.Helpers
{
    public static class PopupHelper
    {
        public static void ShowPopup(Character source, string text)
        {
            if (source == null) return;
            source.PopUpMessage(text, null, 0.25f);
            var field = typeof(Unit).GetField("m_PopUpText", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                var popUpText = field.GetValue(source) as PopUpText;
                if (popUpText != null)
                {
                    var textProp = typeof(PopUpText).GetProperty("Text");
                    if (textProp != null)
                    {
                        var tmpText = textProp.GetValue(popUpText) as TMP_Text;
                        if (tmpText != null)
                        {
                            tmpText.text = text;
                            tmpText.color = Color.magenta;
                            tmpText.fontSize = 1.5f;
                            tmpText.SetAllDirty();
                        }
                    }
                }
            }
        }
    }
}