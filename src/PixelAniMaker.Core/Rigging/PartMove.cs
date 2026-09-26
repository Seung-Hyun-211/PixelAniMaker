using System.Numerics;
using PixelAniMaker.Core.History;

namespace PixelAniMaker.Core.Rigging;

/// <summary>
/// Undoable move of a movable part (a detail such as an eye, or an added part) in one direction: its
/// image and joint shift together by whole pixels, and an added part takes its added children along
/// (the rest of a chain). The template skeleton's own joints stay where the template put them.
/// A mirrored direction without its own view moves its source view (the view both show).
/// </summary>
public sealed class PartMove(IReadOnlyList<PartView> views, Vector2 delta) : IUndoableAction
{
    public string Name => "파츠 위치";

    public void Undo()
    {
        foreach (var view in views)
            view.MoveBy(-delta);
    }

    public void Redo()
    {
        foreach (var view in views)
            view.MoveBy(delta);
    }

    /// <param name="delta">In source-view coordinates, rounded to whole pixels.</param>
    public static void Apply(Part part, Direction direction, Vector2 delta, UndoHistory history)
    {
        if (!part.IsMovable)
            throw new InvalidOperationException($"Only detail or added parts can be moved ('{part.Name}' is not one).");
        delta = new Vector2(MathF.Round(delta.X), MathF.Round(delta.Y));
        if (delta == Vector2.Zero)
            return;
        // an added part takes the links under it along (only added parts hang under added parts)
        IEnumerable<Part> moved = part.IsCustom ? part.SelfAndDescendants() : [part];
        history.Do(new PartMove(moved.Select(p => p.View(direction)).Distinct().ToList(), delta));
    }
}
