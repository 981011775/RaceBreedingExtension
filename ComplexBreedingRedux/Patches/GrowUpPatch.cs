using ComplexBreedingRedux.Data;
using ComplexBreedingRedux.Data.Species;
using ComplexBreedingRedux.Features;
using HarmonyLib;
using MBMScripts;

namespace ComplexBreedingRedux.Patches
{
    /// <summary>
    /// <c>Character.ChangeToAdult</c>（喂母乳 / 切换体型 → 成体阶段）之后，
    /// 为「有 RigPrefix 的物种」重新套用成人骨架外观。
    ///
    /// ★ 注意：这里必须使用 <see cref="FemaleSpeciesSetup.RefreshSpeciesAppearance"/>，
    /// 绝不能调用 <see cref="FemaleSpeciesSetup.SetSpecies"/>——
    /// 后者会先执行 ClearRaceTrait/ClearTrait，导致成人化后普通特质
    /// （职业、血脉、Trait93~Trait100、一般特质等）被清空（虎人/雪狼人等物种的旧 bug）。
    /// </summary>
    [HarmonyPatch(typeof(Character), "ChangeToAdult")]
    internal static class ChangeToAdultPatch
    {
        public static void Postfix(Character __instance)
        {
            if (__instance is not Female female) return;
            string? identity = GameData.GetSpeciesIdentity(female);
            if (string.IsNullOrEmpty(identity)) return;

            if (GameData.TryGetSpecies(identity, out ISpeciesData? species) && species != null)
            {
                // 只对需要换皮的物种执行切换（有 RigPrefix）
                if (!string.IsNullOrEmpty(species.RigPrefix))
                {
                    try
                    {
                        FemaleSpeciesSetup.RefreshSpeciesAppearance(female, species);
                    }
                    catch (System.Exception e)
                    {
                        UnityEngine.Debug.LogError("[CBR] ChangeToAdult 物种外观刷新失败：" + e);
                    }
                }
            }
        }
    }
}