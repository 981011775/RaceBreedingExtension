#nullable enable
using System.Collections.Generic;
using MBMScripts;

namespace ComplexBreedingRedux.Data.Professions;

/// <summary>
/// 职业注册中心（职业系统拓展版）。
/// 品阶体系：
///   1 普通 (Common)：Maid / Farmhand / Blacksmith / Brigand
///   2 不凡 (Uncommon)：Warrior / Hunter / Cultist / Assassin / Thief
///   3 稀有 (Rare)：Priest / Wizard / Knight / Apothecary / Marauder
///   4 史诗 (Epic)：Witch / Artificer / Dark Priest / Merchant / Plunderer
///   5 传说 (Legendary)：Princess / Paladin / Alchemist / Hero / Trainer
/// </summary>
internal static class ProfessionRegistry
{
    // ============================ 职业 ID 常量 ============================

    public const int PrincessId   = 2;
    public const int MaidId       = 3;
    public const int FarmhandId   = 4;
    public const int PriestId     = 5;
    public const int WizardId     = 6;
    public const int WarriorId    = 7;
    public const int KnightId     = 8;
    public const int WitchId      = 9;
    public const int PaladinId    = 10;
    public const int BlacksmithId = 11;
    public const int CultistId    = 12;
    public const int HunterId     = 13;
    public const int ArtificerId  = 14;

    // 新增职业（怪物可遗传的 6 个以 ★ 标注）
    public const int BrigandId    = 15; // ★
    public const int AssassinId   = 16; // ★
    public const int ThiefId      = 17; // ★
    public const int MarauderId   = 18; // ★
    public const int PlundererId  = 19; // ★
    public const int ApothecaryId = 20;
    public const int DarkPriestId = 21;
    public const int MerchantId   = 22;
    public const int AlchemistId  = 23;
    public const int HeroId       = 24; // 勇者（传说级战士，与原版战士区分）
    public const int TrainerId    = 25; // ★

    // ============================ 职业定义 ============================

    public static readonly ProfessionData Princess = new("Princess", PrincessId, 5)
    {
        Jobs = new List<JobDescription>
        {
            Passive("Profession_Princess_Passive"),
        },
        GuaranteedTraits = new List<ETrait> { (ETrait)30 },
    };

    public static readonly ProfessionData Maid = new("Maid", MaidId, 1)
    {
        Jobs = new List<JobDescription>
        {
            new JobDescription { Name = JobNames.Cleaning, Value = 1f, ValueMax = 1f, Chance = 70, FullyDepraved = false, Type = EJobType.RoomCleaning },
            new JobDescription { Name = JobNames.Foraging, Value = 1f, ValueMax = 1f, Chance = 50, FullyDepraved = true,  Type = EJobType.Gathering, ItemReward = EItemType.GoblinSemen },
        },
        Teachable = true,
        BaseChance = 40,
    };

    public static readonly ProfessionData Farmhand = new("Farmhand", FarmhandId, 1)
    {
        Jobs = new List<JobDescription>
        {
            new JobDescription { Name = JobNames.Foraging, Value = 1f, ValueMax = 1f, Chance = 70, FullyDepraved = true, Type = EJobType.Gathering, ItemReward = EItemType.BodyFluid },
        },
    };

    public static readonly ProfessionData Priest = new("Priest", PriestId, 3)
    {
        Jobs = new List<JobDescription>
        {
            KillInfo("Profession_Priest_Kill"),
            new JobDescription { Name = JobNames.PotionCrafting,       Value = 1f,  ValueMax = 1f,  Chance = 60,                        Type = EJobType.CraftingPotion, ItemReward = EItemType.HealthRecoveryInjection },
            new JobDescription { Name = JobNames.CleansingDepravation, Value = 15f, ValueMax = 30f, Chance = 50, FullyDepraved = false, Type = EJobType.Cleansing },
        },
        Teachable = true,
        BaseChance = 25,
        ForbiddenStartingSpecies = new List<ERace> { ERace.Neko },
        // 未堕落：Sex 触发 / Sex 结束时，战斗力 > 怪物 → 25%；≥2 倍 → 100%
        KillMode = EProfessionKillMode.SingleTarget,
        KillChance = 25,
    };

    public static readonly ProfessionData Wizard = new("Wizard", WizardId, 3)
    {
        Jobs = new List<JobDescription>
        {
            KillInfo("Profession_Wizard_Kill"),
            new JobDescription { Name = JobNames.HelpingNiel,       Value = 1f, ValueMax = 1f, Chance = 15,                        Type = EJobType.HelpingNiel  },
            new JobDescription { Name = JobNames.HelpingFlora,      Value = 2f, ValueMax = 2f, Chance = 15,                        Type = EJobType.HelpingFlora },
            new JobDescription { Name = JobNames.HormoneExtraction, Value = 1f, ValueMax = 2f, Chance = 50, FullyDepraved = true,  Type = EJobType.Gathering, ItemReward = EItemType.HumanPheromone, ItemCountMin = 1, ItemCountMax = 3 },
        },
        Teachable = true,
        BaseChance = 15,
        // 未堕落：Sex 触发 / Sex 结束时，房间内 25% 几率秒杀战力低于自己的怪物（仅猎手营地）
        KillMode = EProfessionKillMode.RoomWeakMonsters,
        KillChance = 25,
    };

    public static readonly ProfessionData Warrior = new("Warrior", WarriorId, 2)
    {
        Jobs = new List<JobDescription>
        {
            Passive("Profession_Warrior_Passive"),
            KillInfo("Profession_Warrior_Kill"),
            new JobDescription { Name = JobNames.Adventuring, Value = 1f, ValueMax = 1f, Chance = 60, FullyDepraved = true, Type = EJobType.Gathering, ItemReward = EItemType.OrcHeart },
        },
        Teachable = true,
        BaseChance = 25,
        // 战斗力统计加成 +20%
        CombatPowerBonus = 0.20f,
        KillMode = EProfessionKillMode.SingleTarget,
        KillChance = 25,
    };

    public static readonly ProfessionData Knight = new("Knight", KnightId, 3)
    {
        Jobs = new List<JobDescription>
        {
            Passive("Profession_Knight_Passive"),
            KillInfo("Profession_Knight_Kill"),
            new JobDescription { Name = JobNames.Adventuring, Value = 1f, ValueMax = 1f, Chance = 70, FullyDepraved = true, Type = EJobType.Gathering, ItemReward = EItemType.DragonTailMeat },
        },
        Teachable = true,
        BaseChance = 10,
        // 战斗力统计加成 +40%；未堕落：只要战斗力 > 怪物 → 100% 秒杀
        CombatPowerBonus = 0.40f,
        KillMode = EProfessionKillMode.SingleTarget,
        KillChance = 100,
    };

    public static readonly ProfessionData Witch = new("Witch", WitchId, 4)
    {
        Jobs = new List<JobDescription>
        {
            KillInfo("Profession_Witch_Kill"),
            new JobDescription { Name = JobNames.SummonPixy,       Value = 1f, ValueMax = 2f, Chance = 10,                        Type = EJobType.SummonPixy },
            new JobDescription { Name = JobNames.Witchcraft,       Value = 1f, ValueMax = 1f, Chance = 35, FullyDepraved = true,  Type = EJobType.CraftingPotion, ItemReward = EItemType.MammaryGlandRecoveryInjection },
            new JobDescription { Name = JobNames.WitchcraftPotion, Value = 1f, ValueMax = 1f, Chance = 35, FullyDepraved = true,  Type = EJobType.CraftingPotion, ItemReward = EItemType.OvumRecoveryInjection },
        },
        Teachable = true,
        BaseChance = 10,
        // 未堕落：Sex 触发 / Sex 结束时，50% 几率秒杀房间内战力低于自己的怪物（仅猎手营地）
        KillMode = EProfessionKillMode.RoomWeakMonsters,
        KillChance = 50,
    };

    public static readonly ProfessionData Paladin = new("Paladin", PaladinId, 5)
    {
        Jobs = new List<JobDescription>
        {
            Passive("Profession_Paladin_Passive"),
            KillInfo("Profession_Paladin_Kill"),
            new JobDescription { Name = JobNames.CleansingDepravation, Value = 20f, ValueMax = 30f, Chance = 80, FullyDepraved = false, Type = EJobType.Cleansing },
            new JobDescription { Name = JobNames.PotionCrafting,       Value = 1f,  ValueMax = 1f,  Chance = 80,                        Type = EJobType.CraftingPotion, ItemReward = EItemType.HealthRecoveryInjection },
            new JobDescription { Name = JobNames.PotionCrafting,       Value = 1f,  ValueMax = 1f,  Chance = 70, FullyDepraved = true,  Type = EJobType.CraftingPotion, ItemReward = EItemType.MammaryGlandRecoveryInjection },
        },
        Teachable = true,
        BaseChance = 5,
        ForbiddenStartingSpecies = new List<ERace> { ERace.Neko },
        GuaranteedTraits = new List<ETrait> { ETrait.Trait108 },
        // 战斗力统计加成 +60%；未堕落：100% 秒杀房间内战力低于自己的怪物（仅猎手营地）
        CombatPowerBonus = 0.60f,
        KillMode = EProfessionKillMode.RoomWeakMonsters,
        KillChance = 100,
    };

    public static readonly ProfessionData Blacksmith = new("Blacksmith", BlacksmithId, 1)
    {
        Jobs = new List<JobDescription>
        {
            new JobDescription { Name = JobNames.CraftingTools,  Value = 200f, ValueMax = 300f, Chance = 70,                        Type = EJobType.EarningMoney },
            new JobDescription { Name = JobNames.HelpingBarbara, Value = 5f,   ValueMax = 5f,   Chance = 20, FullyDepraved = true,  Type = EJobType.HelpingBarbara },
        },
        Teachable = true,
        BaseChance = 35,
    };


    public static readonly ProfessionData Cultist = new("Cultist", CultistId, 2)
    {
        Jobs = new List<JobDescription>
        {
            new JobDescription { Name = JobNames.PreachingCorruption, Value = 1f, ValueMax = 1f, Chance = 70, FullyDepraved = true, Type = EJobType.RemoveShield, ShieldRemoval = 80 },
        },
    };

    public static readonly ProfessionData Hunter = new("Hunter", HunterId, 2)
    {
        Jobs = new List<JobDescription>
        {
            Passive("Profession_Hunter_Passive"),
            KillInfo("Profession_Hunter_Kill"),
            new JobDescription { Name = JobNames.Hunting, Value = 1f, ValueMax = 1f, Chance = 35, FullyDepraved = true, Type = EJobType.Gathering, ItemReward = EItemType.WerewolfTail },
        },
        Teachable = true,
        BaseChance = 35,
        // 战斗力统计加成 +20%
        CombatPowerBonus = 0.20f,
        KillMode = EProfessionKillMode.SingleTarget,
        KillChance = 25,
    };

    public static readonly ProfessionData Artificer = new("Artificer", ArtificerId, 4)
    {
        Jobs = new List<JobDescription>
        {
            new JobDescription { Name = JobNames.CraftingArtifacts, Value = 800f, ValueMax = 1500f, Chance = 50,                       Type = EJobType.EarningMoney },
            new JobDescription { Name = JobNames.CraftingArtifacts, Value = 1f,   ValueMax = 1f,   Chance = 60, FullyDepraved = true, Type = EJobType.Gathering, ItemReward = EItemType.DawrfHeart },
        },
        Teachable = true,
        BaseChance = 10,
    };

    // ============================ 新增职业 ============================

    public static readonly ProfessionData Brigand = new("Brigand", BrigandId, 1)
    {
        Jobs = new List<JobDescription>
        {
            Passive("Profession_Brigand_Passive"),
            KillInfo("Profession_Brigand_Kill"),
        },
        MonsterInheritable = true,
        // 战斗力统计加成 +10%；营地 sex 次数 +1
        CombatPowerBonus = 0.10f,
        KillMode = EProfessionKillMode.SingleTarget,
        KillChance = 25,
        CampSexCountBonus = 1,
    };

    public static readonly ProfessionData Assassin = new("Assassin", AssassinId, 2)
    {
        Jobs = new List<JobDescription>
        {
            Passive("Profession_Assassin_Passive"),
            KillInfo("Profession_Assassin_Kill"),
            new JobDescription { Name = JobNames.PotionCrafting, Value = 1f, ValueMax = 1f, Chance = 40, Type = EJobType.CraftingPotion, ItemReward = EItemType.Item_Aphrodisiac },
        },
        MonsterInheritable = true,
        // 战斗力统计加成 +20%；未堕落：只要更强 → 100% 秒杀
        CombatPowerBonus = 0.20f,
        KillMode = EProfessionKillMode.SingleTarget,
        KillChance = 100,
    };

    public static readonly ProfessionData Thief = new("Thief", ThiefId, 2)
    {
        Jobs = new List<JobDescription>
        {
            Passive("Profession_Thief_Passive"),
            KillInfo("Profession_Thief_Kill"),
        },
        MonsterInheritable = true,
        // 战斗力统计加成 +20%；营地 sex 次数 +2
        CombatPowerBonus = 0.20f,
        KillMode = EProfessionKillMode.SingleTarget,
        KillChance = 25,
        CampSexCountBonus = 2,
    };

    public static readonly ProfessionData Marauder = new("Marauder", MarauderId, 3)
    {
        Jobs = new List<JobDescription>
        {
            Passive("Profession_Marauder_Passive"),
            KillInfo("Profession_Marauder_Kill"),
        },
        MonsterInheritable = true,
        // 战斗力统计加成 +40%；营地 sex 次数 +3
        CombatPowerBonus = 0.40f,
        KillMode = EProfessionKillMode.SingleTarget,
        KillChance = 25,
        CampSexCountBonus = 3,
    };

    public static readonly ProfessionData Plunderer = new("Plunderer", PlundererId, 4)
    {
        Jobs = new List<JobDescription>
        {
            Passive("Profession_Plunderer_Passive"),
            KillInfo("Profession_Plunderer_Kill"),
        },
        MonsterInheritable = true,
        // 战斗力统计加成 +60%；营地 sex 次数 +4
        CombatPowerBonus = 0.60f,
        KillMode = EProfessionKillMode.SingleTarget,
        KillChance = 25,
        CampSexCountBonus = 4,
    };


    public static readonly ProfessionData Apothecary = new("Apothecary", ApothecaryId, 3)
    {
        Jobs = new List<JobDescription>
        {
            new JobDescription { Name = JobNames.PotionCrafting, Value = 1f, ValueMax = 1f, Chance = 50,                        Type = EJobType.CraftingPotion, ItemReward = EItemType.HealthRecoveryInjection },
            new JobDescription { Name = JobNames.PotionCrafting, Value = 1f, ValueMax = 1f, Chance = 60, FullyDepraved = true,  Type = EJobType.CraftingPotion, ItemReward = EItemType.Item_ViOgra },
        },
    };

    public static readonly ProfessionData DarkPriest = new("DarkPriest", DarkPriestId, 4)
    {
        Jobs = new List<JobDescription>
        {
            KillInfo("Profession_DarkPriest_Kill"),
            new JobDescription { Name = JobNames.PotionCrafting, Value = 1f, ValueMax = 1f, Chance = 60,                        Type = EJobType.CraftingPotion, ItemReward = EItemType.Item_Aphrodisiac },
            new JobDescription { Name = JobNames.DarkRitual,     Value = 1f, ValueMax = 1f, Chance = 70, FullyDepraved = true,  Type = EJobType.RemoveShield,    ShieldRemoval = 300, ShieldRemovalTargetsMin = 2, ShieldRemovalTargetsMax = 3 },
        },
        // 未堕落：Sex 触发 / Sex 结束时，战斗力 > 怪物 → 25%；≥2 倍 → 100%
        KillMode = EProfessionKillMode.SingleTarget,
        KillChance = 25,
    };

    public static readonly ProfessionData Merchant = new("Merchant", MerchantId, 4)
    {
        Jobs = new List<JobDescription>
        {
            new JobDescription { Name = JobNames.Commerce, Value = 300f,  ValueMax = 500f,  Chance = 60,                        Type = EJobType.EarningMoney },
            new JobDescription { Name = JobNames.Commerce, Value = 5000f, ValueMax = 8000f, Chance = 30, FullyDepraved = true,  Type = EJobType.EarningMoney },
        },
    };

    public static readonly ProfessionData Alchemist = new("Alchemist", AlchemistId, 5)
    {
        Jobs = new List<JobDescription>
        {
            KillInfo("Profession_Alchemist_Kill"),
            new JobDescription { Name = JobNames.PotionCrafting, Value = 1f, ValueMax = 1f, Chance = 80,                        Type = EJobType.CraftingPotion, ItemReward = EItemType.Item_TraitUpgradePill },
            new JobDescription { Name = JobNames.CreateGolem,    Value = 1f, ValueMax = 1f, Chance = 20, FullyDepraved = true,  Type = EJobType.CreateGolem },
        },
        KillMode = EProfessionKillMode.SingleTarget,
        KillChance = 25,
    };

    public static readonly ProfessionData Hero = new("Hero", HeroId, 5)
    {
        Jobs = new List<JobDescription>
        {
            Passive("Profession_Hero_Passive"),
            KillInfo("Profession_Hero_Kill"),
            new JobDescription { Name = JobNames.AbilitySharing, Value = 1f, ValueMax = 1f, Chance = 80, FullyDepraved = true, Type = EJobType.CraftingPotion, ItemReward = EItemType.Item_TraitUpgradePill, ItemCountMin = 2, ItemCountMax = 4 },
        },
        // 战斗力统计加成 +80%；未堕落：100% 秒杀房间内战力低于自己的怪物（仅猎手营地）
        CombatPowerBonus = 0.80f,
        KillMode = EProfessionKillMode.RoomWeakMonsters,
        KillChance = 100,
    };

    public static readonly ProfessionData Trainer = new("Trainer", TrainerId, 5)
    {
        Jobs = new List<JobDescription>
        {
            Passive("Profession_Trainer_Passive"),
            new JobDescription { Name = JobNames.DepravedTraining, Value = 1f, ValueMax = 1f, Chance = 80, Type = EJobType.RemoveShield, ShieldRemoval = 400, ShieldRemovalTargetsMin = 1, ShieldRemovalTargetsMax = 3 },
        },
        MonsterInheritable = true,
        // 战斗力统计加成 +40%；怪物：性交速度 +0.09；营地 sex 次数 +4
        CombatPowerBonus = 0.40f,
        CampSexCountBonus = 4,
    };


    public static IReadOnlyList<ProfessionData> All { get; } = new[]
    {
        Princess, Maid, Farmhand, Priest, Wizard, Warrior, Knight,
        Witch, Paladin, Blacksmith, Cultist, Hunter, Artificer,
        Brigand, Assassin, Thief, Marauder, Plunderer,
        Apothecary, DarkPriest, Merchant, Alchemist, Hero, Trainer,
    };

    // ============================ 规则辅助 ============================

    /// <summary>商人（奴隶市场）出售时可生成的职业：暂时保留原版 13 种，不添加新职业。</summary>
    private static readonly HashSet<int> MarketAllowedProfessionIds = new HashSet<int>
    {
        PrincessId, MaidId, FarmhandId, PriestId, WizardId, WarriorId, KnightId,
        WitchId, PaladinId, BlacksmithId, CultistId, HunterId, ArtificerId,
    };

    public static bool IsMarketAllowedProfession(int id) => MarketAllowedProfessionIds.Contains(id);

    /// <summary>构造一行“被动特性”工作条目：只在职业工具提示的工作栏以小字展示，不参与每日工作执行。</summary>
    private static JobDescription Passive(string localizationKey)
        => new JobDescription { Name = localizationKey, Type = EJobType.PassiveEffect };

    /// <summary>
    /// 构造一行“斩杀效果”说明：FullyDepraved = false → 显示在职业提示的「未堕落」工作栏下
    /// （与「未堕落」工作实际生效的条件一致），同样不参与每日工作执行。
    /// </summary>
    private static JobDescription KillInfo(string localizationKey)
        => new JobDescription { Name = localizationKey, Type = EJobType.PassiveEffect, FullyDepraved = false };
}
