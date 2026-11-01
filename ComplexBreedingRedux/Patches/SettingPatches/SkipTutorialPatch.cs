#nullable enable
using HarmonyLib;
using MBMScripts;

namespace ComplexBreedingRedux.Patches.SettingPatches;

// 完成基础初始化后——设置庄园名称。
// SavePoint2010100 不在此处设置：它在 SkipTutorialRoomCreationPatch 中实际创建房间后设置，以作为可靠的防护标志。
[HarmonyPatch(typeof(PlayData), "SetGameEventFlag")]
internal static class SkipTutorialNewGamePatch
{
    public static void Postfix(PlayData __instance, EGameEvent gameEvent, bool flag)
    {
        if (!flag || gameEvent != EGameEvent.SavePoint_10000000 || !Setting.Instance.SkipTutorial)
            return;
        if (string.IsNullOrEmpty(__instance.EstateDisplayName))
            __instance.EstateDisplayName = "Estate";
    }
}

// 在创建 SlaveCage 后（意味着 Home 已存在且 x=2000 区域已初始化），将 SlaveCage、MonsterCage 和 BreedingRoom 添加到主建筑中——仅当它们尚不存在时。
// _creating 防止递归：我们的 NewRoom(SlaveCage) 会再次触发此 Postfix。
// SavePoint2010100 是主要防护：第一次执行后设置标志，防止玩家后续任何建造 SlaveCage 时再次触发。
[HarmonyPatch(typeof(PlayData), "NewRoom")]
internal static class SkipTutorialRoomCreationPatch
{
    private static bool _creating;

    public static void Postfix(PlayData __instance, ERoomType roomType)
    {
        if (roomType != ERoomType.SlaveCage || !Setting.Instance.SkipTutorial || _creating)
            return;
        if (GameManager.Instance.GetWindowState(EGameWindow.SpineTest))
            return;
        if (__instance.GetGameEventFlag(EGameEvent.SavePoint2010100))
            return;
        if (__instance.GetUnit(ERoomType.MonsterCage) != null)
            return;
        _creating = true;
        try
        {
            __instance.NewRoom(ERoomType.SlaveCage, ESector.Room, (-2, -2), 0);
            __instance.NewRoom(ERoomType.MonsterCage, ESector.Room, (2, -2), 0);
            __instance.NewRoom(ERoomType.BreedingRoom, ESector.Room, (0, -2), 0);
            __instance.NewCharacter(EUnitType.Human, ESector.Female, (0, -2), -1, false);
            __instance.NewCharacter(EUnitType.Goblin, ESector.Male, (0, -2), -1, false);
            __instance.SetPayData();
            __instance.SetGameEventFlag(EGameEvent.SavePoint2010100, true);
            var breedingRoom = __instance.GetUnit(ERoomType.BreedingRoom);
            if (breedingRoom != null)
            {
                GameManager.Instance.SetCameraPositionToUnit(breedingRoom, 0f);
                __instance.CameraPosition = GameManager.Instance.GetCameraPosition();
                __instance.CameraOrthographicSizeRate = 2f;
            }
        }
        finally
        {
            _creating = false;
        }
    }
}