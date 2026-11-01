#nullable enable
namespace RaceBreedingExtension.VoiceReplay
{
    /// <summary>
    /// 原版女性角色语音播放列表（完全复刻）。
    /// 数据来源：原游戏反编译代码 Assembly-CSharp.txt
    ///   - GameManager.PlayVoiceSound（日常语音，按 VoiceType 0-6 = A-G 区分种族）
    ///   - GameManager.PlayVoiceSoundInSex（性爱语音）
    ///   - GameManager.PlayTouchSound（触摸语音，特殊角色）
    ///   - GameManager.IsVoiceSound（原版语音名总表，含 woman_come / loli_come 高潮语音）
    /// </summary>
    internal static class VoiceCatalog
    {
        public const int VoiceTypeCount = 7; // A,B,C,D,E,F,G

        /// <summary>VoiceType → 字母（A-G），与原版 UI 显示一致（Convert.ToChar(VoiceType + 65)）。</summary>
        public static char GetVoiceTypeLetter(int voiceType) => (char)(voiceType + 65);

        // ==================== 原版 PlayVoiceSoundInSex：各族性爱语音 ====================
        // 索引：0=A Loli, 1=B Human, 2=C Elf, 3=D Dwarf, 4=E Furry, 5=F SmallFurry, 6=G Dragonian
        // 变体：0=普通 Sex（_1），1=SexDrained（_2），2=堕落（_3）
        public static readonly string[][] SexVoices =
        {
            new[] { "Loli_sex_1",          "Loli_sex_2",          "Loli_sex_3" },          // 0 = A
            new[] { "human_sex_1",         "human_sex_2",         "human_sex_3" },         // 1 = B
            new[] { "Elf_sex_1",           "Elf_sex_2",           "Elf_sex_3" },           // 2 = C
            new[] { "Dwarf_sex_1",         "Dwarf_sex_2",         "Dwarf_sex_3" },         // 3 = D
            new[] { "furry_sex_1",         "furry_sex_2",         "furry_sex_3" },         // 4 = E
            new[] { "small_furry_sex_1",   "small_furry_sex_2",   "small_furry_sex_3" },   // 5 = F
            new[] { "dragonian_sex_1",     "dragonian_sex_2",     "dragonian_sex_3" },     // 6 = G
        };

        // ==================== 高潮/中出（CumShotInside / CumShotOutside）语音 ====================
        // 原版 IsVoiceSound 总表中与“高潮/中出”相关的语音：
        //   Human → woman_come (1)-(7)，Loli → loli_come (1)-(5)。
        // 其余种族原版没有独立的高潮音，使用各族 _3（堕落/激烈）变体作为高潮语音。
        public static readonly string[][] CumshotVoices =
        {
            new[] { "loli_come (1)", "loli_come (2)", "loli_come (3)", "loli_come (4)", "loli_come (5)" },                  // 0 = A
            new[] { "woman_come (1)", "woman_come (2)", "woman_come (3)", "woman_come (4)", "woman_come (5)", "woman_come (6)", "woman_come (7)" }, // 1 = B
            new[] { "Elf_sex_3" },        // 2 = C
            new[] { "Dwarf_sex_3" },      // 3 = D
            new[] { "furry_sex_3" },      // 4 = E
            new[] { "small_furry_sex_3" },// 5 = F
            new[] { "dragonian_sex_3" },  // 6 = G
        };

        // ==================== 特殊角色性爱语音（原版 PlayVoiceSoundInSex） ====================
        // normal = 普通性爱语音，intense = 高潮/激烈变体（对应各特殊场景中的 _2 音频）
        public static readonly (string normal, string intense)[] SpecialNpcSexVoices =
        {
            ("amilia_sex",   "amilia_sex_2"),
            ("flora_sex",    "flora_sex_2"),
            ("niel_sex",     "niel_sex_2"),
            ("sena_sex",     "senalena_sex_2"),
            ("barbara_sex",  "barbara_sex_2"),
        };

        // ==================== 特殊角色日常语音（原版 PlayVoiceSound） ====================
        public static readonly (string sound, int index)[] SpecialNpcDailyVoices =
        {
            ("adult_sex (1)", 0),  // Amilia / Amilia2
            ("woman_sex (22)", 1), // Flora / Flora2
            ("dwarf_wk_1", 2),     // Niel / Niel2
            ("smfurry_s_2", 3),    // SenaLena / Sena2 / Lena2
            ("furry_s_18", 4),     // Barbara / Barbara2
        };

        // ==================== 怪物种族日常语音（原版 PlayVoiceSound） ====================
        public static readonly (string sound, int index)[] MonsterDailyVoices =
        {
            ("goblin_growl00", 0),
            ("orc_growl00", 1),
            ("werewolf_growl00", 2),
            ("minotaur_growl00", 3),
            ("salamander growl00", 4),
            ("horse_sound", 5),
        };

        /// <summary>越界保护：VoiceType 固定为 0-6（A-G）。</summary>
        public static int ClampVoiceType(int voiceType)
            => voiceType < 0 ? 0 : (voiceType >= VoiceTypeCount ? VoiceTypeCount - 1 : voiceType);
    }
}
