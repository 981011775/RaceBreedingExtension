#nullable enable
using MBMScripts;

namespace ComplexBreedingRedux.Utilities;

internal static class PlayDataUtils
{
    public static PlayData GetInstance()
        => GameManager.Instance.PlayerData;

    public static void AddAchievement(EAchievement achievement)
        => GetInstance().AddAchievement(achievement, 1, true, false);
}
