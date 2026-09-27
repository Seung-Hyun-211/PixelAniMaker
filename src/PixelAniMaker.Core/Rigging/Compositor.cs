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

    public CompositeResult Clone()
    {
        var copy = new CompositeResult(Width, Height);
        Indices.CopyTo(copy.Indices, 0);
        Owners.CopyTo(copy.Owners, 0);
        return copy;
    }

    public void MirrorHorizontally()
    {
        for (int y = 0; y < Height; y++)
        {
            Array.Reverse(Indices, y * Width, Width);
            Array.Reverse(Owners, y * Width, Width);
        }
    }
}

/// <summary>
/// Renders a posed character in one direction. Rotated parts are resampled RotSprite-style from an
/// 8x Scale2x image so edges stay clean and only palette colours are used.
/// </summary>
public sealed class Compositor
{
    private readonly Dictionary<IndexedImage, RotSprite> _samplers = [];

    /// <summary>Renders <paramref name="direction"/> in <paramref name="pose"/> (default: the direction's current pose).</summary>
    /// <param name="hidden">Parts left out (the editor's hidden parts); the outline then follows what is drawn.</param>
    /// <param name="secondary">Secondary motion of this frame: extra angles, and warps drawn before the outline.</param>
    public CompositeResult Compose(Character character, Direction direction, PoseData? pose = null,
        IReadOnlySet<Part>? hidden = null, Animation.SecondaryFrame? secondary = null)
    {
        var result = new CompositeResult(character.Width, character.Height);
        if (secondary is { IsEmpty: false })
            pose = secondary.Apply(pose ?? character.PoseFor(direction).Snapshot());
        var transforms = character.ComputeTransforms(direction, pose);
        var order = DrawOrderEdit.WithOverrides(character, character.DrawOrder(direction).ToList(),
            pose ?? character.PoseFor(direction).Snapshot()).ToList();
        var drawn = order.Where(p => hidden is null || !hidden.Contains(p)).ToList();
        // smooth joints: every drawn part bends at its joint; a parent with no other child bends half of it
        var joints = new List<JointBend>();
        var bendOf = new Dictionary<Part, JointBend>();
        var tipOf = new Dictionary<Part, JointBend>();
        if (character.JointBlend.Enabled)
            foreach (var part in drawn.Where(p => p is { Parent: not null, IsDetail: false } && drawn.Contains(p.Parent!)))
            {
                var parent = part.Parent!;
                bool shared = parent.Children.Count(c => !c.IsDetail) == 1;
                float relative = (float)(Pose.Normalize((transforms[part].Angle - transforms[parent].Angle) * 180 / Math.PI) * Math.PI / 180);
                var bend = new JointBend((short)character.IndexOf(part), (short)character.IndexOf(parent), transforms[part].Pivot,
                    relative, character.JointBlend.Radius, shared);
                joints.Add(bend);
                bendOf[part] = bend;
                if (shared)
                    tipOf[parent] = bend;
            }
        foreach (var part in drawn)
        {
            var t = transforms[part];
            var image = secondary is not null && secondary.Offsets.TryGetValue(part, out var off) ? Warp(t, off) : t.Image;
            Draw(result, t, image, (short)character.IndexOf(part),
                bendOf.TryGetValue(part, out var own) ? own : null, tipOf.TryGetValue(part, out var tip) ? tip : null);
        }
        if (joints.Count > 0)
            JointBlendPass.FillGaps(result, joints);
        var rank = RankByOwner(character, order);
        if (character.Shading.Enabled)
            ShadingPass.Apply(result, rank, character.Palette, character.Shading, character.Outline.Enabled,
                character.Outline.Enabled ? new HashSet<int> { character.Outline.OutlineIndex, character.Outline.InnerIndex } : []);
        if (character.Outline.Enabled)
            OutlinePass.Apply(result, rank, character.Outline,
                joints.Count > 0 ? (a, b, x, y) => JointBlendPass.Joined(joints, a, b, x, y) : null);
        if (direction.IsMirrored())
            result.MirrorHorizontally();
        return result;
    }

    /// <summary>
    /// The drawn image deformed by a canvas offset: the top row stays, row y moves by the offset × (y/h)^1.5,
    /// in whole pixels; rows that spread apart are joined. The image grows downwards so nothing is cut.
    /// </summary>
    private static IndexedImage Warp(PartTransform t, System.Numerics.Vector2 offset)
    {
        var src = t.Image;
        var local = PartTransform.Rotate(offset, -t.ImageAngle);        // canvas → image axes
        int pad = (int)MathF.Ceiling(MathF.Abs(local.Y)) + 1;
        int w = src.Width, h = src.Height;
        var dst = new IndexedImage(w, h + pad);
        var written = new bool[w * (h + pad)];
        for (int y = 0; y < h; y++)
        {
            float weight = MathF.Pow(h > 1 ? y / (float)(h - 1) : 1, 1.5f);
            int dx = (int)MathF.Round(local.X * weight), dy = (int)MathF.Round(local.Y * weight);
            for (int x = 0; x < w; x++)
            {
                int index = src[x, y];
                if (index == Palette.TransparentIndex || !dst.InBounds(x + dx, y + dy))
                    continue;
                dst.Set(x + dx, y + dy, index);
                written[(y + dy) * w + x + dx] = true;
            }
        }
        for (int y = 1; y < h + pad - 1; y++)                                   // join rows that spread apart
            for (int x = 0; x < w; x++)
                if (!written[y * w + x] && written[(y - 1) * w + x] && written[(y + 1) * w + x])
                    dst.Set(x, y, dst[x, y - 1]);
        return dst;
    }

    /// <param name="bend">
    /// A smooth joint: around the joint the image turns less, as much as the parent at the joint itself (see
    /// <see cref="JointBend.Remaining"/>). Turning about the joint keeps the distance to it, so this inverts exactly.
    /// </param>
    /// <param name="tip">The shared bend of this part's only child: pixels around that joint turn towards the child.</param>
    private void Draw(CompositeResult target, PartTransform t, IndexedImage image, short owner, JointBend? bend = null, JointBend? tip = null)
    {
        bool bent = bend is { } b && MathF.Abs(b.Bend) > 0.01f;
        bool tipped = tip is { } tb && MathF.Abs(tb.Bend) > 0.01f;
        var (x0, y0, x1, y1) = CanvasBounds(t, image, target.Width, target.Height, bent ? bend!.Value.Bend * (bend.Value.Shared ? 0.5f : 1) : 0);
        if (tipped)
        {
            // pixels around the child's joint stay within the radius of it
            var j = tip!.Value.Joint;
            int r = (int)MathF.Ceiling(tip.Value.Radius) + 1;
            (x0, y0) = (Math.Max(0, Math.Min(x0, (int)j.X - r)), Math.Max(0, Math.Min(y0, (int)j.Y - r)));
            (x1, y1) = (Math.Min(target.Width, Math.Max(x1, (int)j.X + r + 1)), Math.Min(target.Height, Math.Max(y1, (int)j.Y + r + 1)));
        }
        // warped images are made per frame: sample them without filling the cache
        Func<Vector2, int> sample = t.IsGridAligned && !bent && !tipped ? p => SampleNearest(image, p)
            : (image == t.Image ? Sampler(image) : new RotSprite(image)).Sample;

        for (int y = y0; y < y1; y++)
        {
            for (int x = x0; x < x1; x++)
            {
                var canvas = new Vector2(x + 0.5f, y + 0.5f);
                if (tipped)
                {
                    // undo the turn towards the child around its joint first
                    var dj = canvas - tip!.Value.Joint;
                    canvas = tip.Value.Joint + PartTransform.Rotate(dj, -tip.Value.ParentTurn(dj.Length()));
                }
                Vector2 local;
                if (bent)
                {
                    var d = canvas - t.Pivot;
                    local = t.ImagePivot + PartTransform.Rotate(d, -(t.ImageAngle - bend!.Value.Remaining(d.Length())));
                }
                else
                    local = t.ToLocal(canvas);
                int index = sample(local);
                if (index == Palette.TransparentIndex)
                    continue;
                int i = y * target.Width + x;
                target.Indices[i] = (ushort)index;
                target.Owners[i] = owner;
            }
        }
    }

    /// <summary>Draw rank per owner; detail parts share their parent's rank so no line is drawn between them.</summary>
    private static int[] RankByOwner(Character character, List<Part> drawOrder)
    {
        var rank = new int[character.Parts.Count];
        for (int i = 0; i < drawOrder.Count; i++)
            rank[character.IndexOf(drawOrder[i])] = i;
        foreach (var part in character.Parts)
            if (part.IsDetail && part.Parent is { } parent)
                rank[character.IndexOf(part)] = rank[character.IndexOf(parent)];
        return rank;
    }

    /// <param name="bend">Also cover the image turned back by this much (a smooth joint lies between the two).</param>
    private static (int X0, int Y0, int X1, int Y1) CanvasBounds(PartTransform t, IndexedImage img, int width, int height, float bend = 0)
    {
        Vector2[] corners = [new(0, 0), new(img.Width, 0), new(0, img.Height), new(img.Width, img.Height)];
        var pts = corners.Select(t.ToCanvas)
            .Concat(bend == 0 ? [] : corners.Select(c => t.Pivot + PartTransform.Rotate(c - t.ImagePivot, t.ImageAngle - bend)))
            .ToArray();
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

    /// <summary>Cached RotSprite sampler, rebuilt when the image has been edited.</summary>
    private RotSprite Sampler(IndexedImage image)
    {
        if (!_samplers.TryGetValue(image, out var sampler) || sampler.Version != image.Version)
            _samplers[image] = sampler = new RotSprite(image);
        return sampler;
    }
}
