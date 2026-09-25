using PixelAniMaker.Core.History;

namespace PixelAniMaker.Core.Rigging;

/// <summary>
/// Allowed joint rotation of a part in degrees, <see cref="Min"/> to <see cref="Max"/> within (-180, 180]
/// (0 = rest). Editing clamps to it; keys already saved outside it are left as they are.
/// </summary>
public sealed record RotationLimit
{
    public RotationLimit(double min, double max)
    {
        min = Math.Clamp(min, -180, 180);
        max = Math.Clamp(max, -180, 180);
        (Min, Max) = min <= max ? (min, max) : (max, min);
    }

    public double Min { get; }
    public double Max { get; }

    public bool Contains(double degrees) => Pose.Normalize(degrees) is var d && d >= Min && d <= Max;

    /// <summary>The angle itself when allowed, otherwise the nearer end (measured around the circle).</summary>
    public double Clamp(double degrees)
    {
        double d = Pose.Normalize(degrees);
        if (d >= Min && d <= Max)
            return d;
        return Math.Abs(Pose.Normalize(d - Min)) <= Math.Abs(Pose.Normalize(d - Max)) ? Min : Max;
    }
}

/// <summary>Undoable set or removal (null) of a part's rotation limit.</summary>
public sealed class RotationLimitChange(Part part, RotationLimit? before, RotationLimit? after) : IUndoableAction
{
    public string Name => "회전 범위";

    public void Undo() => part.Limit = before;

    public void Redo() => part.Limit = after;

    public static void Apply(Part part, UndoHistory history, RotationLimit? limit)
    {
        if (part.Limit == limit)
            return;
        var change = new RotationLimitChange(part, part.Limit, limit);
        change.Redo();
        history.Push(change);
    }
}
