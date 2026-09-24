using System.Numerics;

namespace PixelAniMaker.Core.Imaging;

/// <summary>
/// RotSprite-style sampling: reads a pixel image at fractional positions from its 8x Scale2x
/// enlargement, so rotated edges stay clean and only existing palette indices appear.
/// </summary>
public sealed class RotSprite
{
    private readonly IndexedImage _up;

    public RotSprite(IndexedImage source)
    {
        Source = source;
        Version = source.Version;
        _up = Scale2x.Upscale8(source);
    }

    public IndexedImage Source { get; }

    /// <summary>The source version this sampler was built from (stale when it differs from Source.Version).</summary>
    public int Version { get; }

    /// <summary>Palette index at a source-image position (pixel units, fractional).</summary>
    public int Sample(Vector2 p)
    {
        int x = (int)MathF.Floor(p.X * 8), y = (int)MathF.Floor(p.Y * 8);
        return _up.InBounds(x, y) ? _up[x, y] : Palette.TransparentIndex;
    }

    /// <summary>
    /// A new image holding <paramref name="source"/> rotated by <paramref name="radians"/> around
    /// <paramref name="pivot"/> (clockwise on screen), plus where the pivot lands in it.
    /// </summary>
    public static (IndexedImage Image, Vector2 Pivot) Rotate(IndexedImage source, Vector2 pivot, float radians)
    {
        var sampler = new RotSprite(source);
        Vector2 Rot(Vector2 v, float a) => new(MathF.Cos(a) * v.X - MathF.Sin(a) * v.Y, MathF.Sin(a) * v.X + MathF.Cos(a) * v.Y);

        Vector2[] corners = [new(0, 0), new(source.Width, 0), new(0, source.Height), new(source.Width, source.Height)];
        var rotated = corners.Select(c => Rot(c - pivot, radians)).ToArray();
        const float Eps = 1e-4f; // cos/sin of right angles are not exact
        float minX = MathF.Floor(rotated.Min(p => p.X) + Eps), minY = MathF.Floor(rotated.Min(p => p.Y) + Eps);
        int w = (int)MathF.Ceiling(rotated.Max(p => p.X) - Eps - minX), h = (int)MathF.Ceiling(rotated.Max(p => p.Y) - Eps - minY);

        var result = new IndexedImage(Math.Max(1, w), Math.Max(1, h));
        var newPivot = new Vector2(-minX, -minY);
        for (int y = 0; y < result.Height; y++)
            for (int x = 0; x < result.Width; x++)
                result.Set(x, y, sampler.Sample(pivot + Rot(new Vector2(x + 0.5f, y + 0.5f) - newPivot, -radians)));
        return (result, newPivot);
    }
}
