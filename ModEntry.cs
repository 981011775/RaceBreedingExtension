using System;
using MBM.ModLoader.API;
using UnityEngine;

namespace RaceBreedingExtension
{
    /// <summary>
    /// 合并后的模组统一入口（RaceBreedingExtension）。
    /// 由 MBM.ModLoader 自动发现（public static 无参 Load()）。
    /// 负责初始化 ComplexBreedingRedux 与 DepravedShield 两套子系统。
    /// Harmony [HarmonyPatch] 补丁由 ModLoader 加载本程序集后自动 PatchAll，
    /// 因此此处不再手动 PatchAll，仅注册需要反射定位的补丁。
    /// </summary>
    public static class ModEntry
    {
        internal const string ModName = "RaceBreedingExtension";

        public static void Load()
        {
            // MODS 菜单（Manage 详情面板）中的本地化描述
            try
            {
                ModLoaderAPI.SetDescription(ModName, "A large-scale race and mechanics expansion built on Complex Breeding Redux. It fixes the issue of Complex Breeding Redux over-modifying the original game by instead expanding on top of the original game.");
                ModLoaderAPI.SetDescription(ModName, "zh-CN", "基于复杂繁育制作的大型种族与机制拓展拓展，修改了复杂繁育过度修改原游戏的问题，改成在原游戏的基础上进行拓展。");
            }
            catch (Exception e)
            {
                Debug.LogError("[RaceBreedingExtension] SetDescription failed: " + e);
            }

            // ★ 修复原版每帧空引用刷屏：把模组自用特质槽位（血脉 9 / 职业 10 / Princess 保证种族特质 30）
            // 注册进原版 TraitData 数据库。必须早于任何写入这些槽位的子系统，
            // 否则原版（Male.Idle 的 Trait70 分支、TryGetIsPositiveRaceTrait、Trait67/Trait90 合并等）
            // 会对不存在的 TraitData 直接访问 .IsPositive 而抛出 NullReferenceException。
            Compat.ModTraitSlots.Install();

            try
            {
                ComplexBreedingRedux.Plugin.Initialize();
                Debug.Log("[RaceBreedingExtension] ComplexBreedingRedux subsystem initialized.");
            }
            catch (Exception e)
            {
                Debug.LogError("[RaceBreedingExtension] ComplexBreedingRedux init failed: " + e);
            }

            // ★ 独特角色换色子系统（UniqueLookColor）：对复用独特 Figure / 换皮 Rig（如 GolemRig）的角色，
            // 在渲染层（OnChangeSpine 之后）按槽位名前缀把 HairColor/EyeColor 乘染到全部发/眼部件，
            // 覆盖站立/卧姿/性爱/分娩等各动作骨架的命名差异；并支持独立「服色」（扩展字段 + ModSaveData 存档）。
            // UI 仅在「女性信息窗口 → 扩展区」注入 发色/瞳色/服色 三个取色槽（不注入画廊，避免画廊 UI 超框）。
            // 补丁通过 [HarmonyPatch] 由 ModLoader PatchAll 自动加载。
            try
            {
                UniqueLookColor.UniqueLookColorSystem.Initialize();
                Debug.Log("[RaceBreedingExtension] UniqueLookColor subsystem initialized.");
            }
            catch (Exception e)
            {
                Debug.LogError("[RaceBreedingExtension] UniqueLookColor init failed: " + e);
            }

            try
            {
                DepravedShield.ModEntry.Initialize();
                Debug.Log("[RaceBreedingExtension] DepravedShield subsystem initialized.");
            }
            catch (Exception e)
            {
                Debug.LogError("[RaceBreedingExtension] DepravedShield init failed: " + e);
            }

            // ★ 血脉系统（Bloodline）：彻底取代原高等种族特质（HighRaceTrait，已整体移除）。
            // 仍然是「单 Trait 映射」：一般特质 ETrait.Trait9 的值 = 血脉 ID × 1000 + 等级（1~10），
            // 界面层伪装成“种族特质”显示，绕开直接添加新 RaceTrait 导致的游戏冻结。
            try
            {
                Bloodline.BloodlineSystem.Initialize();
                Debug.Log("[RaceBreedingExtension] Bloodline subsystem initialized.");
            }
            catch (Exception e)
            {
                Debug.LogError("[RaceBreedingExtension] Bloodline init failed: " + e);
            }

            // ★ 重制语音系统（VoiceReplay）：完全复刻原版女性语音播放列表
            // （PlayVoiceSound / PlayVoiceSoundInSex / PlayTouchSound，含种族、特殊角色、VoiceType A-G），
            // 并在 EState.Sex/SexDrained/CumShotInside/CumShotOutside 场景下改为根据
            // DepravedShield（堕落护盾）进度条播放语音（0-90% 循环 Sex/SexDrained，100% 高潮/中出语音）。
            try
            {
                VoiceReplay.VoiceReplaySystem.Initialize();
                Debug.Log("[RaceBreedingExtension] VoiceReplay subsystem initialized.");
            }
            catch (Exception e)
            {
                Debug.LogError("[RaceBreedingExtension] VoiceReplay init failed: " + e);
            }

            // ★ titmod 兼容子系统（TitsModCompat）：模组菜单开关 —— 屏蔽 titmod 的 EnableInfoPortrait
            // 对独特角色（特殊NPC）Spine 肖像的修改（修复肖像变黑），使其回归原版静态肖像；
            // 非独特角色保留 titmod 的动态 Spine 肖像。未安装 titmod 时补丁自动跳过，无副作用。
            try
            {
                TitsModCompat.TitsModCompatSystem.Initialize();
                Debug.Log("[RaceBreedingExtension] TitsModCompat subsystem initialized.");
            }
            catch (Exception e)
            {
                Debug.LogError("[RaceBreedingExtension] TitsModCompat init failed: " + e);
            }

            // ★ 猎手营地子系统（HunterCamp）：个人领地地面「猎手营地 / 大型猎手营地」的自动捕获流程
            // （营地空置数秒后自动生成奴隶并消耗金币；房内无怪物 60 秒后逃跑；
            //   有怪物则由原版 AI 触发性行为，累计若干次后逃跑；俘虏在正常状态下不可拖动，
            //   乏力/堕落/死亡时解锁；逃跑会损失大量金币）。
            // 旧版“房间升级获得猎手营地技能”的机制已整体删除，房间升级回归原版。
            // 补丁使用 [HarmonyPatch]，由 ModLoader 的 PatchAll 自动加载。
            try
            {
                HunterCamp.HunterCampSystem.Initialize();
                Debug.Log("[RaceBreedingExtension] HunterCamp subsystem initialized.");
            }
            catch (Exception e)
            {
                Debug.LogError("[RaceBreedingExtension] HunterCamp init failed: " + e);
            }

            // ★ 地面猎手营地子系统（GroundCamp）：在个人领地（私人宅邸）的地面建造列表中追加
            // 「猎手营地 / 大型猎手营地」两个可建造房间（复制原版 MonsterCage / MonsterCage2，
            // 名称与入住规则不同：怪物 + 奴隶可同时放入）。
            // 房间类型与房间类保持原版，不写入 Database<RoomData>，不影响原版房间；
            // 补丁使用 [HarmonyPatch]，由 ModLoader 的 PatchAll 自动加载。
            try
            {
                GroundCamp.GroundCampSystem.Initialize();
                Debug.Log("[RaceBreedingExtension] GroundCamp subsystem initialized.");
            }
            catch (Exception e)
            {
                Debug.LogError("[RaceBreedingExtension] GroundCamp init failed: " + e);
            }
        }
    }
}

