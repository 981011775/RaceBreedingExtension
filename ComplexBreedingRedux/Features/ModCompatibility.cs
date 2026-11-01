#nullable enable
using MBM.ModLoader.Core;
using MBMScripts;

namespace ComplexBreedingRedux.Features;

internal static class ModCompatibility
{
    public const string TitsModFileName = "TitsMod";
    public const int TitsModMaxBreastSize = 5;
    public const int VanillaMaxBreastSizeDlc = 4;
    public const int VanillaMaxBreastSizeNoDlc = 3;

    public static bool IsTitsModLoaded() => Loader.IsModLoaded(TitsModFileName);

    /// <summary>Максимальный TitsType: 5 с TitsMod, иначе ванильный предел (4 с DLC, 3 без).</summary>
    public static int GetMaxBreastSize()
    {
        if (IsTitsModLoaded())
            return TitsModMaxBreastSize;

        return GameManager.Instance != null && GameManager.Instance.Dlc
            ? VanillaMaxBreastSizeDlc
            : VanillaMaxBreastSizeNoDlc;
    }
}