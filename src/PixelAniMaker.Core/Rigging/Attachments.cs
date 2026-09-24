using System.Numerics;
using PixelAniMaker.Core.History;

namespace PixelAniMaker.Core.Rigging;

/// <summary>
/// Named points on a part view (weapon grip, effect origin …) in the base image's pixel coordinates,
/// so they follow the part when it rotates.
/// </summary>
public sealed class AttachmentPoints
{
    private readonly SortedDictionary<string, Vector2> _points = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, Vector2> All => _points;

    public void Set(string name, Vector2 position) => _points[name] = position;

    public void Remove(string name) => _points.Remove(name);

    internal Dictionary<string, Vector2> Snapshot() => new(_points);

    internal void Restore(IReadOnlyDictionary<string, Vector2> points)
    {
        _points.Clear();
        foreach (var (name, p) in points)
            _points[name] = p;
    }
}

/// <summary>Undoable edit of a part view's attachment points (add, move, rename, remove).</summary>
public sealed class AttachmentChange(AttachmentPoints points, IReadOnlyDictionary<string, Vector2> before,
    IReadOnlyDictionary<string, Vector2> after) : IUndoableAction
{
    public string Name => "장착점";

    public void Undo() => points.Restore(before);

    public void Redo() => points.Restore(after);

    public static void Apply(AttachmentPoints points, UndoHistory history, Action<AttachmentPoints> edit)
    {
        var before = points.Snapshot();
        edit(points);
        var after = points.Snapshot();
        if (before.Count == after.Count && before.All(kv => after.TryGetValue(kv.Key, out var v) && v == kv.Value))
            return;
        history.Push(new AttachmentChange(points, before, after));
    }

    /// <summary>A name not used on this view yet: "point", "point2", …</summary>
    public static string FreeName(AttachmentPoints points, string stem = "point")
    {
        if (!points.All.ContainsKey(stem))
            return stem;
        int i = 2;
        while (points.All.ContainsKey(stem + i))
            i++;
        return stem + i;
    }
}

/// <summary>An attachment point placed on the canvas for one pose (as displayed, Right already mirrored).</summary>
/// <param name="Degrees">The part's accumulated rotation, clockwise on screen.</param>
public readonly record struct AttachmentPlacement(Part Part, string Name, Vector2 Position, float Degrees);

public static class Attachments
{
    /// <summary>Every attachment point of the direction's parts, in display canvas coordinates.</summary>
    public static IEnumerable<AttachmentPlacement> Place(Character character, Direction direction, PoseData? pose = null)
    {
        bool mirrored = direction.IsMirrored();
        foreach (var (part, t) in character.ComputeTransforms(direction, pose))
        {
            foreach (var (name, local) in t.View.Attachments.All)
            {
                var p = t.Pivot + PartTransform.Rotate(local - t.View.LocalPivot, t.Angle);
                float degrees = t.Angle * 180 / MathF.PI;
                yield return mirrored
                    ? new AttachmentPlacement(part, name, p with { X = character.Width - p.X }, -degrees)
                    : new AttachmentPlacement(part, name, p, degrees);
            }
        }
    }
}
