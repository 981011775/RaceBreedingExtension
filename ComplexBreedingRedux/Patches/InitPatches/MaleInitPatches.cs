#nullable enable
using System;
using ComplexBreedingRedux.Data;
using ComplexBreedingRedux.Data.Species;
using ComplexBreedingRedux.Features;
using HarmonyLib;
using MBMScripts;

namespace ComplexBreedingRedux.Patches.InitPatches;

/// <summary>
/// Postfix на обе перегрузки <c>Male.Initialize</c> — задаёт Maintenance и палитру SkinColor.
/// </summary>
internal static class MaleInitPatches
{
    private static void ApplyMaintenance(Male instance)
    {
        if (instance is Minotaur m) { m.Maintenance = 500; return; }
        if (instance is Salamander s) { s.Maintenance = 800; }
    }

    // 注：雄性（怪物）的血脉不在初始化时直接添加 ——
    // 怪物血脉只在「遗传时生成」（BreedingSystem → BloodlineSystem.ApplyInheritedBloodline）。

    /// <summary>
    /// Заменяет ванильный <c>Color.Lerp(male.SkinColor, female.HairColor, 0.5f)</c>
    /// палитрой из <c>SpeciesRegistry</c>. Ваниль смешивает цвет отца с волосами матери
    /// для Goblin/Orc/Salamander, что даёт непредсказуемые цвета.
    /// </summary>
    private static void ApplySkinColor(Male instance)
    {
        string typeName = instance.GetType().Name;
        if (!GameData.TryGetSpecies(typeName, out ISpeciesData? species) || species == null)
            return;

        instance.SkinColor = SkinColorSystem.PickRandom(species.SkinColors);

        if (SkinColorSystem.MaleHairPaletteTypes.Contains(typeName) && species.HairColors is { Length: > 0 })
            instance.HairColor = SkinColorSystem.PickRandom(species.HairColors);
    }

    /// <summary>Патч на <c>Male.Initialize()</c> — вызывается при создании нового самца (покупка/спавн).</summary>
    [HarmonyPatch(typeof(Male), "Initialize", new Type[] { })]
    internal static class Overload0
    {
        public static void Postfix(Male __instance) => ApplyMaintenance(__instance);
    }

    /// <summary>Патч на <c>Male.Initialize(Character, Character)</c> — вызывается при рождении самца (мать, отец).</summary>
    [HarmonyPatch(typeof(Male), "Initialize", new Type[] { typeof(Character), typeof(Character) })]
    internal static class Overload2
    {
        public static void Postfix(Male __instance)
        {
            ApplyMaintenance(__instance);
            ApplySkinColor(__instance);
        }
    }
}
