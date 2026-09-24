namespace PixelAniMaker.Core.Imaging;

/// <summary>A straight-alpha RGBA image, the exchange format with image codecs.</summary>
public sealed record RgbaImage(int Width, int Height, Rgba[] Pixels)
{
    public static RgbaImage Blank(int width, int height) => new(width, height, new Rgba[width * height]);
}

/// <summary>Encodes and decodes image files (PNG). Implemented by the UI layer.</summary>
public interface IImageCodec
{
    byte[] EncodePng(RgbaImage image);
    RgbaImage DecodePng(Stream stream);
}
