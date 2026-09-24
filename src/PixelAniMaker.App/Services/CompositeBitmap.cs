using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using PixelAniMaker.Core.Imaging;
using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.App.Services;

/// <summary>Turns composites into Avalonia bitmaps.</summary>
public static class CompositeBitmap
{
    /// <summary>Opacity (0-255) of faded parts.</summary>
    private const byte DimAlpha = 70;

    public static WriteableBitmap Create(int width, int height) =>
        new(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);

    /// <summary>Creates a bitmap holding <paramref name="composite"/>.</summary>
    public static WriteableBitmap From(CompositeResult composite, Palette palette)
    {
        var bitmap = Create(composite.Width, composite.Height);
        Write(composite, palette, bitmap);
        return bitmap;
    }

    /// <summary>Writes a composite into <paramref name="bitmap"/>, optionally fading every part except one.</summary>
    public static unsafe void Write(CompositeResult composite, Palette palette, WriteableBitmap bitmap, int? dimExcept = null)
    {
        using var fb = bitmap.Lock();
        int width = composite.Width;
        for (int y = 0; y < composite.Height; y++)
        {
            var row = (uint*)((byte*)fb.Address + y * fb.RowBytes);
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                var c = palette[composite.Indices[i]];
                short owner = composite.Owners[i];
                bool dim = dimExcept is { } keep && owner != keep && owner != CompositeResult.NoPart;
                row[x] = (dim ? c with { A = DimAlpha } : c).ToBgra32();
            }
        }
    }
}
