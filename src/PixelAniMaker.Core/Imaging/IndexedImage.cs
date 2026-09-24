namespace PixelAniMaker.Core.Imaging;

/// <summary>Pixel image storing palette indices (0 = transparent).</summary>
public sealed class IndexedImage
{
    private readonly ushort[] _pixels;

    public IndexedImage(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        Width = width;
        Height = height;
        _pixels = new ushort[width * height];
    }

    public int Width { get; }
    public int Height { get; }

    public ReadOnlySpan<ushort> Pixels => _pixels;

    /// <summary>Incremented on every pixel change; lets caches detect edits.</summary>
    public int Version { get; private set; }

    public bool InBounds(int x, int y) => (uint)x < (uint)Width && (uint)y < (uint)Height;

    public int this[int x, int y] => _pixels[y * Width + x];

    /// <summary>Sets a pixel; returns false when out of bounds or unchanged.</summary>
    public bool Set(int x, int y, int index)
    {
        if (!InBounds(x, y))
            return false;
        ref ushort p = ref _pixels[y * Width + x];
        if (p == index)
            return false;
        p = (ushort)index;
        Version++;
        return true;
    }

    public IndexedImage Clone()
    {
        var copy = new IndexedImage(Width, Height);
        _pixels.CopyTo(copy._pixels, 0);
        return copy;
    }

    /// <summary>Writes the image as BGRA32 into <paramref name="destination"/> (row stride = Width).</summary>
    public void ToBgra32(Palette palette, Span<uint> destination)
    {
        for (int i = 0; i < _pixels.Length; i++)
            destination[i] = palette[_pixels[i]].ToBgra32();
    }

    public RgbaImage ToRgba(Palette palette)
    {
        var pixels = new Rgba[_pixels.Length];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = palette[_pixels[i]];
        return new RgbaImage(Width, Height, pixels);
    }

    /// <summary>Builds an indexed image from RGBA pixels, adding unseen colours to <paramref name="palette"/>.</summary>
    public static IndexedImage FromRgba(int width, int height, ReadOnlySpan<Rgba> pixels, Palette palette)
    {
        if (pixels.Length != width * height)
            throw new ArgumentException("Pixel count does not match the size.", nameof(pixels));
        var image = new IndexedImage(width, height);
        for (int i = 0; i < pixels.Length; i++)
            image._pixels[i] = (ushort)palette.GetOrAdd(pixels[i]);
        return image;
    }
}
