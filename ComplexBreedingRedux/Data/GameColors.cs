#nullable enable
using System.Collections.Generic;

namespace ComplexBreedingRedux.Data;

internal static class GameColors
{
    public const string GoodColorHex    = "#63E057";
    public const string BadColorHex     = "#E07369";
    public const string BoldColorHex    = "#FFD700";
    public const string BoldColor2Hex   = "#9e97a3";
    public const string EssenceHex      = "#480e5f";
    public const string ProfessionHex   = "#0c4855";
    public const string PersonalityHex  = "#57470f";
    public const string TraitHex        = "#9e97a3";
    public const string TextSeperator   = "<BR><color=#A567D1>--------</color>";
    public const int    DescriptionFontSize = 11;

    public static readonly Dictionary<int, string> TierColors = new Dictionary<int, string>
    {
        { 1, "#008000" },
        { 2, "#4F8ACF" },
        { 3, "#BA65C1" },
        { 4, "#930EF1" },
        { 5, "#FFA500" },
    };
}
