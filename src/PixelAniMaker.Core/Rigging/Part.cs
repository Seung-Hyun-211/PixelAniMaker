using System.Numerics;
using PixelAniMaker.Core.Imaging;

namespace PixelAniMaker.Core.Rigging;

/// <summary>
/// How a part looks in one direction. At rest (all rotations 0) the image sits at
/// <see cref="RestPosition"/> on the canvas and rotates around <see cref="RestPivot"/>, the joint it
/// shares with its parent. The picture is made of <see cref="Layers"/>; <see cref="Image"/> is the
/// flattened result that gets drawn.
/// </summary>
public sealed class PartView(IndexedImage image, Vector2 restPosition, Vector2 restPivot, int drawOrder)
{
    /// <summary>Drawing layers, bottom first (at least one; the first starts as <paramref name="image"/>).</summary>
    public PartLayers Layers { get; } = new(image);

    /// <summary>The visible layers flattened — what is drawn and rotated.</summary>
    public IndexedImage Image => Layers.Flattened;

    public Vector2 RestPosition { get; } = restPosition;
    public Vector2 RestPivot { get; } = restPivot;
    public int DrawOrder { get; } = drawOrder;

    /// <summary>Joint position inside the image.</summary>
    public Vector2 LocalPivot => RestPivot - RestPosition;

    /// <summary>Hand-drawn replacement images for 45° steps of rotation.</summary>
    public AngleVariants Variants { get; } = new();

    /// <summary>Named points (weapon grip, effect origin …) in base-image pixels.</summary>
    public AttachmentPoints Attachments { get; } = new();

    /// <summary>The image to draw at a rotation (radians): a variant near that angle, or the base image.</summary>
    public (IndexedImage Image, Vector2 Pivot, float Angle) Pick(float radians)
    {
        double degrees = radians * 180 / Math.PI;
        if (Variants.Pick(degrees) is { } angle && Variants.Get(angle) is { } v)
            return (v.Image, v.LocalPivot, (float)(Pose.Normalize(degrees - angle) * Math.PI / 180));
        return (Image, LocalPivot, radians);
    }
}

/// <summary>
/// One bone of the skeleton, with its look in every stored direction. The Right view mirrors the
/// Left view unless the part has its own right image (<see cref="HasOwnRight"/>).
/// </summary>
public sealed class Part
{
    private readonly List<Part> _children = [];
    private readonly Dictionary<Direction, PartView> _views;

    /// <param name="views">Every stored direction, plus optionally <see cref="Direction.Right"/>.</param>
    public Part(string name, string label, IReadOnlyDictionary<Direction, PartView> views)
    {
        foreach (var direction in DirectionExtensions.Stored)
            if (!views.ContainsKey(direction))
                throw new ArgumentException($"Part '{name}' has no {direction} view.", nameof(views));
        Name = name;
        Label = label;
        _views = new Dictionary<Direction, PartView>(views);
    }

    public string Name { get; }
    public string Label { get; }

    /// <summary>
    /// A detail drawn on its parent (e.g. an eye on the head): it follows the parent's outline instead
    /// of getting its own, so no line is drawn where it meets the parent.
    /// </summary>
    public bool IsDetail { get; init; }

    public Part? Parent { get; private set; }
    public IReadOnlyList<Part> Children => _children;

    public int Depth => Parent is null ? 0 : Parent.Depth + 1;

    /// <summary>
    /// The view drawn for <paramref name="direction"/>. Right uses its own view when there is one,
    /// otherwise the Left view. Either way it is in Left-view coordinates; the renderer mirrors it.
    /// </summary>
    public PartView View(Direction direction) =>
        _views.TryGetValue(direction, out var own) ? own : _views[direction.Source()];

    /// <summary>True when the Right view is drawn separately instead of mirroring the Left view.</summary>
    public bool HasOwnRight => _views.ContainsKey(Direction.Right);

    /// <summary>Gives the part its own right view, or (null) goes back to mirroring the Left view.</summary>
    internal void SetOwnRight(PartView? view)
    {
        if (view is null)
            _views.Remove(Direction.Right);
        else
            _views[Direction.Right] = view;
    }

    internal void Detach()
    {
        Parent?._children.Remove(this);
        Parent = null;
    }

    internal void AttachTo(Part parent)
    {
        if (Parent is not null)
            throw new InvalidOperationException($"{Name} already has a parent.");
        Parent = parent;
        parent._children.Add(this);
    }
}
