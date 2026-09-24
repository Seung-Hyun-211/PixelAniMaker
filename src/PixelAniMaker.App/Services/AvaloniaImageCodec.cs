using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using PixelAniMaker.Core.Imaging;

namespace PixelAniMaker.App.Services;

/// <summary>PNG encode/decode through Avalonia (Skia).</summary>
public sealed class AvaloniaImageCodec : IImageCodec
{
    public static AvaloniaImageCodec Instance { get; } = new();

    public unsafe byte[] EncodePng(RgbaImage image)
    {
        using var bitmap = new WriteableBitmap(new PixelSize(image.Width, image.Height), new Vector(96, 96),
            PixelFormat.Bgra8888, AlphaFormat.Unpremul);
        using (var fb = bitmap.Lock())
        {
            for (int y = 0; y < image.Height; y++)
            {
                var row = (uint*)((byte*)fb.Address + y * fb.RowBytes);
                for (int x = 0; x < image.Width; x++)
                    row[x] = image.Pixels[y * image.Width + x].ToBgra32();
            }
        }
        using var ms = new MemoryStream();
        bitmap.Save(ms, PngBitmapEncoderOptions.Default);
        return ms.ToArray();
    }

    /// <summary>Decodes any PNG; alpha below 50% becomes fully transparent (pixel art has no soft edges).</summary>
    /// <exception cref="InvalidDataException">The data is not a readable image.</exception>
    public RgbaImage DecodePng(Stream stream)
    {
        // Skia needs a seekable stream; zip entries are not
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        buffer.Position = 0;
        try
        {
            using var bitmap = new Bitmap(buffer);
            return ToRgba(bitmap);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidDataException("PNG 이미지를 읽을 수 없습니다.", ex);
        }
    }

    private static unsafe RgbaImage ToRgba(Bitmap bitmap)
    {
        var size = bitmap.PixelSize;
        var raw = new uint[size.Width * size.Height];
        fixed (uint* p = raw)
            bitmap.CopyPixels(new PixelRect(size), (nint)p, raw.Length * 4, size.Width * 4);

        bool rgbaOrder = bitmap.Format == PixelFormat.Rgba8888;
        var pixels = new Rgba[raw.Length];
        for (int i = 0; i < raw.Length; i++)
        {
            var c = Rgba.FromBgra32(raw[i]);
            pixels[i] = c.A < 128 ? Rgba.Transparent : rgbaOrder ? new Rgba(c.B, c.G, c.R) : c with { A = 255 };
        }
        return new RgbaImage(size.Width, size.Height, pixels);
    }
}
