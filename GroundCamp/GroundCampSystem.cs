#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using MBM.ModLoader.Core;
using MBMScripts;
using UnityEngine;

namespace RaceBreedingExtension.GroundCamp
{
    /// <summary>
    /// 地面猎手营地（Ground Camp）核心逻辑。
    ///
    /// 实现要点（不影响原版房间、功能完全独立）：
    ///  1) 房间类型保持原版 <see cref="ERoomType.MonsterCage"/> / <see cref="ERoomType.MonsterCage2"/>，
    ///     房间类也不变 —— 因此原版升级、房间技能（含本模组猎手营地技能）、
    ///     Spine 外观/动画、拆除逻辑、BinaryFormatter 存档序列化全部照旧，不存在坏档风险。
    ///  2) 只在“个人领地（IsPrivateEstate）”的**地面**建造列表中追加两条只读副本 RoomData
    ///     （DataId 仍为 10 / 19，但 IsGround = TRUE，DisplayName = 猎手营地 / 大型猎手营地）。
    ///     副本不会写入 <c>Database&lt;RoomData&gt;</c>，因此原版地下建造条目与其它 RoomData 完全不受影响。
    ///  3) 用“槽位 Y &gt; 0”识别地面猎手营地：原版怪物牢笼只可能建在 Y ≤ 0 的地下格
    ///     （建造点由 RoomData.IsGround 决定；开局初始化同样是 (2000,-1) / (2000,-2)），
    ///     所以识别不需要任何额外存档数据，卸载模组后也能优雅退化为普通怪物牢笼。
    ///  4) 地面猎手营地：RoomData 返回地面副本（IsGround 生效 → 拆除支柱逻辑正确、显示本房间名），
    ///     并通过 IsAllocatable 补丁让“怪物 + 奴隶”同时入住。
    /// </summary>
    internal static class GroundCampSystem
    {
        /// <summary>地面版猎手营地 RoomData（复制自原版 MonsterCage，DataId = 10）。</summary>
        private static RoomData? _smallCampData;

        /// <summary>地面版大型猎手营地 RoomData（复制自原版 MonsterCage2，DataId = 19）。</summary>
        private static RoomData? _largeCampData;

        private static bool _languageSubscribed;

        // ============================ 初始化 ============================

        public static void Initialize()
        {
            GroundCampConfig.Register();

            if (!_languageSubscribed)
            {
                Localization.OnLanguageChanged += OnLanguageChanged;
                _languageSubscribed = true;
            }

            Log("Ground camp subsystem initialized.");
        }

        private static void OnLanguageChanged(string langCode)
        {
            try
            {
                GroundCampConfig.OnLanguageChanged();
                RefreshRoomNames();
            }
            catch (Exception e)
            {
                LogError("OnLanguageChanged failed: " + e);
            }
        }

        /// <summary>语言切换后刷新两个地面房间副本的显示名。</summary>
        public static void RefreshRoomNames()
        {
            if (_smallCampData != null)
                SetPrivateField(_smallCampData, "m_DisplayName", GclLoc.RoomName);
            if (_largeCampData != null)
                SetPrivateField(_largeCampData, "m_DisplayName", GclLoc.RoomNameLarge);
        }

        // ============================ 房间识别 ============================

        /// <summary>是否为“地面猎手营地”（复制自原版 MonsterCage）。</summary>
        public static bool IsSmallGroundCamp(Room? room)
            => room is MonsterCage && room.Slot.Item2 > 0;

        /// <summary>是否为“大型地面猎手营地”（复制自原版 MonsterCage2）。</summary>
        public static bool IsLargeGroundCamp(Room? room)
            => room is MonsterCage2 && room.Slot.Item2 > 0;

        /// <summary>是否为任意一种地面猎手营地。</summary>
        public static bool IsGroundCamp(Room? room)
            => IsSmallGroundCamp(room) || IsLargeGroundCamp(room);

        /// <summary>
        /// 取得地面猎手营地对应的地面版 RoomData（IsGround = TRUE、显示名为猎手营地）。
        /// 非地面猎手营地返回 false，此时调用方应保持原版行为。
        /// </summary>
        public static bool TryGetGroundCampData(Room? room, out RoomData data)
        {
            data = null!;
            if (room == null || room.Slot.Item2 <= 0)
                return false;
            if (room is MonsterCage)
                return EnsureData(false, out data);
            if (room is MonsterCage2)
                return EnsureData(true, out data);
            return false;
        }

        // ============================ 地面 RoomData 副本 ============================

        /// <summary>懒加载：从原版 RoomData 复制出地面版本（仅内存，不写入数据库）。</summary>
        private static bool EnsureData(bool large, out RoomData data)
        {
            data = null!;

            RoomData? cached = large ? _largeCampData : _smallCampData;
            if (cached != null)
            {
                data = cached;
                return true;
            }

            try
            {
                ERoomType vanillaType = large ? ERoomType.MonsterCage2 : ERoomType.MonsterCage;
                RoomData? source = Database<RoomData>.GetDataByDataId((int)vanillaType);
                if (source == null)
                {
                    // 数据表尚未初始化完成（例如 CustomAssetTextResource 正在替换数据库），下次再试。
                    return false;
                }

                RoomData clone = CloneRoomData(source, large);
                if (large)
                    _largeCampData = clone;
                else
                    _smallCampData = clone;

                data = clone;
                Log($"已创建地面房间数据副本：{clone.DisplayName}（DataId={(int)vanillaType}, IsGround={clone.IsGround}, Mana={clone.Mana}）");
                return true;
            }
            catch (Exception e)
            {
                LogError("EnsureData failed: " + e);
                return false;
            }
        }

        /// <summary>反射复制 RoomData（含私有 [SerializeField] 字段），并覆盖为“地面猎手营地”的差异项。</summary>
        private static RoomData CloneRoomData(RoomData source, bool large)
        {
            RoomData clone = new RoomData();

            foreach (FieldInfo field in typeof(RoomData).GetFields(
                         BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                object? value = field.GetValue(source);

                // 占地格列表需要深拷贝，避免与原版数据共享同一个 List 实例。
                if (value is List<Vector2Int> locationList)
                    value = new List<Vector2Int>(locationList);

                field.SetValue(clone, value);
            }

            // 清空按需缓存的字段，让其按新数据重新解析。
            SetPrivateField(clone, "m_BuildSpriteFullName", null);
            SetPrivateField(clone, "m_BuildIconSpriteFullName", null);
            SetPrivateField(clone, "m_PointAttachmentData", null);

            // ─── 与原版的唯一差异：名称 / 地面建筑 / 个人领地没有客人 / 不可升级 ───
            SetPrivateField(clone, "m_DisplayName", large ? GclLoc.RoomNameLarge : GclLoc.RoomName);
            SetPrivateField(clone, "m_IsGround", true);
            SetPrivateField(clone, "m_ClientCanVisit", false);
            SetPrivateField(clone, "m_Block", false);
            // ★ 猎手营地不可升级：RoomData.Upgradable = false（升级入口会显示「无法升级」并直接返回）
            SetPrivateField(clone, "m_Upgradable", false);

            // DataName 仅用于调试辨识（该副本不会进入 Database 的名称/ID 字典）。
            clone.m_DataName = large ? "GroundHunterCampLarge" : "GroundHunterCamp";

            return clone;
        }

        private static void SetPrivateField(object target, string fieldName, object? value)
        {
            FieldInfo? field = target.GetType().GetField(
                fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null)
            {
                LogError($"字段不存在：{target.GetType().Name}.{fieldName}");
                return;
            }
            field.SetValue(target, value);
        }

        // ============================ 建造列表（仅个人领地） ============================

        /// <summary>
        /// 在 <see cref="PlayData.SetBuildList"/> 之后调用：
        /// 个人领地时把两个地面猎手营地追加到**地面**建造列表；离开个人领地时自动取消选择。
        /// （建造面板的按钮数量由 UpdaterPrefabList 按 BuildGroundList.Count 动态生成，
        ///   因此追加条目会自动多出对应按钮，无需改动 UI。）
        /// </summary>
        public static void AddToPrivateEstateBuildList(PlayData? pd)
        {
            if (pd == null || !GroundCampConfig.Enable)
                return;

            if (!pd.IsPrivateEstate)
            {
                CancelCampBuildIfSelected(pd);
                return;
            }

            if (!EnsureData(false, out RoomData small) || !EnsureData(true, out RoomData large))
                return;

            var list = pd.BuildGroundList;
            bool largeAvailable = IsLargeCampAvailable(pd);

            if (!list.Contains(small))
                list.Add(small);

            if (largeAvailable && !list.Contains(large))
                list.Add(large);

            Log($"个人领地地面建造列表已追加猎手营地：共 {list.Count} 项" +
                $"（猎手营地={list.Contains(small)}, 大型猎手营地={largeAvailable && list.Contains(large)}）");
        }

        /// <summary>大型猎手营地是否已解锁（可选按原版“巨型怪物”教程事件解锁）。</summary>
        private static bool IsLargeCampAvailable(PlayData pd)
            => !GroundCampConfig.GateLargeCamp || pd.GetGameEventFlag(EGameEvent.Tutorial_HugeMonster);

        /// <summary>离开个人领地时，若当前正选中猎手营地建造模式则取消（保证仅个人领地可建造）。</summary>
        private static void CancelCampBuildIfSelected(PlayData pd)
        {
            RoomData? current = pd.BuildRoomData;
            if (current == null)
                return;

            if (ReferenceEquals(current, _smallCampData) || ReferenceEquals(current, _largeCampData))
            {
                pd.CancelBuild();
                Log("已离开个人领地：取消地面猎手营地的建造选择。");
            }
        }

        // ============================ 混住容量 ============================

        /// <summary>
        /// 地面猎手营地的入住判定：
        ///  - 奴隶（Female）→ ESector.Female，上限由配置决定；
        ///  - 怪物 → 小营地 = Goblin/Orc/Werewolf（上限 5，同原版），大营地 = Minotaur/Salamander（上限 3，同原版）。
        /// </summary>
        public static bool IsAllocatable(Room room, Unit? unit, bool large)
        {
            if (room == null || unit == null)
                return false;

            PlayData? pd = GameManager.Instance?.PlayerData;
            if (pd == null)
                return false;

            // ─── 奴隶（Female）───
            if (unit is Female)
            {
                int slaveCapacity = GetEffectiveCapacity(room,
                    large ? GroundCampConfig.LargeSlaveCapacity : GroundCampConfig.SmallSlaveCapacity);
                if (slaveCapacity <= 0)
                    return false;
                return pd.GetUnitList(ESector.Female, room.Slot).Count < slaveCapacity;
            }

            // ─── 怪物 ───
            bool isMonster = large
                ? unit is Minotaur or Salamander
                : unit is Goblin or Orc or Werewolf;
            if (!isMonster)
                return false;

            int monsterCapacity = large
                ? GroundCampConfig.LargeMonsterCapacity
                : GroundCampConfig.SmallMonsterCapacity;
            return pd.GetUnitList(ESector.Male, room.Slot).Count < monsterCapacity;
        }

        /// <summary>
        /// 营地的奴隶容量上限：猎手营地（MonsterCage 复制的房间）默认 5，大型猎手营地默认 3，
        /// 并受房间预制体座位数限制（与 <see cref="IsAllocatable"/> 使用同一套规则）。
        /// </summary>
        public static int GetSlaveCapacity(Room? room)
        {
            if (room == null)
                return 0;

            bool large = room is MonsterCage2;
            int config = large ? GroundCampConfig.LargeSlaveCapacity : GroundCampConfig.SmallSlaveCapacity;
            return GetEffectiveCapacity(room, config);
        }

        /// <summary>
        /// 实际可用容量 = min(配置值, 房间预制体的座位数)。
        /// 说明：游戏按“每个 Sector 各自占用座位索引（0 ~ SeatCount-1）”分配座位
        /// （见 PlayData.GetEmptySeatInRoom），所以每个 Sector 的上限不会超过房间座位数
        /// （怪物牢笼的座位就是按原版 5 / 3 只怪物设计的，因此大营地默认 3 个奴隶也足够）。
        /// </summary>
        private static int GetEffectiveCapacity(Room room, int configCapacity)
        {
            if (configCapacity <= 0)
                return 0;

            try
            {
                if (room.UnitComponent != null)
                {
                    int seatCount = room.SeatCount;
                    if (seatCount > 0 && seatCount < configCapacity)
                        return seatCount;
                }
            }
            catch
            {
                // 房间尚未生成显示对象时不取座位数，直接使用配置值。
            }

            return configCapacity;
        }

        // ============================ 日志 ============================

        internal static void Log(string msg)
        {
            if (GroundCampConfig.EnableLogging)
                Debug.Log($"[RaceBreedingExtension][GroundCamp] {msg}");
        }

        internal static void LogError(string msg)
            => Debug.LogError($"[RaceBreedingExtension][GroundCamp] {msg}");
    }
}
