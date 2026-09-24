using PixelAniMaker.Core.History;

namespace PixelAniMaker.Core.Rigging;

/// <summary>Undoable rotation of one joint.</summary>
public sealed class PoseChange(Pose pose, string part, double before, double after) : IUndoableAction
{
    public string Name => "포즈";

    public void Undo() => pose.Set(part, before);

    public void Redo() => pose.Set(part, after);

    /// <summary>Applies the rotation and records it (no-op when unchanged).</summary>
    public static void Apply(Pose pose, UndoHistory history, string part, double degrees) =>
        Record(pose, history, part, pose.Get(part), degrees);

    /// <summary>Records a rotation that was already previewed live, starting from <paramref name="before"/>.</summary>
    public static void Record(Pose pose, UndoHistory history, string part, double before, double after)
    {
        after = Pose.Normalize(after);
        pose.Set(part, after);
        if (before != after)
            history.Push(new PoseChange(pose, part, before, after));
    }

    /// <summary>Returns every joint to 0° as one undoable step.</summary>
    public static void ResetAll(Pose pose, UndoHistory history)
    {
        var before = pose.Snapshot();
        if (before.Count == 0)
            return;
        var rest = new Dictionary<string, double>();
        pose.Restore(rest);
        history.Push(new PoseSnapshotChange(pose, before, rest));
    }
}

/// <summary>Undoable change of the whole pose.</summary>
public sealed class PoseSnapshotChange(
    Pose pose, IReadOnlyDictionary<string, double> before, IReadOnlyDictionary<string, double> after) : IUndoableAction
{
    public string Name => "포즈 초기화";

    public void Undo() => pose.Restore(before);

    public void Redo() => pose.Restore(after);
}
