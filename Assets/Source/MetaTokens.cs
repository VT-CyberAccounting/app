using UnityEngine;

public static class MetaTokens
{
    public static readonly Color Blue = Hex(0x01, 0x73, 0xEC);
    public static readonly Color BlueLight = Hex(0x64, 0xB5, 0xFF);
    public static readonly Color Green = Hex(0x0B, 0x8A, 0x1B);
    public static readonly Color Red = Hex(0xDD, 0x15, 0x35);

    public static readonly Color Sheet = Hex(0x27, 0x27, 0x27);
    public static readonly Color SheetAlt = Hex(0x41, 0x41, 0x41);
    public static readonly Color Neutral5A = Hex(0x5A, 0x5A, 0x5A);
    public static readonly Color Neutral74 = Hex(0x74, 0x74, 0x74);
    public static readonly Color Neutral8E = Hex(0x8E, 0x8E, 0x8E);
    public static readonly Color NeutralC0 = Hex(0xC0, 0xC0, 0xC0);
    public static readonly Color NeutralD9 = Hex(0xD9, 0xD9, 0xD9);
    public static readonly Color TextPrimary = Hex(0xF2, 0xF2, 0xF2);
    public static readonly Color White = Color.white;

    public const float PanelWidth = 502f;
    public const float PanelHeight = 634f;
    public const float RowHeight = 48f;
    public const float ButtonHeight = 48f;
    public const float ToolButtonWidth = 100f;
    public const float PanelGutter = 12f;
    public const float Spacing = 6f;
    public const float IconSize = 24f;

    public const float PanelTitle = 16f;
    public const float ToolHeader = 14f;
    public const float Headline1 = 12f;
    public const float Headline2 = 12f;
    public const float Headline3 = 12f;
    public const float Body1 = 12f;
    public const float Body2 = 12f;
    public const float Subheadline = 12f;
    public const float Caption = 12f;

    public const float RadiusChip = 4f;
    public const float RadiusButton = 8f;
    public const float RadiusCard = 12f;
    public const float RadiusPill = 22f;
    public const float RadiusCircle = 60f;

    public static readonly Color LockDim = Alpha(Color.black, 0.40f);

    public static Color Alpha(Color c, float a)
    {
        c.a = a;
        return c;
    }

    private static Color Hex(byte r, byte g, byte b) => new Color32(r, g, b, 255);
}
