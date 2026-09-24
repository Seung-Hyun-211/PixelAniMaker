using System.Numerics;
using PixelAniMaker.Core.Imaging;

namespace PixelAniMaker.Core.Rigging;

/// <summary>
/// One bone of the skeleton with its pixel image. At rest (all rotations 0) the image sits at
/// <see cref="RestPosition"/> on the canvas; it rotates around <see cref="RestPivot"/>, the joint it
/// shares with its parent.
/// </summary>
public sealed class Part
{
    private readonly List<Part> _children = [];

    public Part(string name, string label, IndexedImage image, Vector2 restPosition, Vector2 restPivot, int drawOrder)
    {
        Name = name;
        Label = label;
        Image = image;
        RestPosition = restPosition;
        RestPivot = restPivot;
        DrawOrder = drawOrder;
    }

    public string Name { get; }
    public string Label { get; }
    public IndexedImage Image { get; }

    /// <summary>Canvas position of the image's top-left corner at rest.</summary>
    public Vector2 RestPosition { get; }

    /// <summary>Canvas position of the joint at rest.</summary>
    public Vector2 RestPivot { get; }

    /// <summary>Joint position inside the image.</summary>
    public Vector2 LocalPivot => RestPivot - RestPosition;

    public int DrawOrder { get; }
    public Part? Parent { get; private set; }
    public IReadOnlyList<Part> Children => _children;

    public int Depth => Parent is null ? 0 : Parent.Depth + 1;

    internal void AttachTo(Part parent)
    {
        if (Parent is not null)
            throw new InvalidOperationException($"{Name} already has a parent.");
        Parent = parent;
        parent._children.Add(this);
    }
}
