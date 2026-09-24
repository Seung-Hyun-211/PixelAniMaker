using PixelAniMaker.Core.Editing;
using PixelAniMaker.Core.History;
using PixelAniMaker.Core.Imaging;

namespace PixelAniMaker.Core.Rigging;

/// <summary>A skeleton of parts sharing one palette, undo history and colour selection.</summary>
public sealed class Character
{
    public Character(int width, int height, Palette palette, IEnumerable<Part> parts)
    {
        Width = width;
        Height = height;
        Palette = palette;
        Colors = new ColorSelection(palette);
        Parts = parts.OrderBy(p => p.DrawOrder).ToList();
        Root = Parts.Single(p => p.Parent is null);
    }

    public int Width { get; }
    public int Height { get; }
    public Palette Palette { get; }
    public UndoHistory History { get; } = new();
    public ColorSelection Colors { get; }
    public Pose Pose { get; } = new();

    /// <summary>All parts in draw order (back to front).</summary>
    public IReadOnlyList<Part> Parts { get; }

    public Part Root { get; }

    public Part? Find(string name) => Parts.FirstOrDefault(p => p.Name == name);

    /// <summary>Parts in tree order (parent before children), for hierarchy views.</summary>
    public IEnumerable<Part> Hierarchy()
    {
        var stack = new Stack<Part>();
        stack.Push(Root);
        while (stack.Count > 0)
        {
            var part = stack.Pop();
            yield return part;
            foreach (var child in part.Children.OrderByDescending(c => c.DrawOrder))
                stack.Push(child);
        }
    }

    /// <summary>A drawing target for one part's image.</summary>
    public EditorDocument CreateDocument(Part part) => new(part.Image, Palette, History, Colors);

    /// <summary>Canvas placement of every part for the current pose (forward kinematics).</summary>
    public IReadOnlyDictionary<Part, PartTransform> ComputeTransforms()
    {
        var result = new Dictionary<Part, PartTransform>(Parts.Count);
        foreach (var part in Hierarchy())
        {
            float angle = (float)(Pose.Get(part.Name) * Math.PI / 180);
            if (part.Parent is null)
            {
                result[part] = new PartTransform(part, part.RestPivot, angle);
                continue;
            }
            var parent = result[part.Parent];
            var offset = PartTransform.Rotate(part.RestPivot - part.Parent.RestPivot, parent.Angle);
            result[part] = new PartTransform(part, parent.Pivot + offset, parent.Angle + angle);
        }
        return result;
    }
}
