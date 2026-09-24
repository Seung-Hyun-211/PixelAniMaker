namespace PixelAniMaker.Core.Imaging;

/// <summary>
/// Scale2x (EPX) pixel-art upscaler. Applied three times it gives the 8x image that RotSprite-style
/// rotation samples from, so rotated edges stay clean and no new colours appear.
/// </summary>
public static class Scale2x
{
    public static IndexedImage Upscale(IndexedImage src)
    {
        var dst = new IndexedImage(src.Width * 2, src.Height * 2);
        int w = src.Width, h = src.Height;
        int At(int x, int y) => src[Math.Clamp(x, 0, w - 1), Math.Clamp(y, 0, h - 1)];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int p = At(x, y), a = At(x, y - 1), b = At(x + 1, y), c = At(x - 1, y), d = At(x, y + 1);
                dst.Set(2 * x, 2 * y, c == a && c != d && a != b ? a : p);
                dst.Set(2 * x + 1, 2 * y, a == b && a != c && b != d ? b : p);
                dst.Set(2 * x, 2 * y + 1, d == c && d != b && c != a ? c : p);
                dst.Set(2 * x + 1, 2 * y + 1, b == d && b != a && d != c ? d : p);
            }
        }
        return dst;
    }

    /// <summary>Three passes: 8x.</summary>
    public static IndexedImage Upscale8(IndexedImage src) => Upscale(Upscale(Upscale(src)));
}
