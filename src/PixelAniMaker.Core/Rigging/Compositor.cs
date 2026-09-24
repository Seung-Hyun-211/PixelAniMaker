using System.Numerics;
using PixelAniMaker.Core.Imaging;

namespace PixelAniMaker.Core.Rigging;

/// <summary>Result of compositing: palette indices plus which part owns each canvas pixel.</summary>
public sealed class CompositeResult(int width, int height)
{
    public const short NoPart = -1;

    public int Width { get; } = width;
    public int Height { get; } = height;
    public ushort[] Indices { get; } = new ushort[width * height];

    /// <summary>Index into <see cref="Character.Parts"/>, or <see cref="NoPart"/>.</summary>
    public short[] Owners { get; } = Enumerable.Repeat(NoPart, width * height).ToArray();

    public short OwnerAt(int x, int y) =>
        (uint)x < (uint)Width && (uint)y < (uint)Height ? Owners[y * Width + x] : NoPart;
}

/// <summary>
/// Renders a posed character onto the canvas. Rotated parts are resampled RotSprite-style from an
/// 8x Scale2x image so edges stay clean and only palette colours are used.
/// </summary>
public sealed class Compositor
{
    private readonly Dictionary<Part, (int Version, IndexedImage Image)> _upscaled = [];

    public CompositeResult Compose(Character character)
    {
        var result = new CompositeResult(character.Width, character.Height);
        var transforms = character.ComputeTransforms();
        for (short i = 0; i < character.Parts.Count; i++)
            Draw(result, transforms[character.Parts[i]], i);
        return result;
    }

    private void Draw(CompositeResult target, PartTransform t, short owner)
    {
        var image = t.Part.Image;
        var (x0, y0, x1, y1) = CanvasBounds(t, target.Width, target.Height);
        Func<Vector2, int> sample = t.IsGridAligned ? p => SampleNearest(image, p) : Sampler8x(t.Part);

        for (int y = y0; y < y1; y++)
        {
            for (int x = x0; x < x1; x++)
            {
                int index = sample(t.ToLocal(new Vector2(x + 0.5f, y + 0.5f)));
                if (index == Palette.TransparentIndex)
                    continue;
                int i = y * target.Width + x;
                target.Indices[i] = (ushort)index;
                target.Owners[i] = owner;
            }
        }
    }

    private static (int X0, int Y0, int X1, int Y1) CanvasBounds(PartTransform t, int width, int height)
    {
        var img = t.Part.Image;
        Vector2[] corners = [new(0, 0), new(img.Width, 0), new(0, img.Height), new(img.Width, img.Height)];
        var pts = corners.Select(t.ToCanvas).ToArray();
        return (
            Math.Max(0, (int)MathF.Floor(pts.Min(p => p.X))),
            Math.Max(0, (int)MathF.Floor(pts.Min(p => p.Y))),
            Math.Min(width, (int)MathF.Ceiling(pts.Max(p => p.X))),
            Math.Min(height, (int)MathF.Ceiling(pts.Max(p => p.Y))));
    }

    private static int SampleNearest(IndexedImage image, Vector2 p)
    {
        int x = (int)MathF.Floor(p.X), y = (int)MathF.Floor(p.Y);
        return image.InBounds(x, y) ? image[x, y] : Palette.TransparentIndex;
    }

    private Func<Vector2, int> Sampler8x(Part part)
    {
        if (!_upscaled.TryGetValue(part, out var cached) || cached.Version != part.Image.Version)
        {
            cached = (part.Image.Version, Scale2x.Upscale8(part.Image));
            _upscaled[part] = cached;
        }
        var up = cached.Image;
        return p => SampleNearest(up, p * 8);
    }
}
