using System.Numerics;
using PixelAniMaker.Core.Animation;
using PixelAniMaker.Core.Imaging;

namespace PixelAniMaker.Core.Rigging;

public enum CustomPartKind
{
    Hair,
    Tail,
    Cape,
    Accessory,
}

/// <summary>Where an added part is drawn relative to the body.</summary>
public enum CustomPlacement
{
    /// <summary>Just in front of the parent in every direction.</summary>
    Front,

    /// <summary>Just behind the parent in every direction.</summary>
    Behind,

    /// <summary>On the back: behind everything when the body faces the viewer or the side, in front of everything from behind.</summary>
    BackSide,
}

/// <param name="Segments">Chain links (1–4); each link is a part, the child of the one before.</param>
/// <param name="Length">Whole length in pixels, over all links (null = the kind's default for the canvas).</param>
public sealed record CustomPartOptions(CustomPartKind Kind, CustomPlacement Placement, int Segments, int? Length = null);

/// <summary>
/// Parts the user adds to the skeleton (doc/plan-custom-parts.md): hair strands, tails, cape tails and
/// accessories hanging from a parent part. Each starts with a simple shape in the parent's main colour in
/// every stored direction; chains get one part per link. Adding and removing are single undo steps.
/// </summary>
public static class CustomParts
{
    public const int MaxSegments = 4;

    /// <summary>Default segments for a kind (accessories and capes are one piece).</summary>
    public static int DefaultSegments(CustomPartKind kind) => kind switch
    {
        CustomPartKind.Hair => 2,
        CustomPartKind.Tail => 3,
        _ => 1,
    };

    /// <summary>Default placement for a kind.</summary>
    public static CustomPlacement DefaultPlacement(CustomPartKind kind) => kind switch
    {
        CustomPartKind.Tail or CustomPartKind.Cape => CustomPlacement.BackSide,
        _ => CustomPlacement.Front,
    };

    /// <summary>Default whole length for a kind on a canvas <paramref name="height"/> pixels high.</summary>
    public static int DefaultLength(CustomPartKind kind, int height) => Math.Max(4, (int)MathF.Round(height * kind switch
    {
        CustomPartKind.Hair => 0.18f,
        CustomPartKind.Tail => 0.22f,
        CustomPartKind.Cape => 0.45f,
        _ => 0.05f,
    }));

    public static SecondarySettings? DefaultSecondary(CustomPartKind kind) => kind switch
    {
        CustomPartKind.Hair => SecondarySettings.Hair,
        CustomPartKind.Tail or CustomPartKind.Cape => SecondarySettings.Cloth,
        _ => null,
    };

    private static string Prefix(CustomPartKind kind) => kind switch
    {
        CustomPartKind.Hair => "hair",
        CustomPartKind.Tail => "tail",
        CustomPartKind.Cape => "cape",
        _ => "acc",
    };

    private static string KindLabel(CustomPartKind kind) => kind switch
    {
        CustomPartKind.Hair => "머리카락",
        CustomPartKind.Tail => "꼬리",
        CustomPartKind.Cape => "망토",
        _ => "장신구",
    };

    /// <summary>
    /// Adds the part (all its links) under <paramref name="parent"/> as one undo step and returns the links,
    /// first one first. Accessories are always one link.
    /// </summary>
    public static IReadOnlyList<Part> Add(Character character, Part parent, CustomPartOptions options)
    {
        int segments = options.Kind == CustomPartKind.Accessory ? 1 : Math.Clamp(options.Segments, 1, MaxSegments);
        int length = Math.Clamp(options.Length ?? DefaultLength(options.Kind, character.Height), 4, Math.Max(4, character.Height));
        int number = NextNumber(character, options.Kind);
        string name = $"{Prefix(options.Kind)}{number}", label = $"{KindLabel(options.Kind)} {number}";

        var shapes = character.StoredDirections.ToDictionary(d => d, d => Shape.For(character, parent, d, options, length));
        var links = new List<(Part Part, Part Parent)>();
        var colour = MainColour(character, parent);
        for (int i = 0; i < segments; i++)
        {
            var views = shapes.ToDictionary(kv => kv.Key, kv => kv.Value.Link(i, segments, colour));
            var part = new Part(i == 0 ? name : $"{name}_{i + 1}", i == 0 ? label : $"{label}-{i + 1}", views)
            {
                IsCustom = true,
                Secondary = DefaultSecondary(options.Kind),
            };
            links.Add((part, i == 0 ? parent : links[^1].Part));
        }
        DetailParts.Add(character, $"{KindLabel(options.Kind)} 추가", links);
        return links.Select(l => l.Part).ToList();
    }

    /// <summary>True when <paramref name="part"/> was added by the user (and can be removed).</summary>
    public static bool CanRemove(Part part) => part.IsCustom && part.Parent is not null;

    /// <summary>Removes an added part and the links under it as one undo step; false for template parts.</summary>
    public static bool Remove(Character character, Part part)
    {
        if (!CanRemove(part))
            return false;
        var subtree = Subtree(part).ToList();
        if (subtree.Any(p => !p.IsCustom))
            return false;
        var change = new PartsChange(character, $"{part.Label} 삭제", subtree.Select(p => (p, p.Parent!)).ToList(), adding: false);
        change.Redo();
        character.History.Push(change);
        return true;
    }

    private static IEnumerable<Part> Subtree(Part part)
    {
        yield return part;
        foreach (var child in part.Children)
            foreach (var p in Subtree(child))
                yield return p;
    }

    private static int NextNumber(Character character, CustomPartKind kind)
    {
        string prefix = Prefix(kind);
        int n = 1;
        while (character.Parts.Any(p => p.Name == $"{prefix}{n}" || p.Name.StartsWith($"{prefix}{n}_", StringComparison.Ordinal)))
            n++;
        return n;
    }

    /// <summary>The parent's most used colour (front view), or a neutral grey when it is empty.</summary>
    private static int MainColour(Character character, Part parent)
    {
        var image = parent.View(Direction.Front).Image;
        var used = image.Pixels.ToArray().Where(i => i != Palette.TransparentIndex).GroupBy(i => (int)i).MaxBy(g => g.Count());
        return used?.Key ?? character.Palette.GetOrAdd(new Rgba(170, 170, 184));
    }

    /// <summary>The whole starting shape in one direction: where it hangs, which way, how wide; cut into links.</summary>
    private sealed record Shape(CustomPartKind Kind, Vector2 Anchor, Vector2 Down, float Length, float TopHalf, float BottomHalf, int Order)
    {
        public static Shape For(Character character, Part parent, Direction direction, CustomPartOptions options, int length)
        {
            var view = parent.View(direction);
            var drawn = Drawn(view);
            float minX = drawn.Min(p => p.X), maxX = drawn.Max(p => p.X) + 1, minY = drawn.Min(p => p.Y), maxY = drawn.Max(p => p.Y) + 1;
            float cx = (minX + maxX) / 2, cy = (minY + maxY) / 2, width = maxX - minX;
            bool back = options.Placement == CustomPlacement.BackSide;

            float height = maxY - minY;
            float y = options.Kind switch
            {
                CustomPartKind.Cape => minY + 2,
                CustomPartKind.Accessory when !back => cy + 0.2f * height,   // an earring below the ear
                _ => cy,
            };
            float edge = drawn.Where(p => Math.Abs(p.Y + 0.5f - y) < 1).Select(p => p.X + 1f).DefaultIfEmpty(maxX).Max();
            float x = back
                ? direction switch
                {
                    Direction.Left => edge - 1,
                    Direction.FrontLeft => (cx + edge) / 2,
                    _ => cx,
                }
                : options.Kind is CustomPartKind.Hair or CustomPartKind.Accessory
                    // a side lock or earring: at the side of the face, by the ear in the side views
                    ? direction switch
                    {
                        Direction.Left => cx + 0.12f * width,
                        Direction.FrontLeft => cx + 0.08f * width,
                        _ => minX + (options.Kind == CustomPartKind.Hair ? 0.18f : 0.08f) * width,
                    }
                    : cx;

            // hanging straight down; back pieces lean towards the back in the side (and half as much in the front-3/4) view
            float lean = !back ? 0 : options.Kind switch
            {
                CustomPartKind.Tail => 45,
                CustomPartKind.Cape => 15,
                CustomPartKind.Hair => 10,
                _ => 0,
            } * direction switch
            {
                Direction.Left => 1f,
                Direction.FrontLeft => 0.5f,
                _ => 0f,
            };
            float rad = lean * MathF.PI / 180;
            var down = new Vector2(MathF.Sin(rad), MathF.Cos(rad));

            // widths (half), from the joint to the end
            float side = direction switch
            {
                Direction.Left => 0.3f,
                Direction.FrontLeft or Direction.BackLeft => 0.7f,
                _ => 1f,
            };
            (float top, float bottom) = options.Kind switch
            {
                CustomPartKind.Hair => (Math.Max(1.5f, length * 0.12f), 0.8f),
                CustomPartKind.Tail => (Math.Max(2f, length * 0.14f), Math.Max(1.5f, length * 0.08f)),
                CustomPartKind.Cape => (Math.Max(2f, 0.45f * width * side), Math.Max(3f, 0.62f * width * side)),
                _ => (length / 2f, length / 2f),
            };

            var orders = character.Parts.Select(p => p.View(direction).DrawOrder).ToList();
            bool facingAway = direction is Direction.Back or Direction.BackLeft;
            int order = options.Placement switch
            {
                CustomPlacement.Front => view.DrawOrder,
                CustomPlacement.Behind => view.DrawOrder - 1,
                _ => facingAway ? orders.Max() + 1 : orders.Min() - 1,
            };
            return new Shape(options.Kind, new Vector2(x, y), down, length, top, bottom, order);
        }

        /// <summary>Link <paramref name="index"/> of <paramref name="count"/>: its pixels, joint at the link's start.</summary>
        public PartView Link(int index, int count, int colour)
        {
            float t0 = index / (float)count, t1 = (index + 1) / (float)count;
            var joint = Anchor + Down * Length * t0;
            var end = Anchor + Down * Length * t1;
            Func<Vector2, bool> inside = Kind switch
            {
                CustomPartKind.Accessory => p => Vector2.DistanceSquared(p, Anchor + Down * TopHalf) <= TopHalf * TopHalf,
                CustomPartKind.Cape => p => InCape(p, t0, t1),
                _ => p => InCapsule(p, joint, end, Half(t0), Half(t1)),
            };
            float reach = Math.Max(TopHalf, BottomHalf) + 2;
            int x0 = (int)MathF.Floor(Math.Min(joint.X, end.X) - reach), x1 = (int)MathF.Ceiling(Math.Max(joint.X, end.X) + reach);
            int y0 = (int)MathF.Floor(Math.Min(joint.Y, end.Y) - reach), y1 = (int)MathF.Ceiling(Math.Max(joint.Y, end.Y) + reach);
            var pixels = new List<(int X, int Y)>();
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    if (inside(new Vector2(x + 0.5f, y + 0.5f)))
                        pixels.Add((x, y));
            if (pixels.Count == 0)
                pixels.Add(((int)MathF.Floor(joint.X), (int)MathF.Floor(joint.Y)));

            int minX = pixels.Min(p => p.X), minY = pixels.Min(p => p.Y);
            var image = new IndexedImage(pixels.Max(p => p.X) - minX + 1, pixels.Max(p => p.Y) - minY + 1);
            foreach (var (x, y) in pixels)
                image.Set(x - minX, y - minY, colour);
            return new PartView(image, new Vector2(minX, minY), joint, Order);
        }

        private float Half(float t) => TopHalf + (BottomHalf - TopHalf) * t;

        /// <summary>Inside the widening cape between t0 and t1 of its length (one pixel of overlap between links).</summary>
        private bool InCape(Vector2 p, float t0, float t1)
        {
            var d = p - Anchor;
            float along = Vector2.Dot(d, Down), across = MathF.Abs(d.X * Down.Y - d.Y * Down.X);
            float lo = Math.Max(0, t0 * Length - (t0 > 0 ? 1 : 0)), hi = t1 * Length;
            return along >= lo && along <= hi && across <= Half(along / Length);
        }

        /// <summary>Inside a capsule from a to b whose radius goes from ra to rb.</summary>
        private static bool InCapsule(Vector2 p, Vector2 a, Vector2 b, float ra, float rb)
        {
            var ab = b - a;
            float t = Math.Clamp(Vector2.Dot(p - a, ab) / Math.Max(1e-6f, ab.LengthSquared()), 0, 1);
            float r = ra + (rb - ra) * t;
            return Vector2.DistanceSquared(p, a + ab * t) <= r * r;
        }

        /// <summary>Drawn pixels of a view in canvas coordinates (the whole image when nothing is drawn).</summary>
        private static List<(int X, int Y)> Drawn(PartView view)
        {
            var image = view.Image;
            int ox = (int)view.RestPosition.X, oy = (int)view.RestPosition.Y;
            var list = new List<(int X, int Y)>();
            for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                    if (image[x, y] != Palette.TransparentIndex)
                        list.Add((ox + x, oy + y));
            if (list.Count == 0)
                list.AddRange([(ox, oy), (ox + image.Width - 1, oy + image.Height - 1)]);
            return list;
        }
    }
}
