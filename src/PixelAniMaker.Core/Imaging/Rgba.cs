namespace PixelAniMaker.Core.Imaging;

/// <summary>Straight (non-premultiplied) 8-bit RGBA colour.</summary>
public readonly record struct Rgba(byte R, byte G, byte B, byte A = 255)
{
    public static readonly Rgba Transparent = new(0, 0, 0, 0);

    public bool IsTransparent => A == 0;

    /// <summary>Packs the colour as BGRA bytes in a little-endian uint (the layout of Bgra8888 bitmaps).</summary>
    public uint ToBgra32() => (uint)(B | (G << 8) | (R << 16) | (A << 24));

    public static Rgba FromBgra32(uint v) =>
        new((byte)(v >> 16), (byte)(v >> 8), (byte)v, (byte)(v >> 24));

    public override string ToString() => A == 255 ? $"#{R:X2}{G:X2}{B:X2}" : $"#{R:X2}{G:X2}{B:X2}{A:X2}";
}
