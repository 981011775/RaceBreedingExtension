#nullable enable
using DepravedShield.Helpers;
using HarmonyLib;
using MBMScripts;

namespace DepravedShield.Patches
{
    /// <summary>
    /// 拷问室（Capsule）限制：拥有防护结界（DepravedShield &gt; 0）的奴隶不能被放入拷问室。
    ///
    /// 原版放置与妖精（Flora）自动系统的移动都会走 <see cref="Room.IsAllocatable"/> 校验
    /// （PlayData.Allocate → room.IsAllocatable），因此这里拦截一次即可同时阻止：
    ///   1) 玩家手动把该奴隶拖入拷问室；
    ///   2) 妖精自动系统把她送往拷问室（PixyCapsule / FloraPixyCapsule 判定）。
    /// </summary>
    [HarmonyPatch(typeof(Capsule), nameof(Capsule.IsAllocatable))]
    public static class CapsuleAllocationPatch
    {
        public static bool Prefix(Unit unit, ref bool __result)
        {
            if (!ModConfig.Enable)
                return true;

            if (unit is not Female female)
                return true;

            if (SexProgressManager.GetDepravedShield(female) <= 0)
                return true;

            __result = false;
            // ★ 不再输出日志：该判定会在每次分配校验（玩家拖拽 / 妖精自动系统）时触发，
            //   属于高频冗余输出，会刷屏 output_log.txt / ModLoader.log。
            return false; // 跳过原版判定，直接判定为不可入住
        }
    }
}
