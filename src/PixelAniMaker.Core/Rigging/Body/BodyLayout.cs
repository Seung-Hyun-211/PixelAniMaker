using System.Numerics;
using View = PixelAniMaker.Core.Rigging.Body.MannequinBuilder.View;

namespace PixelAniMaker.Core.Rigging.Body;

/// <summary>
/// Where the mannequin's joints are in 3D pixels (x: towards the character's left, y: down from the canvas
/// top, z: forward; x = 0 is the centre line) and the shapes of each part as seen from a <see cref="View"/>.
/// </summary>
internal sealed class BodyLayout
{
    /// <summary>A row of a lofted part: half width across, depth to the front and to the back.</summary>
    private readonly record struct Section(float Y, float Across, float Front, float Back);

    private readonly BodyProportions _p;
    private readonly bool _discs;
    private readonly float _u;
    private readonly int _top;
    private readonly Dictionary<string, Vector3> _joints = [];

    // key heights (pixels)
    private readonly float _chestTop, _chestBottom, _waistBottom, _crotch;

    public BodyLayout(BodyProportions p, bool jointDiscs)
    {
        _p = p;
        _discs = jointDiscs;
        _u = p.HeadPixels;
        // room for the default clips: arms raised above the head and swung out to the sides (longer on taller bodies)
        _top = MannequinBuilder.Margin + (int)Math.Ceiling(Math.Max(0, p.Arm - 1 - p.Neck) * 0.5 * _u);
        Height = _top + (int)Math.Ceiling(p.BodyHeightPixels) + MannequinBuilder.Margin;
        Width = RoundUp8(Math.Max(0.7 * Height, 1.7 * (p.ShoulderHalf + p.Arm) * _u + 2 * MannequinBuilder.Margin));
        RimWidth = Math.Max(1, (int)Math.Round(_u / 15));

        _chestTop = Y(1 + p.Neck);
        _chestBottom = _chestTop + U(p.Chest);
        _waistBottom = _chestBottom + U(p.Waist);
        _crotch = _waistBottom + U(p.Pelvis);
        float hip = _waistBottom + U(0.45 * p.Pelvis);
        float ankle = Y(p.Heads - p.FootHeight);
        float knee = (hip + ankle) / 2 - U(0.02);
        float shoulder = _chestTop + U(0.8 * p.UpperArmRadius);
        float elbow = shoulder + U(p.UpperArm), wrist = elbow + U(p.Forearm);

        _joints["pelvis"] = new(0, _waistBottom + U(0.2 * p.Pelvis), 0);
        _joints["waist"] = new(0, _waistBottom, 0);
        _joints["chest"] = new(0, _chestBottom, 0);
        _joints["head"] = new(0, _chestTop, 0);
        float hipX = U(0.55 * p.HipHalf), s = U(p.ShoulderHalf);
        foreach (var (side, sign) in new[] { ("r", -1f), ("l", 1f) })
        {
            // the far (right) limbs sit a little back so they peek out behind the near ones from the side
            float back = side == "r" ? -0.05f * _u : 0;
            _joints["thigh_" + side] = new(sign * hipX, hip, back);
            _joints["shin_" + side] = new(sign * hipX * 0.92f, knee, back);
            _joints["foot_" + side] = new(sign * hipX * 0.88f, ankle, back);
            _joints["upper_arm_" + side] = new(sign * s, shoulder, back);
            _joints["forearm_" + side] = new(sign * (s + U(0.06)), elbow, back - U(0.03));
            _joints["hand_" + side] = new(sign * (s + U(0.09)), wrist, back + U(0.02));
        }
    }

    public int Width { get; }
    public int Height { get; }
    public int RimWidth { get; }

    public Vector3 Pivot(string part) => _joints[part];

    private float U(double heads) => (float)(heads * _u);

    private float Y(double heads) => _top + U(heads);

    private static int RoundUp8(double v) => (int)Math.Ceiling(v / 8) * 8;

    /// <summary>The part's shapes back to front; the flag marks a ball joint.</summary>
    public IEnumerable<(IShape Shape, bool Joint)> Shapes(string part, View view)
    {
        string bone = part.EndsWith("_r") || part.EndsWith("_l") ? part[..^2] : part;
        string side = part[^1..];
        return bone switch
        {
            "head" => Head(view),
            "chest" or "waist" or "pelvis" => [(Loft(TorsoSections(bone), view), false)],
            "thigh" => [(Limb(view, part, "shin_" + side, _p.ThighRadius, 0.7 * _p.ThighRadius), false)],
            "shin" => WithDisc(view, part, 0.8f * U(0.72 * _p.ThighRadius),
                Limb(view, part, "foot_" + side, 0.72 * _p.ThighRadius, 0.45 * _p.ThighRadius)),
            "foot" => WithDisc(view, part, 0.8f * U(0.45 * _p.ThighRadius), Foot(view, part)),
            "upper_arm" => WithDisc(view, part, U(1.05 * _p.UpperArmRadius),
                Limb(view, part, "forearm_" + side, _p.UpperArmRadius, 0.8 * _p.UpperArmRadius)),
            "forearm" => WithDisc(view, part, U(0.8 * _p.UpperArmRadius),
                Limb(view, part, "hand_" + side, 0.8 * _p.UpperArmRadius, 0.58 * _p.UpperArmRadius)),
            "hand" => WithDisc(view, part, U(0.55 * _p.UpperArmRadius), Hand(view, part)),
            _ => throw new ArgumentException($"Unknown part '{part}'.", nameof(part)),
        };
    }

    private IEnumerable<(IShape, bool)> WithDisc(View view, string part, float radius, IShape shape)
    {
        yield return (shape, false);
        if (_discs)
        {
            var c = view.Project(_joints[part]);
            yield return (new EllipseShape(c, radius, radius), true);
        }
    }

    private CapsuleShape Limb(View view, string from, string to, double r1, double r2) =>
        new(view.Project(_joints[from]), U(r1), view.Project(_joints[to]), U(r2));

    // ------------------------------------------------------------------ torso and head

    private IReadOnlyList<Section> TorsoSections(string bone)
    {
        float s = U(_p.ShoulderHalf), w = U(_p.WaistHalf), h = U(_p.HipHalf), d = U(_p.ChestDepth);
        float chestLow = (0.8f * s + w) / 2, o = U(0.03);   // parts overlap a little so joints stay closed
        float c = _chestBottom - _chestTop, hip = _waistBottom + U(0.45 * _p.Pelvis);
        return bone switch
        {
            "chest" =>
            [
                new(_chestTop, 0.8f * s, 0.6f * d, 0.7f * d), new(_chestTop + U(0.08), 0.97f * s, 0.8f * d, 0.8f * d),
                new(_chestTop + 0.3f * c, s, d, 0.9f * d), new(_chestTop + 0.7f * c, 0.9f * s, 0.95f * d, 0.85f * d),
                new(_chestBottom + o, chestLow, 0.8f * d, 0.75f * d),
            ],
            "waist" => [new(_chestBottom - o, chestLow, 0.8f * d, 0.75f * d), new(_waistBottom + o, w, 0.75f * d, 0.72f * d)],
            _ =>
            [
                new(_waistBottom - o, 1.02f * w, 0.75f * d, 0.72f * d), new(hip, h, 0.8f * d, 0.95f * d),
                new(hip + 0.6f * (_crotch - hip), 0.9f * h, 0.7f * d, 0.85f * d), new(_crotch, 0.45f * h, 0.4f * d, 0.5f * d),
            ],
        };
    }

    /// <summary>
    /// The outline of a stack of sections turned to the view: on each row the span of a ring that is a half
    /// ellipse to the front and another to the back.
    /// </summary>
    private static PolygonShape Loft(IReadOnlyList<Section> sections, View view)
    {
        var left = new List<Vector2>();
        var right = new List<Vector2>();
        float top = sections[0].Y, bottom = sections[^1].Y;
        for (float y = top; ; y += 0.25f)
        {
            y = Math.Min(y, bottom);
            var (min, max) = Span(At(sections, y), view);
            left.Add(new(min, y));
            right.Add(new(max, y));
            if (y >= bottom)
                break;
        }
        right.Reverse();
        return new PolygonShape(left.Concat(right));
    }

    private static Section At(IReadOnlyList<Section> sections, float y)
    {
        int i = 0;
        while (i < sections.Count - 2 && y > sections[i + 1].Y)
            i++;
        var (a, b) = (sections[i], sections[i + 1]);
        float t = Math.Clamp((y - a.Y) / (b.Y - a.Y), 0, 1);
        t = t * t * (3 - 2 * t);   // smooth between rows
        return new(y, a.Across + (b.Across - a.Across) * t, a.Front + (b.Front - a.Front) * t, a.Back + (b.Back - a.Back) * t);
    }

    private static (float Min, float Max) Span(Section s, View view)
    {
        float min = float.MaxValue, max = float.MinValue;
        for (int k = 0; k < 48; k++)
        {
            double a = k * Math.PI / 24;
            float x = s.Across * (float)Math.Cos(a), sin = (float)Math.Sin(a);
            float z = (sin >= 0 ? s.Front : s.Back) * sin;
            float sx = view.ScreenX(x, z);
            (min, max) = (Math.Min(min, sx), Math.Max(max, sx));
        }
        return (min, max);
    }

    // The 4-head template's head, in its design units (head 60 high from y 8): half width from the front
    // and the profile facing screen-left (negative dx = face). Scaled to this body's head size and shape.
    private static readonly Vector2[] HeadFront =
        [new(0, 8), new(14.5f, 11.9f), new(25.1f, 22.5f), new(29, 37), new(27.5f, 46), new(23.5f, 54.5f), new(17, 61.5f), new(9, 66.5f), new(0, 68)];

    private static readonly Vector2[] HeadSide =
    [
        new(-20, 65), new(-24, 61.5f), new(-25, 57), new(-26.5f, 53), new(-26, 49), new(-29, 46), new(-27, 41.5f), new(-27.5f, 34),
        new(-25.5f, 24), new(-20, 15.5f), new(-11, 9.5f), new(0, 7.5f), new(11, 8.5f), new(21, 14), new(27.5f, 24), new(30, 36),
        new(28, 46.5f), new(22, 55), new(11, 60.5f), new(-4, 62.5f), new(-12, 64.5f),
    ];

    private static readonly Vector2[] HeadFrontOutline =
        SmoothClosed([.. HeadFront, .. HeadFront[1..^1].Reverse().Select(p => new Vector2(-p.X, p.Y))]);

    private static readonly Vector2[] HeadSideOutline = SmoothClosed(HeadSide);

    private IEnumerable<(IShape, bool)> Head(View view)
    {
        float across = (float)(_p.HeadHalfWidth / (29.0 / 60));
        float depth = (float)_p.HeadDepthScale;
        float Scale(float design) => design / 60 * _u;
        var sections = new List<Section>();
        for (float dy = 8; dy <= 68; dy += 0.5f)
        {
            if (RowSpan(HeadFrontOutline, dy) is not { } front)
                continue;
            float half = front.Max;
            var (face, backOfHead) = RowSpan(HeadSideOutline, dy) is { } side ? (-side.Min, side.Max) : (half * 0.8f, half * 0.8f);
            sections.Add(new(Y(0) + Scale(dy - 8), Scale(half) * across, Scale(Math.Max(face, 0.5f)) * depth,
                Scale(Math.Max(backOfHead, 0.5f)) * depth));
        }

        var neckTop = view.Project(new(0, Y(0.85), -U(0.05)));
        var neckBottom = view.Project(new(0, _chestTop + U(0.05), -U(0.05)));
        yield return (new CapsuleShape(neckTop, U(_p.NeckRadius), neckBottom, U(_p.NeckRadius)), false);

        // ears at eye level on the widest part of the head: under the head's edge when seen from the
        // front or back (the edge draws the line between), over it on the near side, hidden on the far side
        float earX = Scale(29) * across, earY = Y(0) + Scale(35), earZ = -Scale(7.5f) * depth;
        var ears = new[] { -1, 1 }.Select(sign => new Vector3(sign * earX, earY, earZ))
            .Select(e => (Shape: (IShape)new EllipseShape(view.Project(e), Scale(4.75f), Scale(7)), Depth: view.Depth(e)))
            .ToList();
        foreach (var ear in ears.Where(e => Math.Abs(e.Depth) <= 0.5f * earX))
            yield return (ear.Shape, false);
        yield return (Loft(sections, view), false);
        foreach (var ear in ears.Where(e => e.Depth > 0.5f * earX))
            yield return (ear.Shape, false);
    }

    /// <summary>Where a closed outline crosses the row y: its leftmost and rightmost x.</summary>
    private static (float Min, float Max)? RowSpan(Vector2[] outline, float y)
    {
        float min = float.MaxValue, max = float.MinValue;
        for (int i = 0, j = outline.Length - 1; i < outline.Length; j = i++)
        {
            var (a, b) = (outline[i], outline[j]);
            if ((a.Y > y) == (b.Y > y))
                continue;
            float x = a.X + (b.X - a.X) * (y - a.Y) / (b.Y - a.Y);
            (min, max) = (Math.Min(min, x), Math.Max(max, x));
        }
        return max >= min ? (min, max) : null;
    }

    /// <summary>A closed Catmull-Rom curve through the points.</summary>
    private static Vector2[] SmoothClosed(Vector2[] pts, int steps = 8)
    {
        var result = new List<Vector2>();
        int n = pts.Length;
        for (int i = 0; i < n; i++)
        {
            var (p0, p1, p2, p3) = (pts[(i - 1 + n) % n], pts[i], pts[(i + 1) % n], pts[(i + 2) % n]);
            for (int k = 0; k < steps; k++)
            {
                float t = (float)k / steps;
                result.Add(0.5f * (2 * p1 + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (-p0 + 3 * p1 - 3 * p2 + p3) * t * t * t));
            }
        }
        return [.. result];
    }

    // ------------------------------------------------------------------ hands and feet

    /// <summary>The palm faces the body: thin from the front, wide from the side.</summary>
    private IShape Hand(View view, string part)
    {
        var wrist = _joints[part];
        float t = U(0.5 * _p.UpperArmRadius), w = U(0.22 * _p.Hand), l = U(_p.Hand), r = Math.Max(0.6f, U(0.04));
        var points = new List<Vector3>();
        foreach (var (dy, kt, kw) in new[] { (0.05f, 1f, 0.75f), (0.5f, 1.1f, 1f), (0.85f, 0.8f, 0.7f) })
            foreach (var sx in new[] { -1, 1 })
                foreach (var sz in new[] { -1, 1 })
                    points.Add(wrist + new Vector3(sx * t * kt, dy * l, sz * w * kw));
        points.Add(wrist + new Vector3(0, l - r, 0));
        return Hull(points, view, r);
    }

    private IShape Foot(View view, string part)
    {
        var ankle = _joints[part];
        float sole = Y(_p.Heads), fh = U(_p.FootHeight), l = U(_p.FootLength), w = U(_p.FootWidth) / 2;
        float r = Math.Max(0.6f, U(0.05)), a = 0.9f * U(0.45 * _p.ThighRadius);
        var points = new List<Vector3>
        {
            ankle + new Vector3(a, 0, 0), ankle + new Vector3(-a, 0, 0), ankle + new Vector3(0, 0, a), ankle + new Vector3(0, 0, -a),
        };
        foreach (var sx in new[] { -1, 1 })
            foreach (var (kx, height, z) in new[]
                     {
                         (0.7f, 0.55f, -0.2f), (0.7f, 0f, -0.2f), (1f, 0f, 0.5f), (0.9f, 0.6f, 0.4f), (0.7f, 0f, 0.78f), (0.6f, 0.4f, 0.76f),
                     })
                points.Add(new Vector3(ankle.X + sx * w * kx, sole - r - height * fh, ankle.Z + z * l));
        return Hull(points, view, r);
    }

    /// <summary>The convex hull of the projected points, each grown to a small circle of radius r.</summary>
    private static PolygonShape Hull(IEnumerable<Vector3> points, View view, float r)
    {
        var flat = points.Select(view.Project)
            .SelectMany(c => Enumerable.Range(0, 12).Select(k => c + r * new Vector2((float)Math.Cos(k * Math.PI / 6), (float)Math.Sin(k * Math.PI / 6))))
            .OrderBy(p => p.X).ThenBy(p => p.Y).ToList();
        float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);
        var hull = new List<Vector2>();
        foreach (var pass in new[] { flat, Enumerable.Reverse(flat).ToList() })
        {
            int start = hull.Count;
            foreach (var p in pass)
            {
                while (hull.Count >= start + 2 && Cross(hull[^2], hull[^1], p) <= 0)
                    hull.RemoveAt(hull.Count - 1);
                hull.Add(p);
            }
            hull.RemoveAt(hull.Count - 1);
        }
        return new PolygonShape(hull);
    }
}
