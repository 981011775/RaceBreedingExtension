#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using MBMScripts;
using MBM.ModLoader.Settings;

namespace RaceBreedingExtension.UniqueLookColor.Extra
{
    /// <summary>
    /// 独特角色服色的存档读写（参照 MoreGraphicAttachments.ExtensionField.ExtensionFieldSaveData）。
    /// 数据以 "UnitId:#RRGGBBAA;..." 形式存放在 MBM.ModLoader 的 ModSaveData 中（按存档槽位隔离）。
    /// 头/瞳色使用游戏自带字段，无需在此保存。
    /// </summary>
    public static class UniqueLookSaveData
    {
        private const string KEY = "UniqueLookExtraList";

        private static readonly Dictionary<int, string> PendingByUnitId = new();

        /// <summary>由 UniqueLookColorSystem.Initialize() 调用一次。</summary>
        public static void Register()
        {
            ModSaveData.OnBeforeSave += Save;
            ModSaveData.OnAfterLoad += Load;
        }

        private static void Save(int slot)
        {
            try
            {
                var sb = new StringBuilder();
                foreach ((Character character, UniqueLookExtra extra) in UniqueLookExtraSystem.EnumerateValid())
                {
                    if (!extra.NeedSave())
                        continue;
                    if (sb.Length > 0)
                        sb.Append(';');
                    sb.Append(character.UnitId).Append(':').Append(extra.ToColorHex());
                }

                ModSaveData.SetString(ModEntry.ModName, KEY, sb.ToString());
            }
            catch (Exception e)
            {
                UniqueLookColorSystem.LogError("Save failed: " + e);
            }
        }

        private static void Load(int slot)
        {
            try
            {
                PendingByUnitId.Clear();

                string raw = ModSaveData.GetString(ModEntry.ModName, KEY, string.Empty);
                if (string.IsNullOrEmpty(raw))
                    return;

                foreach (string part in raw.Split(';'))
                {
                    if (string.IsNullOrEmpty(part))
                        continue;

                    int idx = part.IndexOf(':');
                    if (idx <= 0)
                        continue;
                    if (!int.TryParse(part.Substring(0, idx), out int unitId))
                        continue;

                    string hex = part.Substring(idx + 1);
                    if (!string.IsNullOrEmpty(hex))
                        PendingByUnitId[unitId] = hex;
                }
            }
            catch (Exception e)
            {
                UniqueLookColorSystem.LogError("Load failed: " + e);
            }
        }

        /// <summary>PlayData.OnEnable（读档数据就绪）后调用，把读出的服色套用到角色。</summary>
        public static void ApplyAll()
        {
            if (PendingByUnitId.Count == 0)
                return;

            try
            {
                PlayData? playData = PlayData.Instance;
                if (playData == null)
                    return;

                foreach (KeyValuePair<int, string> kv in PendingByUnitId)
                {
                    if (playData.GetUnit(kv.Key) is Character character)
                        UniqueLookExtraSystem.ApplySaveData(character, kv.Key, kv.Value);
                    else
                        UniqueLookColorSystem.LogWarning($"UnitId {kv.Key} not found or not a Character; clothes color skipped.");
                }
            }
            catch (Exception e)
            {
                UniqueLookColorSystem.LogError("ApplyAll failed: " + e);
            }
            finally
            {
                PendingByUnitId.Clear();
            }
        }
    }
}
