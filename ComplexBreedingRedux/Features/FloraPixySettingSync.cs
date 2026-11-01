#nullable enable
using System;
using MBMScripts;
using UnityEngine;

namespace ComplexBreedingRedux.Features;

/// <summary>
/// 「妖精管理」设置同步修复。
///
/// 原版缺陷：每个角色的 8 个妖精（Flora / Pixy）开关
/// （PixyHealth / PixyBirth / PixyBaby / PixyMilk / PixyDead / PixyShowFirst / PixySell / PixyCapsule）
/// 只在**角色创建时**从全局设置复制一次（Female / Male 构造函数中 `PixyX = playerData.FloraPixyX`），
/// 而妖精 AI 的判定要求「角色自身开关 &amp;&amp; 全局开关」同时为真。
/// 因此当全局设置为关时创建/已有的奴隶，之后即使把「妖精管理」里的设置打开，
/// 她自身的开关仍停留在 false → 设置更改完全无效（本修复解决的问题）。
///
/// 修复方式：拦截 <see cref="PlayData"/> 的 8 个全局设置属性 setter，设置变更后立即把新值
/// 同步到玩家已拥有的所有角色（女性 / 雄性 / 奴隶市场 / 饲养区），使设置修改立刻生效。
/// 角色自身的单开关（Ctrl/Alt 批量切换）依旧可用，只是每次改动全局设置时会被覆盖为全局值。
/// </summary>
internal static class FloraPixySettingSync
{
    /// <summary>参与同步的分区（只动玩家自己的角色，不触碰 NPC / 特殊角色）。</summary>
    private static readonly ESector[] Sectors =
    {
        ESector.Female, ESector.Male, ESector.Market, ESector.Keeping,
    };

    /// <summary>把某个全局妖精设置的新值同步到所有已拥有的角色。</summary>
    public static void Sync(PlayData? pd, Action<Character> apply)
    {
        if (pd == null || apply == null)
            return;

        foreach (ESector sector in Sectors)
        {
            try
            {
                foreach (Unit unit in pd.GetUnitList(sector))
                {
                    if (unit is Character character)
                        apply(character);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CBR][Flora] 妖精设置同步失败（{sector}）：{e.Message}");
            }
        }
    }
}
