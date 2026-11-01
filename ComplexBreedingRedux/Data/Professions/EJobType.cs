namespace ComplexBreedingRedux.Data.Professions;

internal enum EJobType
{
    RoomCleaning,
    EarningMoney,
    Healing,
    SummonPixy,
    HelpingNiel,
    HelpingFlora,
    HelpingBarbara,
    Corruption,
    Cleansing,
    CreateGolem,

    // ─── 职业拓展：以下工作类型的效果一律由 JobDescription.ItemReward / ShieldRemoval 驱动 ───

    /// <summary>采集 / 狩猎 / 冒险等产出物品的工作（配 ItemReward）。</summary>
    Gathering,
    /// <summary>制作药剂 / 服务等产出物品的工作（配 ItemReward）。</summary>
    CraftingPotion,
    /// <summary>消除奴隶角色防护结界(DepravedShield)的工作（配 ShieldRemoval）。</summary>
    RemoveShield,

    /// <summary>被动特性行（如猎手营地加成/公主加成等）：只在工作栏以小字展示，不参与每日工作执行。</summary>
    PassiveEffect,
}
