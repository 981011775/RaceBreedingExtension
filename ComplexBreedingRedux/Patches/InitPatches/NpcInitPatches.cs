#nullable enable
using System.Collections.Generic;
using System.Reflection;
using ComplexBreedingRedux.Data;
using ComplexBreedingRedux.Data.Professions;
using ComplexBreedingRedux.Utilities;
using HarmonyLib;
using MBMScripts;

namespace ComplexBreedingRedux.Patches.InitPatches;

internal static class NpcInitPatches
{
    private sealed class NpcConfig
    {
        public string TypeName = null!;
        public int ProfessionId;
    }

    private static readonly Dictionary<MethodBase, NpcConfig> NpcMap = new();

    public static void Register(Harmony harmony)
    {
        var npcs = new NpcConfig[]
        {
            new() { TypeName = "MBMScripts.Anna",     ProfessionId = ProfessionRegistry.PriestId },     // 牧师
            new() { TypeName = "MBMScripts.Aure",     ProfessionId = ProfessionRegistry.PrincessId },   // 公主
            new() { TypeName = "MBMScripts.Bella",    ProfessionId = ProfessionRegistry.ApothecaryId }, // 药剂师
            new() { TypeName = "MBMScripts.Claire",   ProfessionId = ProfessionRegistry.HunterId },     // 猎人
            new() { TypeName = "MBMScripts.Karen",    ProfessionId = ProfessionRegistry.ThiefId },      // 盗贼
            new() { TypeName = "MBMScripts.Nero",     ProfessionId = ProfessionRegistry.DarkPriestId }, // 黑暗祭司
            new() { TypeName = "MBMScripts.Sylvia",   ProfessionId = ProfessionRegistry.KnightId },     // 骑士
            new() { TypeName = "MBMScripts.Vivi",     ProfessionId = ProfessionRegistry.WizardId },     // 法师
            new() { TypeName = "MBMScripts.Amilia2",  ProfessionId = ProfessionRegistry.TrainerId },    // 调教师
            new() { TypeName = "MBMScripts.Barbara2", ProfessionId = ProfessionRegistry.ArtificerId },  // 工匠
            new() { TypeName = "MBMScripts.Flora2",   ProfessionId = ProfessionRegistry.WitchId },      // 女巫
            new() { TypeName = "MBMScripts.Lena2",    ProfessionId = ProfessionRegistry.MerchantId },   // 商人
            new() { TypeName = "MBMScripts.Niel2",    ProfessionId = ProfessionRegistry.AlchemistId },  // 炼金术师
            new() { TypeName = "MBMScripts.Sena2",    ProfessionId = ProfessionRegistry.PrincessId },   // 公主
        };

        var prefix = new HarmonyMethod(typeof(NpcInitPatches), nameof(Prefix));
        var postfix = new HarmonyMethod(typeof(NpcInitPatches), nameof(Postfix));

        foreach (var npc in npcs)
        {
            var method = PatchUtils.FindNpcMethod(npc.TypeName, "InitializeTrait");
            if (method != null)
            {
                NpcMap[method] = npc;
                harmony.Patch(method, prefix: prefix, postfix: postfix);
            }
        }

        // 移除对 Sylvia.Initialize1 的阻断补丁
        // 原代码中有一个 BlockPrefix 的注册，此处删除
    }

    public static bool Prefix() => true;

    public static void Postfix(Female __instance, MethodBase __originalMethod)
    {
        if (!NpcMap.TryGetValue(__originalMethod, out var config))
            return;

        // 添加职业（公主ID=2）
        __instance.AddTrait(GameBalance.ProfessionTraitId);
        __instance.SetTraitValue(GameBalance.ProfessionTraitId, (float)config.ProfessionId);
        __instance.SortTrait();
    }
}