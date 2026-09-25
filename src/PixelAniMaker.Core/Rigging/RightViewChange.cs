using PixelAniMaker.Core.History;

namespace PixelAniMaker.Core.Rigging;

/// <summary>
/// Undoable switch of a part between a mirrored and a separately drawn view for a mirrored direction
/// (Right, or a right-facing 3/4 view).
/// </summary>
public sealed class RightViewChange(Part part, PartView? before, PartView? after, Direction direction = Direction.Right)
    : IUndoableAction
{
    public string Name => direction == Direction.Right
        ? after is null ? "우측면 반전으로 되돌리기" : "우측면 따로 그리기"
        : after is null ? $"{direction.Label()} 반전으로 되돌리기" : $"{direction.Label()} 따로 그리기";

    public void Undo() => part.SetView(direction, before);

    public void Redo() => part.SetView(direction, after);

    /// <summary>
    /// Gives <paramref name="part"/> its own view for the mirrored <paramref name="direction"/>, starting
    /// as a copy of the source view (same joints, images and angle variants), or with
    /// <paramref name="separate"/> false drops it.
    /// </summary>
    public static void Apply(Part part, UndoHistory history, bool separate, Direction direction = Direction.Right)
    {
        if (!direction.IsMirrored())
            throw new ArgumentException($"{direction} is not a mirrored direction.", nameof(direction));
        if (part.HasOwnView(direction) == separate)
            return;
        var before = separate ? null : part.View(direction);
        var after = separate ? part.View(direction.Source()).Copy() : null;
        var change = new RightViewChange(part, before, after, direction);
        change.Redo();
        history.Push(change);
    }
}
