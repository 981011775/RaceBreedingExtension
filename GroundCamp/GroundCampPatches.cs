#nullable enable
using System;
using HarmonyLib;
using MBMScripts;

namespace RaceBreedingExtension.GroundCamp
{
    /// <summary>
    /// 地面猎手营地（Ground Camp）Harmony 补丁：
    ///  - <see cref="PlayData.SetBuildList"/>：个人领地时把两个地面猎手营地追加到地面建造列表
    ///    （离开个人领地时自动取消选择，保证“仅个人领地可建造”）；
    ///  - <c>Room.RoomData</c>：地面猎手营地改用地面版 RoomData 副本（IsGround / 房间名 / 无客人）；
    ///  - <see cref="MonsterCage.IsAllocatable"/> / <see cref="MonsterCage2.IsAllocatable"/>：
    ///    地面猎手营地允许“怪物 + 奴隶”同时入住；原版怪物牢笼一律走原版逻辑。
    ///
    /// 说明：房间类型与房间类都保持原版（MonsterCage / MonsterCage2），
    /// 因此这里不会覆盖或改写任何原版数据，只对“槽位 Y &gt; 0 的怪物牢笼”生效。
    /// </summary>
    internal static class GroundCampPatches
    {
        // ============================ 建造列表（个人领地地面） ============================

        [HarmonyPatch(typeof(PlayData), nameof(PlayData.SetBuildList))]
        internal static class SetBuildListPatch
        {
            public static void Postfix(PlayData __instance)
            {
                try
                {
                    GroundCampSystem.AddToPrivateEstateBuildList(__instance);
                }
                catch (Exception e)
                {
                    GroundCampSystem.LogError("PlayData.SetBuildList failed: " + e);
                }
            }
        }

        // ============================ 房间数据（地面版副本） ============================

        /// <summary>
        /// 地面猎手营地改用地面版 RoomData 副本：
        /// IsGround = TRUE → 拆除时的支柱/地板逻辑按地面房间处理；DisplayName = 猎手营地 / 大型猎手营地。
        /// 其它房间（含原版地下怪物牢笼）原样返回。
        /// </summary>
        [HarmonyPatch(typeof(Room), "get_RoomData")]
        internal static class RoomDataPatch
        {
            public static void Postfix(Room __instance, ref RoomData __result)
            {
                try
                {
                    if (GroundCampSystem.TryGetGroundCampData(__instance, out RoomData data))
                        __result = data;
                }
                catch (Exception e)
                {
                    GroundCampSystem.LogError("Room.RoomData failed: " + e);
                }
            }
        }

        // ============================ 不可升级（猎手营地） ============================

        /// <summary>
        /// 猎手营地（个人领地地面上的 MonsterCage / MonsterCage2）禁止升级：
        /// <c>IsUpgradable</c> 直接返回 false（原版升级入口会在此处提前返回），
        /// 同时 <see cref="MonsterCage.Upgrade"/> / <see cref="MonsterCage2.Upgrade"/> 也做兜底拦截，
        /// 防止其它路径直接调用升级。原版地下怪物牢笼不受影响。
        /// </summary>
        [HarmonyPatch(typeof(MonsterCage), "get_IsUpgradable")]
        internal static class SmallCampNotUpgradablePatch
        {
            public static void Postfix(MonsterCage __instance, ref bool __result)
            {
                if (GroundCampConfig.Enable && GroundCampSystem.IsSmallGroundCamp(__instance))
                    __result = false;
            }
        }

        [HarmonyPatch(typeof(MonsterCage2), "get_IsUpgradable")]
        internal static class LargeCampNotUpgradablePatch
        {
            public static void Postfix(MonsterCage2 __instance, ref bool __result)
            {
                if (GroundCampConfig.Enable && GroundCampSystem.IsLargeGroundCamp(__instance))
                    __result = false;
            }
        }

        [HarmonyPatch(typeof(MonsterCage), nameof(MonsterCage.Upgrade))]
        internal static class SmallCampUpgradeBlockPatch
        {
            public static bool Prefix(MonsterCage __instance)
                => !(GroundCampConfig.Enable && GroundCampSystem.IsSmallGroundCamp(__instance));
        }

        [HarmonyPatch(typeof(MonsterCage2), nameof(MonsterCage2.Upgrade))]
        internal static class LargeCampUpgradeBlockPatch
        {
            public static bool Prefix(MonsterCage2 __instance)
                => !(GroundCampConfig.Enable && GroundCampSystem.IsLargeGroundCamp(__instance));
        }

        // ============================ 怪物 + 奴隶混住 ============================

        /// <summary>猎手营地（= 地面 MonsterCage）：小怪 + 奴隶。</summary>
        [HarmonyPatch(typeof(MonsterCage), nameof(MonsterCage.IsAllocatable))]
        internal static class SmallCampIsAllocatablePatch
        {
            public static bool Prefix(MonsterCage __instance, Unit unit, ref bool __result)
            {
                if (!GroundCampConfig.Enable || !GroundCampSystem.IsSmallGroundCamp(__instance))
                    return true;   // 原版怪物牢笼 → 完全走原版逻辑

                try
                {
                    __result = GroundCampSystem.IsAllocatable(__instance, unit, false);
                }
                catch (Exception e)
                {
                    GroundCampSystem.LogError("MonsterCage.IsAllocatable failed: " + e);
                    __result = false;
                }
                return false;
            }
        }

        /// <summary>大型猎手营地（= 地面 MonsterCage2）：大怪 + 奴隶。</summary>
        [HarmonyPatch(typeof(MonsterCage2), nameof(MonsterCage2.IsAllocatable))]
        internal static class LargeCampIsAllocatablePatch
        {
            public static bool Prefix(MonsterCage2 __instance, Unit unit, ref bool __result)
            {
                if (!GroundCampConfig.Enable || !GroundCampSystem.IsLargeGroundCamp(__instance))
                    return true;   // 原版大型怪物牢笼 → 完全走原版逻辑

                try
                {
                    __result = GroundCampSystem.IsAllocatable(__instance, unit, true);
                }
                catch (Exception e)
                {
                    GroundCampSystem.LogError("MonsterCage2.IsAllocatable failed: " + e);
                    __result = false;
                }
                return false;
            }
        }
    }
}
