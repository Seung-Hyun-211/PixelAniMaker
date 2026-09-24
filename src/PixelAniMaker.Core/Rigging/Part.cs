using System.Numerics;
using PixelAniMaker.Core.Imaging;

namespace PixelAniMaker.Core.Rigging;

/// <summary>
/// How a part looks in one direction. At rest (all rotations 0) the image sits at
/// <see cref="RestPosition"/> on the canvas and rotates around <see cref="RestPivot"/>, the joint it
/// shares with its parent.
/// </summary>
public sealed record PartView(IndexedImage Image, Vector2 RestPosition, Vector2 RestPivot, int DrawOrder)
{
    /// <summary>Joint position inside the image.</summary>
    public Vector2 LocalPivot => RestPivot - RestPosition;

    /// <summary>Hand-drawn replacement images for 45° steps of rotation.</summary>
    public AngleVariants Variants { get; } = new();

    /// <summary>The image to draw at a rotation (radians): a variant near that angle, or the base image.</summary>
    public (IndexedImage Image, Vector2 Pivot, float Angle) Pick(float radians)
    {
        double degrees = radians * 180 / Math.PI;
        if (Variants.Pick(degrees) is { } angle && Variants.Get(angle) is { } v)
            return (v.Image, v.LocalPivot, (float)(Pose.Normalize(degrees - angle) * Math.PI / 180));
        return (Image, LocalPivot, radians);
    }
}

/// <summary>One bone of the skeleton, with its look in every stored direction.</summary>
public sealed class Part
{
    private readonly List<Part> _children = [];
    private readonly IReadOnlyDictionary<Direction, PartView> _views;

    public Part(string name, string label, IReadOnlyDictionary<Direction, PartView> views)
    {
        foreach (var direction in DirectionExtensions.Stored)
            if (!views.ContainsKey(direction))
                throw new ArgumentException($"Part '{name}' has no {direction} view.", nameof(views));
        Name = name;
        Label = label;
        _views = views;
    }

    public string Name { get; }
    public string Label { get; }
    public Part? Parent { get; private set; }
    public IReadOnlyList<Part> Children => _children;

    public int Depth => Parent is null ? 0 : Parent.Depth + 1;

    /// <summary>The view drawn for <paramref name="direction"/> (Right uses the Left data).</summary>
    public PartView View(Direction direction) => _views[direction.Source()];

    internal void AttachTo(Part parent)
    {
        if (Parent is not null)
            throw new InvalidOperationException($"{Name} already has a parent.");
        Parent = parent;
        parent._children.Add(this);
    }
}
