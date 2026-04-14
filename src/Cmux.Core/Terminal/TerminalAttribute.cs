namespace Cmux.Core.Terminal;

[Flags]
public enum CellFlags : ushort
{
    None = 0,
    Bold = 1,
    Dim = 2,
    Italic = 4,
    Underline = 8,
    Blink = 16,
    Inverse = 32,
    Hidden = 64,
    Strikethrough = 128,
}

public struct TerminalColor : IEquatable<TerminalColor>
{
    public byte R;
    public byte G;
    public byte B;
    public bool IsDefault;

    /// <summary>
    /// When >= 0, this color was created from a palette index (0-15).
    /// The renderer should resolve it against the current theme palette at paint time.
    /// -1 means "not indexed" (true-color or default).
    /// </summary>
    public sbyte PaletteIndex;

    public TerminalColor(byte r, byte g, byte b)
    {
        R = r;
        G = g;
        B = b;
        IsDefault = false;
        PaletteIndex = -1;
    }

    public static TerminalColor Default => new() { IsDefault = true, PaletteIndex = -1 };

    public static TerminalColor FromIndex(int index)
    {
        if (index < 16)
        {
            var c = index switch
            {
                0 => new(0x00, 0x00, 0x00),
                1 => new(0xAA, 0x00, 0x00),
                2 => new(0x00, 0xAA, 0x00),
                3 => new(0xAA, 0x55, 0x00),
                4 => new(0x00, 0x00, 0xAA),
                5 => new(0xAA, 0x00, 0xAA),
                6 => new(0x00, 0xAA, 0xAA),
                7 => new(0xAA, 0xAA, 0xAA),
                8 => new(0x55, 0x55, 0x55),
                9 => new(0xFF, 0x55, 0x55),
                10 => new(0x55, 0xFF, 0x55),
                11 => new(0xFF, 0xFF, 0x55),
                12 => new(0x55, 0x55, 0xFF),
                13 => new(0xFF, 0x55, 0xFF),
                14 => new(0x55, 0xFF, 0xFF),
                15 => new(0xFF, 0xFF, 0xFF),
                _ => Default,
            };
            c.PaletteIndex = (sbyte)index;
            return c;
        }

        if (index < 232)
        {
            // 216-color cube (6x6x6)
            int i = index - 16;
            int r = i / 36;
            int g = (i / 6) % 6;
            int b = i % 6;
            return new((byte)(r > 0 ? r * 40 + 55 : 0), (byte)(g > 0 ? g * 40 + 55 : 0), (byte)(b > 0 ? b * 40 + 55 : 0));
        }

        if (index < 256)
        {
            // 24-step grayscale
            byte v = (byte)(index - 232);
            v = (byte)(v * 10 + 8);
            return new(v, v, v);
        }

        return Default;
    }

    public static TerminalColor FromRgb(byte r, byte g, byte b) => new(r, g, b);

    public bool Equals(TerminalColor other) =>
        R == other.R && G == other.G && B == other.B && IsDefault == other.IsDefault;

    public override bool Equals(object? obj) => obj is TerminalColor c && Equals(c);
    public override int GetHashCode() => HashCode.Combine(R, G, B, IsDefault);
    public static bool operator ==(TerminalColor left, TerminalColor right) => left.Equals(right);
    public static bool operator !=(TerminalColor left, TerminalColor right) => !left.Equals(right);
}

public struct TerminalAttribute
{
    public CellFlags Flags;
    public TerminalColor Foreground;
    public TerminalColor Background;

    public static TerminalAttribute Default => new()
    {
        Flags = CellFlags.None,
        Foreground = TerminalColor.Default,
        Background = TerminalColor.Default,
    };
}

public struct TerminalCell
{
    public char Character;
    public TerminalAttribute Attribute;
    public bool IsDirty;
    public int Width; // 1 for normal, 2 for wide chars

    public static TerminalCell Empty => new()
    {
        Character = ' ',
        Attribute = TerminalAttribute.Default,
        IsDirty = true,
        Width = 1,
    };
}
