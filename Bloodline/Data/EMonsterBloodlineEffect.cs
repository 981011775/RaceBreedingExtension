namespace RaceBreedingExtension.Bloodline.Data;

/// <summary>
/// 怪物（雄性持有者）专属血脉效果类型：
/// 「如果 Trait 生成在怪物身上」时，在怪物触发 cumshot / 与女性性交期间触发的效果。
/// </summary>
internal enum EMonsterBloodlineEffect
{
    /// <summary>无怪物效果。</summary>
    None = 0,

    /// <summary>与其在 EState.Sex / EState.SexDrained 场景中的女性，GetSexProgressSpeed 的 speed 提升（数值 = 每级提升量）。</summary>
    PartnerSexSpeed = 1,

    /// <summary>怪物触发 cumshot 时，场景中女性角色的防护结界（DepravedShield）减少（数值 = 每级减少量）。</summary>
    PartnerShieldDrain = 2,

    /// <summary>怪物触发 cumshot 时，女性有几率触发特质升级（数值 = 每级几率 %）。</summary>
    PartnerTraitUpgrade = 3,

    /// <summary>怪物触发 cumshot 时，女性有几率恢复 50% 体力（数值 = 每级几率 %）。</summary>
    PartnerStaminaRestore = 4,
}
