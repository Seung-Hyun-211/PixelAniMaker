using PixelAniMaker.Core.History;

namespace PixelAniMaker.Core.Rigging;

/// <summary>Undoable change of a pose, stored as before/after snapshots.</summary>
public sealed class PoseChange(Pose pose, PoseData before, PoseData after, string name = "포즈") : IUndoableAction
{
    public string Name => name;

    public void Undo() => pose.Restore(before);

    public void Redo() => pose.Restore(after);

    /// <summary>Runs <paramref name="edit"/> on the pose and records it as one undo step (skipped when nothing changed).</summary>
    public static void Apply(Pose pose, UndoHistory history, Action<Pose> edit, string name = "포즈")
    {
        var before = pose.Snapshot();
        edit(pose);
        Record(pose, history, before, name);
    }

    /// <summary>Records a change that was already applied live (e.g. during a drag), starting from <paramref name="before"/>.</summary>
    public static void Record(Pose pose, UndoHistory history, PoseData before, string name = "포즈")
    {
        var after = pose.Snapshot();
        if (!before.SameAs(after))
            history.Push(new PoseChange(pose, before, after, name));
    }
}
