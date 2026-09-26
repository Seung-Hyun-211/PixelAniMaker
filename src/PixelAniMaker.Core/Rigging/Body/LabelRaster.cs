using System.Numerics;
using PixelAniMaker.Core.Imaging;

namespace PixelAniMaker.Core.Rigging.Body;

/// <summary>A filled area in canvas pixels (continuous coordinates, pixel x covers [x, x+1)).</summary>
internal interface IShape
{
    (float X0, float Y0, float X1, float Y1) Bounds { get; }
    bool Contains(float x, float y);
}

internal sealed class PolygonShape : IShape
{
    private readonly Vector2[] _points;

    public PolygonShape(IEnumerable<Vector2> points)
    {
        _points = [.. points];
        Bounds = (_points.Min(p => p.X), _points.Min(p => p.Y), _points.Max(p => p.X), _points.Max(p => p.Y));
    }

    public (float X0, float Y0, float X1, float Y1) Bounds { get; }

    public bool Contains(float x, float y)
    {
        bool inside = false;
        for (int i = 0, j = _points.Length - 1; i < _points.Length; j = i++)
        {
            var (a, b) = (_points[i], _points[j]);
            if ((a.Y > y) != (b.Y > y) && x < (b.X - a.X) * (y - a.Y) / (b.Y - a.Y) + a.X)
                inside = !inside;
        }
        return inside;
    }
}

internal sealed class EllipseShape(Vector2 centre, float rx, float ry) : IShape
{
    public (float X0, float Y0, float X1, float Y1) Bounds => (centre.X - rx, centre.Y - ry, centre.X + rx, centre.Y + ry);

    public bool Contains(float x, float y)
    {
        float dx = (x - centre.X) / rx, dy = (y - centre.Y) / ry;
        return dx * dx + dy * dy <= 1;
    }
}

/// <summary>A segment whose radius goes from <paramref name="r1"/> to <paramref name="r2"/>, with round ends.</summary>
internal sealed class CapsuleShape(Vector2 p1, float r1, Vector2 p2, float r2) : IShape
{
    public (float X0, float Y0, float X1, float Y1) Bounds { get; } = (
        Math.Min(p1.X - r1, p2.X - r2), Math.Min(p1.Y - r1, p2.Y - r2),
        Math.Max(p1.X + r1, p2.X + r2), Math.Max(p1.Y + r1, p2.Y + r2));

    public bool Contains(float x, float y)
    {
        var d = p2 - p1;
        var p = new Vector2(x, y);
        float len2 = d.LengthSquared();
        float t = len2 == 0 ? 0 : Math.Clamp(Vector2.Dot(p - p1, d) / len2, 0, 1);
        return Vector2.Distance(p, p1 + d * t) <= r1 + (r2 - r1) * t;
    }
}

/// <summary>
/// Paints shapes as numbered areas (later ones on top) and turns them into mannequin pixels the way the
/// hand-made template does: outline next to empty space, an inner line where a shape meets an earlier one,
/// a shaded rim on the right and darker ball joints.
/// </summary>
internal sealed class LabelRaster(int width, int height)
{
    public static readonly Rgba Fill = new(222, 206, 184);
    public static readonly Rgba Shade = new(190, 170, 146);
    public static readonly Rgba Joint = new(176, 150, 120);
    public static readonly Rgba JointShade = new(150, 126, 98);
    public static readonly Rgba Inner = new(120, 96, 74);
    public static readonly Rgba Outline = new(52, 40, 34);

    private const int Samples = 4;
    private readonly int[] _labels = new int[width * height];
    private readonly List<bool> _isJoint = [false];

    /// <summary>Pixels at least half covered by the shape take a new label.</summary>
    public void Add(IShape shape, bool joint = false)
    {
        int label = _isJoint.Count;
        _isJoint.Add(joint);
        var (bx0, by0, bx1, by1) = shape.Bounds;
        int x0 = Math.Max(0, (int)Math.Floor(bx0)), y0 = Math.Max(0, (int)Math.Floor(by0));
        int x1 = Math.Min(width - 1, (int)Math.Ceiling(bx1)), y1 = Math.Min(height - 1, (int)Math.Ceiling(by1));
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int hits = 0;
                for (int j = 0; j < Samples; j++)
                    for (int i = 0; i < Samples; i++)
                        if (shape.Contains(x + (i + 0.5f) / Samples, y + (j + 0.5f) / Samples))
                            hits++;
                if (hits * 2 >= Samples * Samples)
                    _labels[y * width + x] = label;
            }
    }

    public RgbaImage Render(int rimWidth)
    {
        var image = RgbaImage.Blank(width, height);
        int At(int x, int y) => x >= 0 && y >= 0 && x < width && y < height ? _labels[y * width + x] : 0;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int l = At(x, y);
                if (l == 0)
                    continue;
                int[] around = [At(x + 1, y), At(x - 1, y), At(x, y + 1), At(x, y - 1)];
                bool rim = Enumerable.Range(1, rimWidth).Any(k => At(x + k, y) != l);
                image.Pixels[y * width + x] =
                    around.Contains(0) ? Outline
                    : around.Any(n => n < l) ? Inner
                    : _isJoint[l] ? (rim ? JointShade : Joint)
                    : rim ? Shade : Fill;
            }
        return image;
    }
}
