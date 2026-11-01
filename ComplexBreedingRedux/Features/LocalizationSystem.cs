#nullable enable
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using ComplexBreedingRedux.Data;
using ComplexBreedingRedux.Data.Factions;
using ComplexBreedingRedux.Data.Professions;
using ComplexBreedingRedux.Data.Species;
using ComplexBreedingRedux.Data.SpeciesCore;
using ComplexBreedingRedux.Properties;
using MBMScripts;
using UnityEngine;

namespace ComplexBreedingRedux.Features;

internal static class LocalizationSystem
{
    // ─── Caches ──────────────────────────────────────────────────────────────
    private static readonly ConcurrentDictionary<string, string> _raceCache = new();

    /// <summary>切换语言时使缓存失效。</summary>
    public static void InvalidateCaches()
    {
        _raceCache.Clear();
        ReskinSystem.ClearCache();   // 新增：清理换皮缓存
    }

    // ─── SeqLocalization.Localize ────────────────────────────────────────────
    public static void ApplyLocalize(string text, ref string result)
    {
        if (string.IsNullOrEmpty(text))
            return;

        // ★ 原版文本替换（特质名称/描述、物品说明等，见 GameTextOverrides）
        if (GameTextOverrides.TryGet(text, out string replacedText))
        {
            result = replacedText;
            return;
        }

        // 处理职业名称和描述
        if (text.Contains("#Profession_"))
        {
            int id = int.Parse(text.Split('_')[1]);
            IProfessionData? prof = GameData.ProfessionDataList.FirstOrDefault(x => x.Id == id);
            if (prof != null)
                result = "<color=#0c4855><b>" + Loc("Label_Profession") + "</b> " + prof.Name + "</color>";
            return;
        }
        if (text.Contains("#ProfessionText_"))
        {
            int id = int.Parse(text.Split('_')[1]);
            IProfessionData? prof = GameData.ProfessionDataList.FirstOrDefault(x => x.Id == id);
            if (prof != null)
                result = GetProfessionDescription(prof);
            return;
        }

        // ★ 血脉（Bloodline）：名称 / 描述（界面上作为“种族特质”显示）
        if (text.Contains("#BloodlineText_"))
        {
            int value = int.Parse(text.Split('_')[1]);
            string bloodlineText = RaceBreedingExtension.Bloodline.BloodlineSystem.GetDescriptionByValue(value);
            if (!string.IsNullOrEmpty(bloodlineText))
                result = bloodlineText;
            return;
        }
        if (text.Contains("#Bloodline_"))
        {
            int value = int.Parse(text.Split('_')[1]);
            string bloodlineName = RaceBreedingExtension.Bloodline.BloodlineSystem.GetDisplayNameByValue(value);
            if (!string.IsNullOrEmpty(bloodlineName))
                result = bloodlineName;
            return;
        }
        // ★ 特质段落标题（查看角色时显示）
        if (text == "#TraitTitle")
        {
            result = Loc("Label_BloodlineTitle");
            return;
        }
        // 其他所有 # 键均交给原游戏处理，不再干涉
        // 特别注意：已删除 #RaceTraitName_* 和 #RaceTraitText_* 的处理

        // ★ 萝莉母乳（LoliMilk）物品说明：与其它母乳一致（含 15% 触发效果说明）
        if (text == "#LoliMilkText")
        {
            result = Loc("ItemText_LoliMilk");
            return;
        }
        // 保留一些通用UI字符串
        if (text == "#CountIsZero") result = Loc("Msg_CountIsZero");
    }

    // ─── Profession Description ──────────────────────────────────────────────
    public static string GetProfessionDescription(IProfessionData prof)
    {
        string text = prof.Description ?? string.Empty;
        text += string.Format("<size={0}>", 11);
        text += "<BR><color=#FFD700>" + Loc("Tooltip_Characteristics") + "</color>";
        text += "<BR>" + Loc("Tooltip_Tier") + " <color=" + GameColors.TierColors[prof.Tier] + ">" + GetLocTierName(prof.Tier) + "</color>";

        var always = prof.Jobs?.Where(x => x.FullyDepraved == null).ToList();
        var depraved = prof.Jobs?.Where(x => x.FullyDepraved != null && x.FullyDepraved.Value).ToList();
        var notDep = prof.Jobs?.Where(x => x.FullyDepraved != null && !x.FullyDepraved.Value).ToList();
        text = WorkDescription(always, text, Loc("Tooltip_WorkAlways"));
        text = WorkDescription(depraved, text, Loc("Tooltip_WorkDepraved"));
        text = WorkDescription(notDep, text, Loc("Tooltip_WorkNotDepraved"));

        // 已删除可传授和教室概率显示
        // text += "<BR><BR>" + Loc("Tooltip_Teachable") + " " + (prof.Teachable ? Loc("Tooltip_Yes") : Loc("Tooltip_No"));
        // if (prof.Teachable)
        // {
        //     text += "<BR><color=#9e97a3>" + Loc("Tooltip_Probability") + ":";
        //     text += string.Format("<BR>" + Loc("Tooltip_DailyInClassroom") + " <color={0}>{1}%</color>",
        //         prof.BaseChance > 0 ? GameColors.GoodColorHex : GameColors.BadColorHex, prof.BaseChance);
        //     text += "</color>";
        // }
        return text + "</size>";
    }

    private static string WorkDescription(ICollection<JobDescription>? jobs, string text, string? suffix = null)
    {
        if (jobs == null || !jobs.Any()) return text;
        text += "<BR><BR><color=#FFD700>" + Loc("Tooltip_Work") + (string.IsNullOrWhiteSpace(suffix) ? string.Empty : " (" + suffix + ")") + ":</color>";
        foreach (JobDescription job in jobs.OrderByDescending(x => x.Chance))
        {
            // 被动特性（猎手营地加成/公主加成等）：以小字显示在工作栏，不参与每日工作
            if (job.Type == EJobType.PassiveEffect)
            {
                if (!string.IsNullOrWhiteSpace(job.Name))
                    text += "<BR>" + Loc(job.Name);
                continue;
            }

            string r = (job.ValueMax == 0f || (int)job.ValueMax == (int)job.Value)
                ? string.Format("{0}", job.Value)
                : string.Format("{0}-{1}", job.Value, job.ValueMax);
            string d = string.Empty;
            if (job.ItemReward.HasValue)
            {
                string itemName = GetLocItemName(job.ItemReward.Value);
                int countMin, countMax;
                if (job.ItemCountMax > 0 && job.ItemCountMin > 0 && job.ItemCountMax >= job.ItemCountMin)
                {
                    countMin = job.ItemCountMin;
                    countMax = job.ItemCountMax;
                }
                else
                {
                    countMin = (int)job.Value;
                    countMax = (int)job.ValueMax;
                }
                d = (countMax > countMin)
                    ? string.Format(Loc("Job_ObtainItemRange"), countMin, countMax, itemName)
                    : string.Format(Loc("Job_ObtainItem"), itemName);
            }
            else if (job.ShieldRemoval > 0)
            {
                int targetMin = Mathf.Max(1, job.ShieldRemovalTargetsMin);
                int targetMax = job.ShieldRemovalTargetsMax >= targetMin ? job.ShieldRemovalTargetsMax : targetMin;
                d = (targetMax > targetMin)
                    ? string.Format(Loc("Job_RemoveShieldRange"), job.ShieldRemoval, targetMin, targetMax)
                    : string.Format(Loc("Job_RemoveShield"), job.ShieldRemoval);
            }
            else switch (job.Type)
            {
                case EJobType.RoomCleaning: d = string.Format(Loc("Job_RoomCleaning"), r); break;
                case EJobType.EarningMoney: d = string.Format(Loc("Job_EarningMoney"), job.Value, job.ValueMax); break;
                case EJobType.Healing: d = string.Format(Loc("Job_Healing"), job.Value, job.ValueMax); break;
                case EJobType.SummonPixy: d = string.Format(Loc("Job_SummonPixy"), r); break;
                case EJobType.HelpingNiel: d = string.Format(Loc("Job_HelpingNiel"), r); break;
                case EJobType.HelpingFlora: d = string.Format(Loc("Job_HelpingFlora"), r); break;
                case EJobType.HelpingBarbara: d = string.Format(Loc("Job_HelpingBarbara"), r); break;
                case EJobType.Corruption: d = string.Format(Loc("Job_Corruption"), r); break;
                case EJobType.Cleansing: d = string.Format(Loc("Job_Cleansing"), r); break;
                case EJobType.CreateGolem: d = Loc("Job_CreateGolem"); break;
                default: d = string.Empty; break;
            }
            text += string.Format("<BR>{0}%: {1} ->{2}", job.Chance, GetLocJobName(job.Name), d);
        }
        return text;
    }

    // ─── Race String (ReferenceCharacterLook) ────────────────────────────────
    public static void ApplyRaceString(Character character, int dataType, ref string result)
    {
        if (dataType != 51) return;

        // 访客（Client）：UI 不再显示种族，改为“颜色 + 派系 + 此派系声望 + 客人品阶 + 偏好”
        if (character is Client client)
        {
            result = GetClientFactionDescription(client);
            return;
        }

        if (GameData.TryGetSpecies(character, out ISpeciesData? species) && species != null)
        {
            if (_raceCache.TryGetValue(species.Name, out string? cached)) { result = cached; return; }
            List<ISpeciesData> children = GameData.SpeciesDataList
                .Where(x => x.InternalName != species.InternalName && x.PossiblyMothers != null && x.PossiblyMothers.Any(y => y == species.InternalName))
                .OrderByDescending(x => x.Tier).ThenBy(x => x.Name).ToList();
            bool ancestors = species.BaseMother != null || (species.PossiblyMothers ?? System.Array.Empty<string>()).Any();
            List<ERace> fathers = species.PossiblyFathers?.ToList() ?? new List<ERace>();
            fathers.Add(ERace.Human);
            string desc = GetRaceDescription(species.Tier, fathers, species.HealthRegeneration, species.MilkProductionRate, species.MaxMilkSizeChange, children, ancestors);
            _raceCache.TryAdd(species.Name, desc);
            result = desc;
            return;
        }

        if (GameData.TryGetSpeciesCore(character, out ISpeciesCoreData? core) && core != null)
        {
            if (_raceCache.TryGetValue(core.Name, out string? cached)) { result = cached; return; }
            List<ISpeciesData> children2 = GameData.SpeciesDataList
                .Where(x => x.BaseMother != null && x.BaseMother == core.GetReplacedType)
                .OrderByDescending(x => x.Tier).ThenBy(x => x.Name).ToList();
            string desc2 = GetRaceDescription(1, new List<ERace> { ERace.Human }, core.HealthRegeneration, core.MilkProductionRate, core.MaxMilkSizeChange, children2, false);
            _raceCache.TryAdd(core.Name, desc2);
            result = desc2;
        }
    }

    // ─── 访客（派系访客）UI 描述：颜色 / 派系 / 派系声望 / 品阶 / 偏好 ───

    /// <summary>
    /// 访客信息 UI（原本用于“种族”说明的行）。显示：派系（带派系颜色）、该派系声望、
    /// 访客品阶（与职业品阶同步的 1~5 阶）、访客偏好（原游戏 EPreferenceType）。
    /// </summary>
    public static string GetClientFactionDescription(Client client)
    {
        string text = string.Format("<size={0}>", 16);
        if (!GameData.TryGetFaction(client.DataType, out FactionData? faction) || faction == null)
            return text + "</size>";

        int tier = FactionSystem.GetVisitorTier(client);
        int score = FactionSystem.GetReputation(faction.Id);
        EFactionReputation rep = FactionSystem.GetReputationTier(score);

        // 1) 颜色 + 派系名
        text += "<color=" + faction.ColorHex + "><b>" + faction.Name + "</b></color>";
        // 2) ★ 血脉（Bloodline）：显示在「派系」下面、「声望」上面，样式与奴隶一致（彩色名称 + 等级）
        string clientBloodline = RaceBreedingExtension.Bloodline.BloodlineSystem.GetDisplayName(client);
        if (!string.IsNullOrEmpty(clientBloodline))
            text += "<BR>" + clientBloodline;
        // 3) 此派系声望（等级 + 数值，数值始终显示）
        //    ★ 声望等级的颜色直接写在 Strings 资源文本中（FactionRep_*，见 Strings.resx / Strings.zh-cn.resx）：
        //      由高到低依次对应「传说 / 史诗 / 稀有 / 不凡 / 普通」的品阶颜色，再低为白色，最低为灰色。
        text += "<BR>" + Loc("Tooltip_Reputation") + " " + GetLocReputationName(rep);
        text += string.Format(" ({0})", score);
        // 4) 客人品阶（与职业品阶同步，1~5）
        text += "<BR>" + Loc("Tooltip_Tier") + " <color=" + GameColors.TierColors[tier] + ">"
            + GetLocTierName(tier) + "</color>";
        // 5) 偏好（原游戏偏好系统）；标签自带冒号（英文资源自带尾随空格），不再额外加空格
        text += "<BR>" + Loc("Label_Preference") + GetLocPreferenceType(client.PreferenceType);
        return text + "</size>";
    }

    private static string GetLocReputationName(EFactionReputation rep) => rep switch
    {
        EFactionReputation.Unknown => Loc("FactionRep_Unknown"),
        EFactionReputation.Obscure => Loc("FactionRep_Obscure"),
        EFactionReputation.Insignificant => Loc("FactionRep_Insignificant"),
        EFactionReputation.Noteworthy => Loc("FactionRep_Noteworthy"),
        EFactionReputation.Reputable => Loc("FactionRep_Reputable"),
        EFactionReputation.Significant => Loc("FactionRep_Significant"),
        EFactionReputation.Famous => Loc("FactionRep_Famous"),
        EFactionReputation.Fabled => Loc("FactionRep_Fabled"),
        _ => rep.ToString(),
    };

    private static string GetLocPreferenceType(EPreferenceType type) => type switch
    {
        EPreferenceType.Pussy => Loc("PrefType_Pussy"),
        EPreferenceType.Fellatio => Loc("PrefType_Fellatio"),
        EPreferenceType.Anal => Loc("PrefType_Anal"),
        EPreferenceType.Paizuri => Loc("PrefType_Paizuri"),
        EPreferenceType.Bestiality => Loc("PrefType_Bestiality"),
        EPreferenceType.Birth => Loc("PrefType_Birth"),
        EPreferenceType.Vip => Loc("PrefType_Vip"),
        EPreferenceType.Vvip => Loc("PrefType_Vvip"),
        _ => type.ToString(),
    };

    private static string GetRaceDescription(int tier, ICollection<ERace> males, float hpRegen, float milkRate, float milkSizeChange, ICollection<ISpeciesData> speciesList, bool ancestorsExists)
    {
        string text = string.Empty;
        text += string.Format("<size={0}>", 11);
        text += "<color=#FFD700>" + Loc("Tooltip_Characteristics") + "</color>";
        text += "<BR>" + Loc("Tooltip_Tier") + " <color=" + GameColors.TierColors[tier] + ">" + GetLocTierName(tier) + "</color>";
        text += string.Format("<BR>" + Loc("Tooltip_HealthRegen") + " {0}/s", hpRegen);
        text += string.Format("<BR>" + Loc("Tooltip_MilkRate") + " {0}/s", milkRate);
        if (milkSizeChange > 1f || milkSizeChange < 1f)
        {
            string col = milkSizeChange > 1f ? GameColors.GoodColorHex : GameColors.BadColorHex;
            float pct = (1f - milkSizeChange) * -1f;
            text += string.Format("<BR>" + Loc("Tooltip_BreastsCapacity") + " <color={0}>{1}{2}%</color>", col, pct > 0f ? "+" : string.Empty, pct * 100f);
        }
        text += "<BR>" + Loc("Tooltip_OwnSpeciesBirth") + " " + string.Join(", ", males.Distinct().Select(x => GetLocRaceName(x).ToLower()));
        // ★ 降级几率提示：品阶 1 也带降级几率（可降级到基础种族），所以不再写死 tier >= 2；
        //   但「基础种族自身」的说明（ancestorsExists = false 且品阶被强制为 1）不显示这一行。
        int lowerBirthChance = GameBalance.TierLowerEvoBirths.TryGetValue(tier, out int lowerChance) ? lowerChance : 0;
        if (lowerBirthChance > 0 && (ancestorsExists || tier >= 2))
        {
            text += ancestorsExists
                ? string.Format("<BR>" + Loc("Tooltip_LowerSpeciesChanceBirth") + " {0}%", lowerBirthChance)
                : string.Format("<BR>" + Loc("Tooltip_ConceptionFailureChance") + " {0}%", lowerBirthChance);
        }
        if (speciesList.Any())
        {
            text += "<BR><BR><color=#FFD700>" + Loc("Tooltip_PossibleSpecies") + "</color>";
            foreach (ISpeciesData sd in speciesList)
            {
                if (text.Contains("base")) text += "<BR><color=#A567D1>--------</color>";
                text += "<BR>" + sd.Name + " <color=" + GameColors.TierColors[sd.Tier] + ">" + GetLocTierName(sd.Tier) + "</color>";
                ERace[]? f = sd.PossiblyFathers;
                IEnumerable<string> fn = f != null ? f.OrderBy(x => x.ToString()).Select(x => GetLocRaceName(x).ToLower()) : System.Array.Empty<string>();
                text += "<BR>" + Loc("Tooltip_Males") + " " + string.Join(", ", fn);
                text += "<BR><color=#9e97a3>" + Loc("Tooltip_Probability") + ":";
                text += string.Format("<BR>" + Loc("Tooltip_Base") + " <color={0}>{1}%</color>", sd.BaseChance > 0 ? GameColors.GoodColorHex : GameColors.BadColorHex, sd.BaseChance);
                text += "</color>";
            }
        }
        text += "</size>";
        return text;
    }

    // ─── Sprite ──────────────────────────────────────────────────────────────
    public static void ApplyRaceSprite(Character character, ref Sprite result)
    {
        EFigure fig = character.Figure;
        if (fig != EFigure.Girl && fig != EFigure.Woman) return;
        SeqAddressables sa = SeqAddressables.Instance;
        if (character.IsChild || fig == EFigure.Girl)
        { result = sa.LoadAssetAsync<Sprite>("StandingImage[none_0]", null); return; }
        if (character.TitsType <= 1)
        { result = sa.LoadAssetAsync<Sprite>("StandingImage[none_1]", null); return; }
        result = character.TitsType == 2
            ? sa.LoadAssetAsync<Sprite>("StandingImage[none_2]", null)
            : sa.LoadAssetAsync<Sprite>("StandingImage[none_3]", null);
    }

    // ─── Unit String（角色详情UI）─────────────────────────────────────────────
    public static void ApplyUnitString(Character character, int dataType, ref string result)
    {
        switch (dataType)
        {
            case 22: // 普通特质（职业 / 血脉 / 其它）
                {
                    ETrait trait = character.GetTrait(Updater.Index);
                    // ★ 血脉（Bloodline）：Trait9 槽位在界面显示为“种族特质”（名称 + 等级）
                    if (trait == RaceBreedingExtension.Bloodline.BloodlineSystem.SlotTrait)
                    {
                        string bloodlineName = RaceBreedingExtension.Bloodline.BloodlineSystem.GetDisplayName(character);
                        if (!string.IsNullOrEmpty(bloodlineName))
                        {
                            result = bloodlineName;
                            return;
                        }
                    }
                    // ★ 原版特质名称替换（Trait71 超凡魅力 / Trait108 疾病免疫，见 GameTextOverrides）
                    string? overriddenName = GameTextOverrides.GetTraitName(trait);
                    if (!string.IsNullOrEmpty(overriddenName))
                    {
                        result = overriddenName!;
                        return;
                    }
                    // 只有职业才由我们处理，其他特质完全交由原游戏
                    if (trait == (ETrait)10)
                    {
                        result = SeqLocalization.Localize(string.Format("#Profession_{0}", (int)character.GetTraitValue(trait)));
                    }
                    // 注意：非职业特质我们不做任何修改，让result保持原样
                    return;
                }
            case 23: // 特质描述
                {
                    ETrait trait2 = character.GetTrait(Updater.Index);
                    // ★ 血脉（Bloodline）描述
                    if (trait2 == RaceBreedingExtension.Bloodline.BloodlineSystem.SlotTrait)
                    {
                        string bloodlineDesc = RaceBreedingExtension.Bloodline.BloodlineSystem.GetDescription(character);
                        if (!string.IsNullOrEmpty(bloodlineDesc))
                        {
                            result = bloodlineDesc;
                            return;
                        }
                    }
                    // ★ 原版特质描述替换（Trait71 超凡魅力 / Trait108 疾病免疫）
                    string? overriddenTooltip = GameTextOverrides.GetTraitTooltip(trait2);
                    if (!string.IsNullOrEmpty(overriddenTooltip))
                    {
                        result = overriddenTooltip!;
                        return;
                    }
                    if (trait2 == (ETrait)10)
                    {
                        result = SeqLocalization.Localize(string.Format("#ProfessionText_{0}", (int)character.GetTraitValue(trait2)));
                    }
                    return;
                }
            case 27: // 角色状态详情（我们自定义的UI）
                {
                    if (character is SenaLena) { result = "test"; return; }
                    if (character is Female female) { result = GetStateDescription(female); return; }
                    // ★ 怪物（雄性）：不再只显示原版“价格率/价格”，改为与奴隶同版式
                    if (character is Male male) { result = GetMonsterStateDescription(male); return; }
                    return;
                }
            default:
                // 其他所有情况（包括 RaceTrait 名称/描述）都不干预，保持原游戏显示
                return;
        }
    }

    private static string GetStateDescription(Female female)
    {
        GameData.TryGetSpecies(female, out ISpeciesData? species);
        int tier = species != null ? species.Tier : 1;
        string text = "<size=27><size=32><color=" + GameColors.TierColors[tier] + ">";
        string displayName = female.DisplayName.StartsWith("#")
            ? SeqLocalization.Localize(female.DisplayName)
            : female.DisplayName;
        text += displayName == female.RaceName
            ? female.RaceName
            : displayName + " (" + female.RaceName + ")";
        if (female.IsChild) text += " [" + Loc("State_Young") + "]";
        text += "</color></size>";

        // 职业
        if (GameData.TryGetProfession(female, out IProfessionData? prof) && prof != null)
            text += "<BR><color=" + GameColors.TierColors[prof.Tier] + ">" + prof.Name + "</color>";

        // ★ 血脉（Bloodline）：紧随职业之后，与职业同样式（仅「彩色名称+等级」一行，无标签、无进度条）
        string bloodlineName = RaceBreedingExtension.Bloodline.BloodlineSystem.GetDisplayName(female);
        if (!string.IsNullOrEmpty(bloodlineName))
            text += "<BR>" + bloodlineName;

        // 价格
        text += string.Format("<BR><color={0}>" + Loc("State_SlavePrice") + "</color> {1}g", GameColors.BoldColorHex, female.FinalPrice);

        // ★ 战斗力（种族稀有度 × 职业稀有度 × 血脉 × 职业加成）
        text += string.Format("<BR><color={0}>" + Loc("State_CombatPower") + "</color> {1}",
            GameColors.BoldColorHex, RaceBreedingExtension.CombatPower.CombatPowerSystem.GetCombatPower(female));

        // 胸部
        text += string.Format("<BR><color={0}>" + Loc("State_Breasts") + "</color> {1}",
            GameColors.BoldColorHex, GetLocBreastSize(female.TitsType));

        // 子宫大小 + 拉伸进度
        text += string.Format("<BR><color={0}>" + Loc("State_Womb") + "</color> {1}",
            GameColors.BoldColorHex, GetLocWombSize(WombSystem.GetWombSize(female)));
        text += FormatWombStretchProgress(female);

        return text + "</size>";
    }

    /// <summary>
    /// 怪物（雄性）状态详情 UI：与奴隶同版式的 名称 / 职业 / 血脉 / 怪物价格 / 价格率 / 战斗力。
    /// 名称不再附带「(种族名)」括号内容（若名称中含括号及其内容一律删除）。
    /// </summary>
    private static string GetMonsterStateDescription(Character monster)
    {
        int raceTier = RaceBreedingExtension.CombatPower.CombatPowerSystem.GetMonsterRaceTier(monster.Race);
        string text = "<size=27><size=32><color=" + GameColors.TierColors[Mathf.Clamp(raceTier, 1, 5)] + ">";

        string displayName = monster.DisplayName ?? string.Empty;
        if (displayName.StartsWith("#"))
            displayName = SeqLocalization.Localize(displayName);
        displayName = StripParentheses(displayName);
        if (string.IsNullOrWhiteSpace(displayName))
            displayName = StripParentheses(SeqLocalization.Localize(monster.RaceName ?? string.Empty));
        text += displayName;
        if (monster.IsBaby) text += " [" + Loc("State_Young") + "]";
        text += "</color></size>";

        // 职业
        if (GameData.TryGetProfession(monster, out IProfessionData? profession) && profession != null)
            text += "<BR><color=" + GameColors.TierColors[profession.Tier] + ">" + profession.Name + "</color>";

        // 血脉（与奴隶一致：彩色名称 + 等级）
        string bloodline = RaceBreedingExtension.Bloodline.BloodlineSystem.GetDisplayName(monster);
        if (!string.IsNullOrEmpty(bloodline))
            text += "<BR>" + bloodline;

        // 怪物价格
        text += string.Format("<BR><color={0}>" + Loc("State_MonsterPrice") + "</color> {1}g",
            GameColors.BoldColorHex, monster.FinalPrice);

        // 价格率
        text += string.Format("<BR><color={0}>" + Loc("State_PriceRate") + "</color> {1}%",
            GameColors.BoldColorHex, Mathf.RoundToInt(monster.MarketRate * 100f));

        // 战斗力
        text += string.Format("<BR><color={0}>" + Loc("State_CombatPower") + "</color> {1}",
            GameColors.BoldColorHex, RaceBreedingExtension.CombatPower.CombatPowerSystem.GetCombatPower(monster));

        return text + "</size>";
    }

    private static string FormatWombStretchProgress(Female female)
    {
        if ((int)WombSystem.GetWombSize(female) >= 5) return string.Empty;
        float progress = WombSystem.GetStretchProgress(female);
        if (progress <= 0f) return string.Empty;
        int pct = (int)(progress * 100f);
        int filled = (int)(progress * 10f);
        string bar = "<color=#66CC22>" + new string('█', filled) + "</color>"
                   + "<color=#333>" + new string('█', 10 - filled) + "</color>";
        return "<BR>  " + GreyLabel(Loc("State_WombStretch"))
             + "<BR>    " + bar + " " + pct + "%";
    }

    private static string GreyLabel(string text) => "<color=#888>" + text + "</color>";

    /// <summary>删除文本中的半角/全角括号及其内容（例如 “名称 (#XXXXX)” → “名称”）。</summary>
    private static string StripParentheses(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;
        string result = System.Text.RegularExpressions.Regex.Replace(text!, @"[（(][^）)]*[）)]", string.Empty);
        return result.Trim();
    }

    // ─── Refresh Localized Strings ───────────────────────────────────────────
    public static void RefreshLocalizedStrings()
    {
        // 只更新职业和种族名称，特质不再需要
        foreach (IProfessionData pr in GameData.ProfessionDataList)
        {
            string key = "Profession_" + pr.InternalName;
            pr.Name = Loc(key + "_Name");
            pr.Description = Loc(key + "_Desc");
        }

        foreach (ISpeciesData s in GameData.SpeciesDataList)
        {
            s.Name = Loc("Species_" + s.InternalName + "_Name");
        }
        foreach (ISpeciesCoreData sc in GameData.SpeciesCoreDataList)
        {
            sc.Name = Loc("SpeciesCore_" + sc.InternalName + "_Name");
        }
        foreach (FactionData f in GameData.FactionDataList)
        {
            f.Name = Loc("Faction_" + f.InternalName + "_Name");
        }
        // 不再更新特质
    }

    // ─── 本地化辅助方法 ───────────────────────────────────────────────────────
    private static string Loc(string key) => Strings.ResourceManager.GetString(key, Strings.Culture) ?? key;

    private static string GetLocBreastSize(int titsType) => titsType switch
    {
        0 => Loc("Breast_Flat"),
        1 => Loc("Breast_Small"),
        2 => Loc("Breast_Average"),
        3 => Loc("Breast_Big"),
        4 => Loc("Breast_Huge"),
        5 => Loc("Breast_Extra"),
        _ => GameBalance.BreastsSizes.TryGetValue(titsType, out string? s) ? s : titsType.ToString()
    };

    private static string GetLocWombSize(EWombSize size) => size switch
    {
        EWombSize.Small => Loc("Womb_Small"),
        EWombSize.Average => Loc("Womb_Average"),
        EWombSize.Big => Loc("Womb_Big"),
        EWombSize.Huge => Loc("Womb_Huge"),
        EWombSize.Enormous => Loc("Womb_Enormous"),
        _ => size.ToString()
    };

    private static string GetLocTierName(int tier) => tier switch
    {
        1 => Loc("Tier_Common"),
        2 => Loc("Tier_Uncommon"),
        3 => Loc("Tier_Rare"),
        4 => Loc("Tier_Epic"),
        5 => Loc("Tier_Legendary"),
        _ => GameBalance.TierNames.TryGetValue(tier, out string? s) ? s : tier.ToString()
    };

    private static string GetLocRaceName(ERace race)
    {
        string localized = SeqLocalization.Localize("#" + race.ToString());
        return string.IsNullOrEmpty(localized) ? race.ToString() : localized;
    }

    internal static string GetLocJobName(string? name) => name switch
    {
        JobNames.Foraging => Loc("JobName_Foraging"),
        JobNames.Hunting => Loc("JobName_Hunting"),
        JobNames.CraftingTools => Loc("JobName_CraftingTools"),
        JobNames.CraftingArtifacts => Loc("JobName_CraftingArtifacts"),
        JobNames.Cleaning => Loc("JobName_Cleaning"),
        JobNames.Healing => Loc("JobName_Healing"),
        JobNames.HealingSpell => Loc("JobName_HealingSpell"),
        JobNames.SummonPixy => Loc("JobName_SummonPixy"),
        JobNames.HelpingNiel => Loc("JobName_HelpingNiel"),
        JobNames.HelpingFlora => Loc("JobName_HelpingFlora"),
        JobNames.HelpingBarbara => Loc("JobName_HelpingBarbara"),
        JobNames.Adventuring => Loc("JobName_Adventuring"),
        JobNames.Witchcraft => Loc("JobName_Witchcraft"),
        JobNames.WitchcraftBig => Loc("JobName_WitchcraftBig"),
        JobNames.PreachingCorruption => Loc("JobName_PreachingCorruption"),
        JobNames.CleansingDepravation => Loc("JobName_CleansingDepravation"),
        JobNames.CreateGolem => Loc("JobName_CreateGolem"),
        JobNames.PotionCrafting => Loc("JobName_PotionCrafting"),
        JobNames.WitchcraftPotion => Loc("JobName_WitchcraftPotion"),
        JobNames.Commerce => Loc("JobName_Commerce"),
        JobNames.AbilitySharing => Loc("JobName_AbilitySharing"),
        JobNames.DepravedTraining => Loc("JobName_DepravedTraining"),
        JobNames.DarkRitual => Loc("JobName_DarkRitual"),
        JobNames.HormoneExtraction => Loc("JobName_HormoneExtraction"),
        _ => name ?? string.Empty
    };

    /// <summary>物品本地化名称（用于工作描述与弹窗）。</summary>
    internal static string GetLocItemName(EItemType itemType) => Loc("ItemName_" + itemType);

    /// <summary>组合“种族/物种名 + 职业名”的显示名；没有职业时仅返回种族/物种名。</summary>
    internal static string ComposeSpeciesProfessionDisplayName(string? speciesName, string? professionName, string fallback = "")
    {
        string baseName = string.IsNullOrWhiteSpace(speciesName) ? fallback : speciesName;
        if (string.IsNullOrWhiteSpace(professionName))
            return baseName ?? string.Empty;
        return (baseName ?? string.Empty) + professionName;
    }
}