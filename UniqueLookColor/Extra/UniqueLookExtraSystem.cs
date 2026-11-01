#nullable enable
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using MBMScripts;

namespace RaceBreedingExtension.UniqueLookColor.Extra
{
    /// <summary>
    /// 为每个 <see cref="Character"/> 挂载 <see cref="UniqueLookExtra"/>（不侵入原版序列化）。
    /// </summary>
    public static class UniqueLookExtraSystem
    {
        private static readonly ConditionalWeakTable<Character, UniqueLookExtra> ExtraTable = new();

        public static UniqueLookExtra Extra(this Character character)
        {
            return ExtraTable.GetValue(character, c => new UniqueLookExtra(c));
        }

        public static UniqueLookExtra ApplySaveData(Character character, int unitId, string? clothesColorHex)
        {
            UniqueLookExtra extra = ExtraTable.GetValue(character, c => new UniqueLookExtra(c, unitId, clothesColorHex));
            // 若 Entry 已存在（角色曾被访问），仍需把存档里的颜色写入
            extra.ApplyLoadedClothesColor(clothesColorHex);
            return extra;
        }

        /// <summary>枚举仍然存在于当前存档中的角色的扩展数据（用于保存）。</summary>
        internal static IEnumerable<(Character character, UniqueLookExtra extra)> EnumerateValid()
        {
            PlayData? playData = PlayData.Instance;
            if (playData == null)
                yield break;

            foreach (KeyValuePair<Character, UniqueLookExtra> kv in ExtraTable)
            {
                Character character = kv.Key;
                if (character == null)
                    continue;
                if (playData.GetUnit(character.UnitId) != character)
                    continue;

                yield return (character, kv.Value);
            }
        }
    }
}
