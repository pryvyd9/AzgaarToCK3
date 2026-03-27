namespace Drawing;

/// <summary>RGBA color with components in [0, 1].</summary>
public readonly record struct RgbaColor(float R, float G, float B, float A = 1f)
{
    public static readonly RgbaColor Black = new(0f, 0f, 0f);
    public static readonly RgbaColor White = new(1f, 1f, 1f);
    public static readonly RgbaColor Transparent = new(0f, 0f, 0f, 0f);

    /// <summary>Creates a color from byte components (0–255).</summary>
    public static RgbaColor FromBytes(byte r, byte g, byte b, byte a = 255)
        => new(r / 255f, g / 255f, b / 255f, a / 255f);

    /// <summary>Creates a grayscale color from a single byte value (0–255).</summary>
    public static RgbaColor FromGrayscaleByte(byte value)
        => FromBytes(value, value, value);
}
