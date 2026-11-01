#nullable enable
using System.Linq;
using ComplexBreedingRedux.Data;
using ComplexBreedingRedux.Data.Professions;
using ComplexBreedingRedux.Utilities;
using HarmonyLib;
using MBMScripts;

namespace ComplexBreedingRedux.Features;

[HarmonyPatch(typeof(Female), "OnDailyUpdate")]
internal class OnDailyUpdatePatch
{
    public static bool Prefix() => false;
}

[HarmonyPatch(typeof(PlayData), "UpdateTime")]
internal class UpdateTimePatch
{
    private static EDayState LastDayState = EDayState.None;

    public static void Prefix(PlayData __instance, EDayState ___m_DayState)
    {
        // 仅在从白天切换到夜晚时执行每日更新
        if (___m_DayState == EDayState.DayToNight && LastDayState == EDayState.Day)
        {
            PlayData playerData = GameManager.Instance.PlayerData;

            // ★ 血脉（Bloodline）日常维护：清理旧高等种族特质残留值，
            //   并按物种补发 / 校正血脉（同名血脉只提升到物种最低等级，已有的更高等级不会被降级）。
            foreach (Character female in playerData.GetUnitList(ESector.Female).Cast<Character>())
                RaceBreedingExtension.Bloodline.BloodlineSystem.ApplySpeciesBloodline(female);
            foreach (Character male in playerData.GetUnitList(ESector.Male).Cast<Character>())
                RaceBreedingExtension.Bloodline.BloodlineSystem.ApplySpeciesBloodline(male);

            // 对每个女性执行职业工作
            foreach (Female female in playerData.GetUnitList(ESector.Female).Cast<Female>())
            {
                if (GameData.TryGetProfession(female, out IProfessionData? profession) && profession != null)
                    ProfessionSystem.Work(female, profession);
            }
        }

        LastDayState = ___m_DayState;
    }
}