#nullable enable
using System.Linq;
using ComplexBreedingRedux.Data;
using ComplexBreedingRedux.Data.SpeciesCore;
using HarmonyLib;
using MBMScripts;

namespace ComplexBreedingRedux.Patches.UnitPatches;

/// <summary>
/// <see cref="Character.FirstSexPartner"/>:
/// 如果第一个性伴侣的名称未设置（空），则自动设置为伙伴的种族名称。
/// 派系相关逻辑已删除。
/// </summary>
[HarmonyPatch(typeof(Character), "FirstSexPartner", MethodType.Setter)]
internal static class FirstSexPartnerPatch
{
    public static void Postfix(Character __instance, int ___m_FirstCumShotInsidePartnerUnitId, Unit value)
    {
        // 如果没有记录第一个性伴侣ID，则不做处理
        if (___m_FirstCumShotInsidePartnerUnitId == 0)
            return;

        string name = __instance.FirstSexPartnerName;

        // 如果名称为空且伙伴是角色，则设置为伙伴的种族名称
        if (string.IsNullOrEmpty(name) && value is Character partner)
        {
            __instance.FirstSexPartnerName = partner.RaceName;
        }
    }
}