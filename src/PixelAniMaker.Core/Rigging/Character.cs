using System.Numerics;
using PixelAniMaker.Core.Editing;
using PixelAniMaker.Core.History;
using PixelAniMaker.Core.Imaging;

namespace PixelAniMaker.Core.Rigging;

/// <summary>
/// A skeleton of parts sharing one palette, undo history and colour selection, with one pose per
/// stored direction (the Right view mirrors the Left view and its pose).
/// </summary>
public sealed class Character
{
    private readonly Dictionary<Direction, Pose> _poses = DirectionExtensions.Stored.ToDictionary(d => d, _ => new Pose());

    public Character(int width, int height, Palette palette, IEnumerable<Part> parts)
    {
        Width = width;
        Height = height;
        Palette = palette;
        Colors = new ColorSelection(palette);
        Parts = parts.ToList();
        Root = Parts.Single(p => p.Parent is null);
    }

    public int Width { get; }
    public int Height { get; }
    public Palette Palette { get; }
    public UndoHistory History { get; } = new();
    public ColorSelection Colors { get; }
    public OutlineSettings Outline { get; } = new();

    /// <summary>All parts in a fixed order; indices into this list identify parts in composites.</summary>
    public IReadOnlyList<Part> Parts { get; }

    public Part Root { get; }

    /// <summary>Raised when any direction's pose changes.</summary>
    public event EventHandler? PoseChanged
    {
        add { foreach (var p in _poses.Values) p.Changed += value; }
        remove { foreach (var p in _poses.Values) p.Changed -= value; }
    }

    public Part? Find(string name) => Parts.FirstOrDefault(p => p.Name == name);

    public int IndexOf(Part part)
    {
        for (int i = 0; i < Parts.Count; i++)
            if (Parts[i] == part)
                return i;
        return -1;
    }

    public Pose PoseFor(Direction direction) => _poses[direction.Source()];

    /// <summary>Parts back to front for a direction.</summary>
    public IEnumerable<Part> DrawOrder(Direction direction) => Parts.OrderBy(p => p.View(direction).DrawOrder);

    /// <summary>Parts in tree order (parent before children), for hierarchy views.</summary>
    public IEnumerable<Part> Hierarchy()
    {
        var stack = new Stack<Part>();
        stack.Push(Root);
        while (stack.Count > 0)
        {
            var part = stack.Pop();
            yield return part;
            for (int i = part.Children.Count - 1; i >= 0; i--)
                stack.Push(part.Children[i]);
        }
    }

    /// <summary>A drawing target for one part's image in a direction (Right edits the Left image).</summary>
    public EditorDocument CreateDocument(Part part, Direction direction) => CreateDocument(part.View(direction).Layers[0].Image);

    /// <summary>A drawing target for any image of this character (e.g. an angle variant).</summary>
    public EditorDocument CreateDocument(IndexedImage image) => new(image, Palette, History, Colors);

    /// <summary>
    /// Canvas placement of every part (forward kinematics) for <paramref name="pose"/>, or the
    /// direction's current pose, in the coordinates of the stored source direction — mirroring for
    /// Right is left to the renderer. The body offset is rounded to whole pixels.
    /// </summary>
    public IReadOnlyDictionary<Part, PartTransform> ComputeTransforms(Direction direction, PoseData? pose = null)
    {
        pose ??= PoseFor(direction).Snapshot();
        var bodyOffset = new Vector2(MathF.Round(pose.Offset.X), MathF.Round(pose.Offset.Y));
        var result = new Dictionary<Part, PartTransform>(Parts.Count);
        foreach (var part in Hierarchy())
        {
            var view = part.View(direction);
            float angle = (float)(pose.Get(part.Name) * Math.PI / 180);
            if (part.Parent is null)
            {
                result[part] = new PartTransform(part, view, view.RestPivot + bodyOffset, angle);
                continue;
            }
            var parent = result[part.Parent];
            var offset = PartTransform.Rotate(view.RestPivot - parent.View.RestPivot, parent.Angle);
            result[part] = new PartTransform(part, view, parent.Pivot + offset, parent.Angle + angle);
        }
        return result;
    }
}
