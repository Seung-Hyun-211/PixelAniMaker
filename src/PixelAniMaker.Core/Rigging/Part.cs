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

    public Vector2 RestPosition { get; private set; } = restPosition;
    public Vector2 RestPivot { get; private set; } = restPivot;
    public int DrawOrder { get; } = drawOrder;

    /// <summary>Shifts the image and its joint together (see <see cref="DetailMove"/>).</summary>
    internal void MoveBy(Vector2 delta)
    {
        RestPosition += delta;
        RestPivot += delta;
    }

    /// <summary>Joint position inside the image.</summary>
    public Vector2 LocalPivot => RestPivot - RestPosition;

    /// <summary>Hand-drawn replacement images for 45° steps of rotation.</summary>
    public AngleVariants Variants { get; } = new();

    /// <summary>Named points (weapon grip, effect origin …) in base-image pixels.</summary>
    public AttachmentPoints Attachments { get; } = new();

    /// <summary>A deep copy: same joints, layers, angle variants and attachment points, with copied images.</summary>
    public PartView Copy()
    {
        var copy = new PartView(Layers[0].Image.Clone(), RestPosition, RestPivot, DrawOrder);
        copy.Layers.Items.Clear();
        foreach (var layer in Layers.All)
            copy.Layers.Items.Add(new PartLayer(layer.Name, layer.Image.Clone(), layer.Visible));
        foreach (var (angle, variant) in Variants.All)
            copy.Variants.Set(angle, variant with { Image = variant.Image.Clone() });
        foreach (var (name, point) in Attachments.All)
            copy.Attachments.Set(name, point);
        return copy;
    }

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
/// Left view unless the part has its own right image (<see cref="HasOwnRight"/>); the same goes for the
/// right-facing 3/4 views. 3/4 views are optional: without them the front or back view is used.
/// </summary>
public sealed class Part
{
    private readonly List<Part> _children = [];
    private readonly Dictionary<Direction, PartView> _views;

    /// <param name="views">Every classic stored direction, plus optionally mirrored and 3/4 views.</param>
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

    /// <summary>Secondary motion (sway) added when frames are made (null = none). The same in every direction.</summary>
    public Animation.SecondarySettings? Secondary { get; internal set; }

    /// <summary>Allowed joint rotation while editing (null = any angle). The same in every direction.</summary>
    public RotationLimit? Limit { get; internal set; }

    /// <summary><paramref name="degrees"/> limited to <see cref="Limit"/> (normalized either way).</summary>
    public double ClampRotation(double degrees) => Limit?.Clamp(degrees) ?? Pose.Normalize(degrees);

    public Part? Parent { get; private set; }
    public IReadOnlyList<Part> Children => _children;

    public int Depth => Parent is null ? 0 : Parent.Depth + 1;

    /// <summary>
    /// The view drawn for <paramref name="direction"/>. A mirrored direction uses its own view when
    /// there is one, otherwise its source view; either way it is in source coordinates and the renderer
    /// mirrors it. A 3/4 direction without a view falls back to the front or back view.
    /// </summary>
    public PartView View(Direction direction) =>
        _views.TryGetValue(direction, out var own) ? own
        : _views.TryGetValue(direction.Source(), out var source) ? source
        : _views[direction.Source().Fallback()];

    /// <summary>True when the Right view is drawn separately instead of mirroring the Left view.</summary>
    public bool HasOwnRight => _views.ContainsKey(Direction.Right);

    /// <summary>True when the part stores a view for exactly this direction (no mirroring or fallback).</summary>
    public bool HasOwnView(Direction direction) => _views.ContainsKey(direction);

    /// <summary>Sets or (null) removes the view stored for a mirrored or 3/4 direction.</summary>
    internal void SetView(Direction direction, PartView? view)
    {
        if (DirectionExtensions.Stored.Contains(direction))
            throw new ArgumentException($"The {direction} view cannot be replaced.", nameof(direction));
        if (view is null)
            _views.Remove(direction);
        else
            _views[direction] = view;
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
