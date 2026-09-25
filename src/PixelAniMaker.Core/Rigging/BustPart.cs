using System.Numerics;
using PixelAniMaker.Core.Imaging;

namespace PixelAniMaker.Core.Rigging;

public enum BustSize
{
    Small,
    Medium,
    Large,
}

/// <summary>
/// Optional bust part for female characters: <c>bust</c>, a detail child of the chest, so it shares the
/// chest's outline and shading and moves with it. The starting drawing is a teardrop from the collarbone
/// (0.10 H of the front chest image) to the lower ribs (0.86 H): flat at the top, fullest at 68%, round at
/// the bottom — the front width and the side protrusion follow the same curve (doc/plan-bust-part.md).
/// </summary>
public static class BustPart
{
    public const string Name = "bust";
    public const string ChestName = "chest";

    private const float Top = 0.10f, Bottom = 0.86f, Fullest = 0.68f;

    public static bool Has(Character character) => character.Find(Name) is not null;

    public static bool CanAdd(Character character) => !Has(character) && character.Find(ChestName) is not null;

    /// <summary>Adds the bust with a starting drawing in every stored direction, as one undo step.</summary>
    public static void Add(Character character, BustSize size)
    {
        if (!CanAdd(character))
            return;
        var chest = character.Find(ChestName)!;
        OptionalParts.Add(character, "가슴 볼륨 추가", [(Create(character, chest, size), chest)]);
    }

    public static void Remove(Character character) => OptionalParts.Remove(character, "가슴 볼륨 삭제", Name);

    /// <summary>Thickness along the teardrop, 0 at the collarbone and the ribs, 1 at the fullest point.</summary>
    public static float Profile(float t) =>
        t <= 0 || t >= 1 ? 0
        : t < Fullest ? MathF.Pow(t / Fullest, 1.5f)
        : MathF.Sqrt(1 - Square((t - Fullest) / (1 - Fullest)));

    private static float Scale(BustSize size) => size switch
    {
        BustSize.Small => 0.85f,
        BustSize.Large => 1.2f,
        _ => 1f,
    };

    private static Part Create(Character character, Part chest, BustSize size)
    {
        var front = Mask(chest.View(Direction.Front));
        float w = front.Keys.Max(p => p.X) - front.Keys.Min(p => p.X) + 1;
        float top = front.Keys.Min(p => p.Y), h = front.Keys.Max(p => p.Y) - top + 1;
        float y0 = top + Top * h, y1 = top + Bottom * h;
        float k = Scale(size), halfWidth = 0.19f * w * k, depth = 0.22f * w * k;

        int skin = chest.View(Direction.Front).MainColour()!.Value;
        int line = ShadingPass.DarkerColours(character.Palette, new HashSet<int>()) is var d && skin < d.Length && d[skin] is { } dark
            ? dark
            : character.Palette.GetOrAdd(Darken(character.Palette[skin]));

        var views = new Dictionary<Direction, PartView>();
        foreach (var direction in character.StoredDirections)
        {
            var chestView = chest.View(direction);
            var mask = Mask(chestView);
            var rows = mask.Keys.GroupBy(p => p.Y).ToDictionary(g => g.Key, g => (L: g.Min(p => p.X), R: g.Max(p => p.X)));
            (int L, int R) Edge(float y) => rows[rows.Keys.MinBy(r => MathF.Abs(r - y))];
            var mid = Edge((y0 + y1) / 2);
            float cx = (mid.L + mid.R) / 2f;

            // shapes: a front-facing drop (centre, outward drift) or a side profile off the front edge
            var drops = new List<(float X, float Out)>();
            float side = 0;
            bool outsideOnly = false;
            switch (direction)
            {
                case Direction.Front:
                    drops.Add((cx - 0.20f * w, -1));
                    drops.Add((cx + 0.20f * w, 1));
                    break;
                case Direction.Left:
                    side = 1;
                    break;
                case Direction.FrontLeft:
                    side = 0.8f;
                    drops.Add((cx - 0.06f * w, 1));
                    break;
                case Direction.BackLeft:
                    side = 0.6f;
                    outsideOnly = true;
                    break;
            }

            var pixels = new Dictionary<(int X, int Y), bool>();   // value: inside the chest
            for (int y = (int)MathF.Floor(y0); y <= (int)MathF.Ceiling(y1) + 1; y++)
            {
                float t = (y + 0.5f - y0) / (y1 - y0), p = Profile(t);
                if (p <= 0)
                    continue;
                foreach (var (x0, outward) in drops)
                {
                    float c = x0 + outward * 0.04f * w * t, half = halfWidth * p;
                    for (int x = (int)MathF.Floor(c - half); x < (int)MathF.Ceiling(c + half); x++)
                        pixels[(x, y)] = mask.ContainsKey((x, y));
                }
                if (side > 0)
                {
                    int edge = Edge(y).L;
                    for (int x = (int)MathF.Floor(edge - depth * side * p); x <= edge + 2; x++)
                        if (!outsideOnly || !mask.ContainsKey((x, y)))
                            pixels[(x, y)] = mask.ContainsKey((x, y));
                }
            }
            views[direction] = View(pixels, skin, line, y0, y1, outsideOnly, new Vector2(cx, (y0 + y1) / 2), chestView.DrawOrder);
        }
        return new Part(Name, "가슴 볼륨", views) { IsDetail = true, Secondary = Animation.SecondarySettings.Bust };
    }

    /// <summary>The drawing, with a line under the round bottom where it lies on the chest.</summary>
    private static PartView View(Dictionary<(int X, int Y), bool> pixels, int skin, int line, float y0, float y1,
        bool noLine, Vector2 joint, int drawOrder)
    {
        if (pixels.Count == 0)
        {
            var blank = new Vector2(MathF.Round(joint.X) - 2, MathF.Round(joint.Y) - 2);
            return new PartView(new IndexedImage(4, 4), blank, joint, drawOrder);
        }
        int minX = pixels.Keys.Min(p => p.X), minY = pixels.Keys.Min(p => p.Y);
        int maxX = pixels.Keys.Max(p => p.X), maxY = pixels.Keys.Max(p => p.Y);
        var image = new IndexedImage(maxX - minX + 1, maxY - minY + 1);
        foreach (var ((x, y), onChest) in pixels)
        {
            float t = (y + 0.5f - y0) / (y1 - y0);
            bool bottomEdge = t > Fullest && !pixels.ContainsKey((x, y + 1));
            image.Set(x - minX, y - minY, bottomEdge && onChest && !noLine ? line : skin);
        }
        return new PartView(image, new Vector2(minX, minY), joint, drawOrder);
    }

    /// <summary>Drawn pixels of a view in character coordinates → palette index.</summary>
    private static Dictionary<(int X, int Y), int> Mask(PartView view) =>
        view.DrawnPixels().ToDictionary(p => (p.X, p.Y), p => p.Index);

    private static Rgba Darken(Rgba c) => new((byte)(c.R * 3 / 4), (byte)(c.G * 3 / 4), (byte)(c.B * 3 / 4));

    private static float Square(float v) => v * v;
}
