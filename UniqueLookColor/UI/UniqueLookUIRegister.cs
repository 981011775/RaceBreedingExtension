#nullable enable
using System;

namespace RaceBreedingExtension.UniqueLookColor.UI
{
    /// <summary>
    /// 独特角色换色 UI 注册入口（游戏初始化后调用一次）。
    /// 仅注入「女性信息窗口 → 扩展区」的发色 / 瞳色 / 服色槽；
    /// 按要求<b>不</b>向画廊注入任何 UI（画廊注入会导致 UI 超框）。
    /// </summary>
    internal static class UniqueLookUIRegister
    {
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized)
                return;
            if (!UniqueLookColorConfig.Enable)
                return;
            _initialized = true;

            try
            {
                UniqueLookInfoSlotUI.InjectSlots();
            }
            catch (Exception e)
            {
                UniqueLookColorSystem.LogError("UniqueLookInfoSlotUI inject failed: " + e);
            }
        }
    }
}
