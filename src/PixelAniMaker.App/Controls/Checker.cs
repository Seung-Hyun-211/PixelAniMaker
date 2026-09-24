using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace PixelAniMaker.App.Controls;

/// <summary>Screen-space checkerboard brushes that mark transparent pixels.</summary>
public static class CheckerBrushes
{
    /// <summary>8 px cells, used behind canvases and previews.</summary>
    public static IBrush Large { get; } = Create(8);

    /// <summary>4 px cells, used for small swatches.</summary>
    public static IBrush Small { get; } = Create(4);

    private static unsafe IBrush Create(int cell)
    {
        var bmp = new WriteableBitmap(new PixelSize(cell * 2, cell * 2), new Vector(96, 96),
            PixelFormat.Bgra8888, AlphaFormat.Opaque);
        using (var fb = bmp.Lock())
        {
            for (int y = 0; y < cell * 2; y++)
            {
                var row = (uint*)((byte*)fb.Address + y * fb.RowBytes);
                for (int x = 0; x < cell * 2; x++)
                    row[x] = (x / cell + y / cell) % 2 == 0 ? 0xFF5A5A5Au : 0xFF6E6E6Eu;
            }
        }
        return new ImageBrush(bmp)
        {
            TileMode = TileMode.Tile,
            DestinationRect = new RelativeRect(0, 0, cell * 2, cell * 2, RelativeUnit.Absolute),
        };
    }
}
