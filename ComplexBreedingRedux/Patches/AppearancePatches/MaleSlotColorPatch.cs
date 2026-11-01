#nullable enable
using System.Collections.Generic;
using HarmonyLib;
using MBMScripts;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace ComplexBreedingRedux.Patches.AppearancePatches;

/// <summary>
/// Окрашивает слоты Male монстров, которые игра пропускает.
/// Игра не имеет monsterHornColorPartList и не красит "nose"-слоты Male.
/// Патч красит "horn" и "nose" только для слотов, положительно идентифицированных
/// как Male (через MonsterShowPartList и Monster*ColorPartList из SpineData).
/// </summary>
[HarmonyPatch(typeof(UpdaterSpineCharacter), "OnChangeSpine")]
internal static class MaleSlotColorPatch
{
    private static readonly AccessTools.FieldRef<UpdaterSpineCharacter, Skeleton> SkeletonRef =
        AccessTools.FieldRefAccess<UpdaterSpineCharacter, Skeleton>("m_Skeleton");

    public static void Postfix(UpdaterSpineCharacter __instance)
    {
        if (__instance.Male == null)
            return;

        Skeleton? skeleton = SkeletonRef(__instance);
        if (skeleton == null)
            return;

        SpineData? spineData = __instance.SpineData;
        if (spineData == null)
            return;

        Color skinColor = __instance.Male.SkinColor;
        Color hornColor = __instance.Male.HornColor;
        // Color.white — дефолтное значение игры (нейтральный тинт).
        // Если HornColor не задан явно, используем SkinColor как fallback.
        if (hornColor == Color.white)
            hornColor = skinColor;

        // Собираем множество слотов, положительно принадлежащих Male.
        HashSet<string> maleSlots = BuildMaleSlotSet(spineData);
        if (maleSlots.Count == 0)
            return;

        ExposedList<Slot> slots = skeleton.Slots;
        for (int i = 0; i < slots.Count; i++)
        {
            Slot slot = slots.Items[i];
            string name = slot.Data.Name;

            // Красим только слоты, положительно идентифицированные как Male.
            if (!maleSlots.Contains(name))
                continue;

            if (name.Contains("horn"))
                slot.SetColor(hornColor);
            else if (name.Contains("nose"))
                slot.SetColor(skinColor);
        }
    }

    /// <summary>
    /// Собирает множество имён слотов, принадлежащих Male, из SpineData:
    /// - MonsterSkinColorPartList, MonsterHairColorPartList, MonsterEyeColorPartList, CondomColorPartList
    /// - Имена слотов из записей "slot:attachment" в MonsterShowPartList
    /// </summary>
    private static HashSet<string> BuildMaleSlotSet(SpineData spineData)
    {
        var set = new HashSet<string>();

        AddRange(set, spineData.MonsterSkinColorPartList);
        AddRange(set, spineData.MonsterHairColorPartList);
        AddRange(set, spineData.MonsterEyeColorPartList);
        AddRange(set, spineData.CondomColorPartList);
        ExtractSlotNames(set, spineData.MonsterShowPartList);

        return set;
    }

    /// <summary>
    /// Извлекает имена слотов из записей формата "slot:attachment".
    /// Записи без ':' — имена анимаций, слоты из них не извлекаются.
    /// </summary>
    private static void ExtractSlotNames(HashSet<string> set, List<string>? entries)
    {
        if (entries == null)
            return;
        for (int i = 0; i < entries.Count; i++)
        {
            string entry = entries[i];
            int colonIdx = entry.IndexOf(':');
            if (colonIdx > 0)
                set.Add(entry.Substring(0, colonIdx));
        }
    }

    private static void AddRange(HashSet<string> set, List<string>? list)
    {
        if (list == null)
            return;
        for (int i = 0; i < list.Count; i++)
            set.Add(list[i]);
    }
}
