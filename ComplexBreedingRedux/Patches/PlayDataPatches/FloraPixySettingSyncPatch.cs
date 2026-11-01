#nullable enable
using ComplexBreedingRedux.Features;
using HarmonyLib;
using MBMScripts;

namespace ComplexBreedingRedux.Patches.PlayDataPatches;

/// <summary>
/// 「妖精管理」设置修复：8 个全局妖精开关（FloraPixy*）在变更后立即同步到所有已拥有的角色。
///
/// 原版角色只在创建时复制一次全局设置，而妖精 AI 要求「角色开关 &amp;&amp; 全局开关」同时为真，
/// 所以改动全局设置对已有角色无效。详见 <see cref="FloraPixySettingSync"/>。
/// </summary>
[HarmonyPatch(typeof(PlayData), "set_FloraPixyHealth")]
internal static class FloraPixyHealthSyncPatch
{
    public static void Postfix(PlayData __instance)
        => FloraPixySettingSync.Sync(__instance, c => c.PixyHealth = __instance.FloraPixyHealth);
}

[HarmonyPatch(typeof(PlayData), "set_FloraPixyBirth")]
internal static class FloraPixyBirthSyncPatch
{
    public static void Postfix(PlayData __instance)
        => FloraPixySettingSync.Sync(__instance, c => c.PixyBirth = __instance.FloraPixyBirth);
}

[HarmonyPatch(typeof(PlayData), "set_FloraPixyBaby")]
internal static class FloraPixyBabySyncPatch
{
    public static void Postfix(PlayData __instance)
        => FloraPixySettingSync.Sync(__instance, c => c.PixyBaby = __instance.FloraPixyBaby);
}

[HarmonyPatch(typeof(PlayData), "set_FloraPixyMilk")]
internal static class FloraPixyMilkSyncPatch
{
    public static void Postfix(PlayData __instance)
        => FloraPixySettingSync.Sync(__instance, c => c.PixyMilk = __instance.FloraPixyMilk);
}

[HarmonyPatch(typeof(PlayData), "set_FloraPixyDead")]
internal static class FloraPixyDeadSyncPatch
{
    public static void Postfix(PlayData __instance)
        => FloraPixySettingSync.Sync(__instance, c => c.PixyDead = __instance.FloraPixyDead);
}

[HarmonyPatch(typeof(PlayData), "set_FloraPixyShowFirst")]
internal static class FloraPixyShowFirstSyncPatch
{
    public static void Postfix(PlayData __instance)
        => FloraPixySettingSync.Sync(__instance, c => c.PixyShowFirst = __instance.FloraPixyShowFirst);
}

[HarmonyPatch(typeof(PlayData), "set_FloraPixySell")]
internal static class FloraPixySellSyncPatch
{
    public static void Postfix(PlayData __instance)
        => FloraPixySettingSync.Sync(__instance, c => c.PixySell = __instance.FloraPixySell);
}

[HarmonyPatch(typeof(PlayData), "set_FloraPixyCapsule")]
internal static class FloraPixyCapsuleSyncPatch
{
    public static void Postfix(PlayData __instance)
        => FloraPixySettingSync.Sync(__instance, c => c.PixyCapsule = __instance.FloraPixyCapsule);
}
