using System.Numerics;
using PixelAniMaker.Core.Imaging;

namespace PixelAniMaker.Core.Rigging;

/// <summary>
/// Parts held in the right hand (guns, swords): built from one side-profile picture pointing left, turned so the
/// business end hangs down at rest, grip on the hand's palm, mirrored when seen from behind. Clips turn it from
/// there — in the side view +90° points it forward. Drawn over the chest (and the right forearm) from the front and
/// side, under the head and the near arm, behind the body from the back.
/// </summary>
internal static class HeldItem
{
    public const string HandName = "hand_r";

    /// <param name="points">Named points of the profile (pixel centres), kept as attachment points of every view.</param>
    public static Part Create(Character character, Part hand, string name, string label, IndexedImage profile, Vector2 grip,
        IReadOnlyList<(string Name, Vector2 Point)> points)
    {
        var down = TurnDown(profile);
        Vector2 Down(Vector2 p) => new(p.Y, profile.Width - p.X);   // a point of the profile after turning
        var gripDown = Down(grip);
        var views = new Dictionary<Direction, PartView>();
        foreach (var direction in character.StoredDirections)
        {
            var pivot = Palm(hand.View(direction));
            bool behind = direction is Direction.Back or Direction.BackLeft;
            var image = behind ? Mirror(down) : down;
            var local = behind ? new Vector2(image.Width - gripDown.X, gripDown.Y) : gripDown;
            var position = new Vector2(MathF.Round(pivot.X - local.X), MathF.Round(pivot.Y - local.Y));
            var view = new PartView(image, position, position + local, Order(character, direction));
            foreach (var (pointName, point) in points)
            {
                var p = Down(point);
                view.Attachments.Set(pointName, behind ? new Vector2(image.Width - p.X, p.Y) : p);
            }
            views[direction] = view;
        }
        return new Part(name, label, views) { IsCustom = true };
    }

    /// <summary>The palm of a hand view at rest: across its middle, a little below its middle along it (canvas).</summary>
    public static Vector2 Palm(PartView hand)
    {
        var pixels = hand.DrawnPixels().ToList();
        return pixels.Count == 0 ? hand.RestPivot
            : new Vector2((float)pixels.Average(p => p.X + 0.5), pixels.Min(p => p.Y) + 0.55f * (pixels.Max(p => p.Y) - pixels.Min(p => p.Y) + 1));
    }

    /// <summary>The body's height at rest (front view), which sets held items' sizes.</summary>
    public static int BodyHeight(Character character)
    {
        var ys = character.Parts.SelectMany(p => p.View(Direction.Front).DrawnPixels()).Select(p => p.Y).ToList();
        return ys.Count == 0 ? character.Height : ys.Max() - ys.Min() + 1;
    }

    /// <summary>Over the chest (and the right forearm) in front and side views, behind everything from the back.</summary>
    private static int Order(Character character, Direction direction)
    {
        int Of(string part) => character.Find(part)?.View(direction).DrawOrder ?? 0;
        return direction is Direction.Back or Direction.BackLeft
            ? character.Parts.Min(p => p.View(direction).DrawOrder) - 1
            : Math.Max(Of("chest"), Of("forearm_r"));
    }

    /// <summary>A quarter turn anticlockwise on screen: the left end ends up at the bottom, the top forward.</summary>
    private static IndexedImage TurnDown(IndexedImage src)
    {
        var dst = new IndexedImage(src.Height, src.Width);
        for (int y = 0; y < src.Height; y++)
            for (int x = 0; x < src.Width; x++)
                dst.Set(y, src.Width - 1 - x, src[x, y]);
        return dst;
    }

    private static IndexedImage Mirror(IndexedImage src)
    {
        var dst = new IndexedImage(src.Width, src.Height);
        for (int y = 0; y < src.Height; y++)
            for (int x = 0; x < src.Width; x++)
                dst.Set(src.Width - 1 - x, y, src[x, y]);
        return dst;
    }

    /// <summary>Where a named point of a held item is at rest, as an offset from its grip (canvas pixels), or null.</summary>
    public static Vector2? Offset(PartView view, string point) =>
        view.Attachments.All.TryGetValue(point, out var p) ? view.RestPosition + p - view.RestPivot : null;
}

/// <summary>Two-bone inverse kinematics for the arms (upper arm and forearm), elbow down.</summary>
internal static class ArmIk
{
    public static float Length(Character c, Direction d, string side, Vector2 handPoint)
    {
        var s = c.Find("upper_arm_" + side)!.View(d).RestPivot;
        var e = c.Find("forearm_" + side)!.View(d).RestPivot;
        return Vector2.Distance(s, e) + Vector2.Distance(e, handPoint);
    }

    /// <summary>
    /// Turns the upper arm and forearm of one side so that <paramref name="handPoint"/> (a point on the hand at rest,
    /// canvas) lands on <paramref name="target"/>, elbow down; the hand lines up with the forearm. Out of reach, the
    /// arm points straight at the target.
    /// </summary>
    public static void Reach(Character c, Direction d, IReadOnlyDictionary<Part, PartTransform> t, string side, Vector2 target,
        Vector2 handPoint, Dictionary<string, double> rotations)
    {
        var upper = c.Find("upper_arm_" + side)!;
        var fore = c.Find("forearm_" + side)!;
        var s0 = upper.View(d).RestPivot;
        var e0 = fore.View(d).RestPivot;
        Vector2 u0 = e0 - s0, f0 = handPoint - e0;
        float l1 = u0.Length(), l2 = f0.Length();
        var shoulder = t[upper].Pivot;
        float parent = upper.Parent is { } p ? t[p].Angle : 0;

        var toTarget = target - shoulder;
        float dist = Math.Clamp(toTarget.Length(), MathF.Abs(l1 - l2) + 0.01f, l1 + l2 - 0.01f);
        float toward = MathF.Atan2(toTarget.Y, toTarget.X);
        float bend = MathF.Acos(Math.Clamp((l1 * l1 + dist * dist - l2 * l2) / (2 * l1 * dist), -1, 1));
        // of the two elbows, the lower one
        var elbowA = shoulder + l1 * new Vector2(MathF.Cos(toward + bend), MathF.Sin(toward + bend));
        var elbowB = shoulder + l1 * new Vector2(MathF.Cos(toward - bend), MathF.Sin(toward - bend));
        var elbow = elbowA.Y >= elbowB.Y ? elbowA : elbowB;
        var hand = shoulder + Vector2.Normalize(toTarget) * dist;

        float upperAbs = Angle(elbow - shoulder) - Angle(u0);
        float foreAbs = Angle(hand - elbow) - Angle(f0);
        rotations[upper.Name] = Degrees(upperAbs - parent);
        rotations[fore.Name] = Degrees(foreAbs - upperAbs);
        rotations["hand_" + side] = 0;
    }

    private static float Angle(Vector2 v) => MathF.Atan2(v.Y, v.X);

    public static double Degrees(float radians) => Math.Round(Pose.Normalize(radians * 180 / Math.PI), 1);
}
