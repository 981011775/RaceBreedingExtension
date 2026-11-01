#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using ComplexBreedingRedux.Data.Species;
using ComplexBreedingRedux.Data;
using ComplexBreedingRedux.Data.Professions;
using ComplexBreedingRedux.Properties;
using ComplexBreedingRedux.Utilities;
using DepravedShield.Helpers;
using MBMScripts;
using RaceBreedingExtension.HunterCamp;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ComplexBreedingRedux.Features;

internal static class ProfessionSystem
{
    /// <summary>
    /// 检查角色是否能在教室中学习指定职业（移除精华条件，仅保留可传授性和基础几率）。
    /// </summary>
    public static bool LearnNewProfessionCheck(Female female, IProfessionData profession)
    {
        if (!profession.Teachable)
            return false;

        // 精华系统已删除，不再检查 RequiredEssences / ForbiddenEssences / 精华总和
        // 直接根据职业基础几率判定
        return Random.Range(1, 101) <= profession.BaseChance;
    }

    public static void Work(Character female, IProfessionData profession)
    {
        if (profession.Jobs == null || female.Health < female.MaxHealth * 0.75f)
            return;

        int roll = Random.Range(0, 101);
        int cumulative = 0;

        var jobs = profession.Jobs.Where(j => j.FullyDepraved == null).ToList();

        if (female.IsDepraved)
            jobs.AddRange(profession.Jobs.Where(j => j.FullyDepraved.HasValue && j.FullyDepraved.Value));
        else
            jobs.AddRange(profession.Jobs.Where(j => j.FullyDepraved.HasValue && !j.FullyDepraved.Value));

        // 被动特性行（猎手营地加成/公主加成等）只在工作栏展示，不参与每日工作执行
        jobs = jobs.Where(j => j.Type != EJobType.PassiveEffect).ToList();

        foreach (var job in jobs.OrderBy(j => j.Chance))
        {
            cumulative += job.Chance;
            if (roll >= cumulative)
                continue;

            // ★ 职业拓展：物品奖励工作（采集/狩猎/冒险/制作药剂/服务等）→ 向玩家库存发放物品
            if (job.ItemReward.HasValue)
            {
                GrantItemReward(female, job);
                return;
            }

            // ★ 职业拓展：消除奴隶角色防护结界(DepravedShield)的工作
            if (job.ShieldRemoval > 0)
            {
                RemoveSlaveShield(female, profession, job);
                return;
            }

            float effectiveMax = job.ValueMax >= job.Value ? job.ValueMax : job.Value;

            switch (job.Type)
            {
                case EJobType.RoomCleaning:
                    {
                        int count = Random.Range((int)job.Value, (int)effectiveMax + 1);
                        var rooms = GameManager.Instance.PlayerData
                            .GetUnitList(ESector.Room)
                            .Select(u => (Room)u)
                            .Where(r => r.IsInfected || r.SemenCount > 0 || r.CondomInformationSeqList.Count > 0)
                            .OrderByDescending(r => r.IsInfected)
                            .Take(count)
                            .ToList();
                        if (!rooms.Any())
                            return;
                        foreach (var room in rooms)
                        {
                            room.Clear();
                            room.PopUpMessage(string.Format(Loc("Work_CleanedRoom"), female.DisplayName, profession.Name), null, 0.25f);
                        }
                        female.PopUpMessage(LocalizationSystem.GetLocJobName(job.Name), null, 0.25f);
                        GameManagerUtils.AddLogMessage($"{Loc("Work_Prefix")} {female.DisplayName} -> {LocalizationSystem.GetLocJobName(job.Name)}");
                        return;
                    }
                case EJobType.EarningMoney:
                    {
                        int earned = Random.Range((int)job.Value, (int)effectiveMax + 1);
                        GameManager.Instance.PlayerData.Gold += earned;
                        string locName = LocalizationSystem.GetLocJobName(job.Name);
                        string msg = $"{locName}: +{earned}";
                        female.PopUpMessage(msg, "Object[resource_gold_128]", 0.25f);
                        GameManagerUtils.AddLogMessage($"{Loc("Work_Prefix")} {female.DisplayName} -> {msg}g");
                        return;
                    }
                case EJobType.Healing:
                    {
                        var target = GameManager.Instance.PlayerData
                            .GetUnitList(ESector.Female)
                            .Select(u => (Female)u)
                            .Where(f => (int)f.Health != (int)f.MaxHealth)
                            .OrderBy(f => f.Health)
                            .FirstOrDefault();
                        if (target != null)
                        {
                            int pct = Random.Range((int)job.Value, (int)effectiveMax + 1);
                            target.Health += (int)(target.MaxHealth * (pct / 100f));
                            female.PopUpMessage(LocalizationSystem.GetLocJobName(job.Name), null, 0.25f);
                            target.PopUpMessage(string.Format(Loc("Work_HealedSlave"), pct, female.DisplayName, profession.Name), null, 0.25f);
                            GameManagerUtils.AddLogMessage($"{Loc("Work_Prefix")} {female.DisplayName} -> {LocalizationSystem.GetLocJobName(job.Name)}");
                            return;
                        }
                        break;
                    }
                case EJobType.SummonPixy:
                    {
                        // ★ 只增加妖精数量，**不做任何出售标记刷新** —— 与游戏原版的 IncreasePixy（源码 65288，
                        //   函数体内只有 FloraPixyCount++）、以及「分解 / 取消妖精待机」加妖精（源码 67315）完全一致。
                        //
                        //   以前这里还调用过妖精出售标记的补发刷新（PixySoldSoonRefresh.AfterPixyCountIncreased）：
                        //   它会把雌 / 雄 / 婴儿扇区里所有「本设施出生长大」的单位重新写一遍 SoldSoon，
                        //   结果女巫一执行召唤妖精就发生一次全场重标记 —— 玩家手动「取消出售」的单位会被
                        //   重新标记，随后立刻被妖精从所在房间（含奴笼）搬去仓库卖掉。现已彻底移除。
                        //
                        //   现在出售标记只可能来自游戏原版出生流程（GiveBirth，源码 22121-22131：
                        //   SoldSoon = PixySell && FloraPixySell、MovedSoon = PixyBaby && FloraPixyBaby，
                        //   且要求当时 FloraPixyCount > 0），本 mod 只做**读取层拦截**：
                        //   婴儿期 / 搬进来的成体 / 营地俘虏 / 动作中（见 PixySoldSoonRefresh.IsProtectedFromPixySale）。
                        int count = Random.Range((int)job.Value, (int)effectiveMax + 1);
                        for (int i = 0; i < count; i++)
                        {
                            GameManager.Instance.PlayerData.FloraPixyCount++;
                            GameManager.Instance.PlayerData.AddAchievement(EAchievement.A000061, 1, true, false);
                            GameManager.Instance.PlayerData.AddLikeability(ELikeability.Flora_Pixy, 1);
                        }

                        string locPixy = LocalizationSystem.GetLocJobName(job.Name);
                        female.PopUpMessage($"{locPixy} (+{count})", null, 0.25f);
                        GameManagerUtils.AddLogMessage($"{Loc("Work_Prefix")} {female.DisplayName} -> {locPixy}");
                        return;
                    }
                case EJobType.HelpingNiel:
                    AddLikeability(female, "Niel_", job);
                    return;
                case EJobType.HelpingFlora:
                    AddLikeability(female, "Flora_", job);
                    return;
                case EJobType.HelpingBarbara:
                    AddLikeability(female, "Barbara_", job);
                    return;
                case EJobType.Corruption:
                    {
                        var victim = GameManager.Instance.PlayerData
                            .GetUnitList(ESector.Female)
                            .Select(u => (Female)u)
                            .FirstOrDefault(f => !f.IsDepraved);
                        if (victim != null)
                        {
                            int amount = Random.Range((int)job.Value, (int)effectiveMax + 1);
                            victim.Training += amount;
                            female.PopUpMessage(LocalizationSystem.GetLocJobName(job.Name), null, 0.25f);
                            victim.PopUpMessage(string.Format(Loc("Work_AddedDepravation"), amount, female.DisplayName, profession.Name), null, 0.25f);
                            GameManagerUtils.AddLogMessage($"{Loc("Work_Prefix")} {female.DisplayName} -> {LocalizationSystem.GetLocJobName(job.Name)}");
                            return;
                        }
                        break;
                    }
                case EJobType.Cleansing:
                    {
                        var target = GameManager.Instance.PlayerData
                            .GetUnitList(ESector.Female)
                            .Select(u => (Female)u)
                            .FirstOrDefault(f => !f.IsDepraved);
                        if (target != null)
                        {
                            int amount = Random.Range((int)job.Value, (int)effectiveMax + 1);
                            target.Training -= amount;
                            female.PopUpMessage(LocalizationSystem.GetLocJobName(job.Name), null, 0.25f);
                            target.PopUpMessage(string.Format(Loc("Work_RemovedDepravation"), amount, female.DisplayName, profession.Name), null, 0.25f);
                            GameManagerUtils.AddLogMessage($"{Loc("Work_Prefix")} {female.DisplayName} -> {LocalizationSystem.GetLocJobName(job.Name)}");
                            return;
                        }
                        break;
                    }
                case EJobType.CreateGolem:
                    {
                        if (GameManager.Instance.PlayerData.NewCharacter(
                                EUnitType.Human, ESector.Keeping, (0, 0), -1, false) is Female golem)
                        {
                            FemaleSpeciesSetup.SetSpecies(golem, SpeciesRegistry.Golem);
                            golem.HairColor = Color.gray;
                            golem.EyeColor = Color.gray;
                            // ★ 与猎手营地生成奴隶一致：随机职业（受派系声望影响）＋随机属性（Trait93~Trait100）
                            //   ＋按体格倍率设置最大生命（声望越高越好）；以“种族名 + 职业名”命名。
                            HunterCampSystem.ProducedProfile profile = HunterCampSystem.RollProducedProfile(SpeciesRegistry.Golem);
                            HunterCampSystem.ApplyProducedProfile(golem, SpeciesRegistry.Golem, profile);
                            golem.SortTrait();
                            golem.DisplayName = LocalizationSystem.ComposeSpeciesProfessionDisplayName(
                                SpeciesRegistry.Golem.Name, profile.Profession?.Name, golem.RaceName);


                            female.PopUpMessage(LocalizationSystem.GetLocJobName(job.Name), null, 0.25f);
                            GameManagerUtils.AddLogMessage(
                                $"{Loc("Work_Prefix")} {golem.DisplayName} -> {LocalizationSystem.GetLocJobName(job.Name)}");
                            return;
                        }
                        break;
                    }
            }
            break;
        }
    }

    /// <summary>物品奖励工作：把奖励物品放入玩家库存（支持固定数量或数量区间）。</summary>
    private static void GrantItemReward(Character worker, JobDescription job)
    {
        if (job.ItemReward == null)
            return;

        EItemType item = job.ItemReward.Value;
        int count = GetItemRewardCount(job);
        GameManager.Instance.PlayerData.NewItem(
            item, ESector.Inventory, new ValueTuple<int, int>(GetInventoryGroup(item), 0), -1, count, true);

        string locJob = LocalizationSystem.GetLocJobName(job.Name);
        string itemName = LocalizationSystem.GetLocItemName(item);
        worker.PopUpMessage($"{locJob}: +{count} {itemName}", null, 0.25f);
        GameManagerUtils.AddLogMessage($"{Loc("Work_Prefix")} {worker.DisplayName} -> {locJob} ({itemName} x{count})");
    }

    /// <summary>
    /// 物品奖励数量：优先使用 ItemCountMin~ItemCountMax 区间；
    /// 其次沿用旧的 Value~ValueMax 区间写法；否则使用固定 ItemCount。
    /// </summary>
    public static int GetItemRewardCount(JobDescription job)
    {
        if (job.ItemCountMax > 0 && job.ItemCountMin > 0 && job.ItemCountMax >= job.ItemCountMin)
            return Random.Range(job.ItemCountMin, job.ItemCountMax + 1);

        if (job.ValueMax > job.Value && job.Value >= 1f)
            return Random.Range((int)job.Value, (int)job.ValueMax + 1);

        return Math.Max(1, job.ItemCount);
    }

    /// <summary>防护结界消减工作的目标数量（默认 1 名）。</summary>
    public static int GetShieldRemovalTargetCount(JobDescription job)
    {
        if (job.ShieldRemovalTargetsMax > 0 && job.ShieldRemovalTargetsMax >= Math.Max(1, job.ShieldRemovalTargetsMin))
            return Random.Range(Math.Max(1, job.ShieldRemovalTargetsMin), job.ShieldRemovalTargetsMax + 1);
        return 1;
    }

    /// <summary>
    /// 原版库存分栏（ESector.Inventory 的槽位 X 分量）：
    /// X=0 → 副产物/材料栏（体液、哥布林精液、器官、DNA 等）；
    /// X=1 → 消耗品栏（注射剂、药丸、牛奶等）。
    /// 与商店/原版发放保持一致，才能与原版同类消耗品正常合并堆叠。
    /// </summary>
    private static int GetInventoryGroup(EItemType item)
    {
        switch (item)
        {
            case EItemType.HealthRecoveryInjection:
            case EItemType.MammaryGlandRecoveryInjection:
            case EItemType.OvumRecoveryInjection:
            case EItemType.Item_Aphrodisiac:
            case EItemType.Item_ViOgra:
            case EItemType.Item_TraitUpgradePill:
                return 1;
            default:
                return 0;
        }
    }

    /// <summary>防护结界消减工作：随机选择指定数量（默认 1 名）拥有防护结界(DepravedShield)的奴隶角色并扣除指定数值。</summary>
    private static void RemoveSlaveShield(Character worker, IProfessionData profession, JobDescription job)
    {
        var candidates = GameManager.Instance.PlayerData
            .GetUnitList(ESector.Female)
            .Select(u => (Female)u)
            .Where(f => SexProgressManager.GetDepravedShield(f) > 0)
            .ToList();
        if (candidates.Count == 0)
            return;

        int targets = Math.Min(GetShieldRemovalTargetCount(job), candidates.Count);
        string locJob = LocalizationSystem.GetLocJobName(job.Name);
        worker.PopUpMessage(locJob, null, 0.25f);

        for (int i = 0; i < targets; i++)
        {
            Female victim = candidates[Random.Range(0, candidates.Count)];
            candidates.Remove(victim);
            SexProgressManager.ReduceDepravedShield(victim, job.ShieldRemoval);
            victim.PopUpMessage(string.Format(Loc("Work_RemovedShield"), job.ShieldRemoval, worker.DisplayName, profession.Name), null, 0.25f);
        }

        GameManagerUtils.AddLogMessage($"{Loc("Work_Prefix")} {worker.DisplayName} -> {locJob} x{targets}");
    }

    /// <summary>
    /// 商人（奴隶市场）出售的奴隶只保留原版 13 种职业；
    /// 猎手营地可生成所有职业，不在此限制范围内。
    /// </summary>
    public static void EnforceMarketProfessions(Female marketFemale)
    {
        if (marketFemale == null)
            return;
        if (!GameData.TryGetProfession(marketFemale, out IProfessionData? current) || current == null)
            return;
        if (ProfessionRegistry.IsMarketAllowedProfession(current.Id))
            return;

        List<IProfessionData> pool = GameData.ProfessionDataList
            .Where(x => ProfessionRegistry.IsMarketAllowedProfession(x.Id)
                        && x.Tier == current.Tier
                        && (x.ForbiddenStartingSpecies == null || !x.ForbiddenStartingSpecies.Contains(marketFemale.Race)))
            .ToList();
        if (pool.Count == 0)
        {
            pool = GameData.ProfessionDataList
                .Where(x => ProfessionRegistry.IsMarketAllowedProfession(x.Id)
                            && (x.ForbiddenStartingSpecies == null || !x.ForbiddenStartingSpecies.Contains(marketFemale.Race)))
                .ToList();
        }
        if (pool.Count == 0)
            return;

        IProfessionData reroll = pool[Random.Range(0, pool.Count)];
        marketFemale.SetTraitValue(GameBalance.ProfessionTraitId, reroll.Id);
        if (reroll.GuaranteedTraits != null)
        {
            foreach (ETrait trait in reroll.GuaranteedTraits)
            {
                if (!marketFemale.TraitContains(trait))
                    marketFemale.AddRaceTrait(trait);
            }
        }
        marketFemale.SortTrait();
    }

    private static void AddLikeability(Character female, string prefix, JobDescription job)
    {
        var available = Enum.GetNames(typeof(ELikeability))
            .Where(n => n.Contains(prefix))
            .Select(n => (ELikeability)Enum.Parse(typeof(ELikeability), n))
            .Where(l => !GameManager.Instance.PlayerData.GetLikeability(l).Item2)
            .ToList();

        if (!available.Any())
            return;

        float effectiveMax = job.ValueMax >= job.Value ? job.ValueMax : job.Value;
        int amount = Random.Range((int)job.Value, (int)effectiveMax + 1);
        var chosen = available[Random.Range(0, available.Count)];
        GameManager.Instance.PlayerData.AddLikeability(chosen, Math.Max(1, amount));
        string locJob = LocalizationSystem.GetLocJobName(job.Name);
        female.PopUpMessage($"{locJob} (+{amount})", null, 0.25f);
        GameManagerUtils.AddLogMessage($"{Loc("Work_Prefix")} {female.DisplayName} -> {locJob}");
    }

    private static string Loc(string key) => Strings.ResourceManager.GetString(key, Strings.Culture) ?? key;
}