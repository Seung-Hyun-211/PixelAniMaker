using System.Numerics;
using PixelAniMaker.Core.History;

namespace PixelAniMaker.Core.Rigging;

/// <summary>
/// Undoable move of a detail part (such as an eye) in one direction: its image and joint shift together
/// by whole pixels. Only detail parts move — the skeleton's own joints stay where the template put them.
/// A mirrored direction without its own view moves its source view (the view both show).
/// </summary>
public sealed class DetailMove(PartView view, Vector2 delta) : IUndoableAction
{
    public string Name => "파츠 위치";

    public void Undo() => view.MoveBy(-delta);

    public void Redo() => view.MoveBy(delta);

    /// <param name="delta">In source-view coordinates, rounded to whole pixels.</param>
    public static void Apply(Part part, Direction direction, Vector2 delta, UndoHistory history)
    {
        if (!part.IsDetail)
            throw new InvalidOperationException($"Only detail parts can be moved ('{part.Name}' is not one).");
        delta = new Vector2(MathF.Round(delta.X), MathF.Round(delta.Y));
        if (delta == Vector2.Zero)
            return;
        var move = new DetailMove(part.View(direction), delta);
        move.Redo();
        history.Push(move);
    }
}
