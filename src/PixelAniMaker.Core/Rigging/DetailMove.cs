using System.Numerics;
using PixelAniMaker.Core.History;

namespace PixelAniMaker.Core.Rigging;

/// <summary>
/// Undoable move of a movable part (a detail such as an eye, or an added part) in one direction: its
/// image and joint shift together by whole pixels, and an added part takes its added children along
/// (the rest of a chain). The template skeleton's own joints stay where the template put them.
/// A mirrored direction without its own view moves its source view (the view both show).
/// </summary>
public sealed class DetailMove(IReadOnlyList<PartView> views, Vector2 delta) : IUndoableAction
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
        var views = Moved(part).Select(p => p.View(direction)).Distinct().ToList();
        var move = new DetailMove(views, delta);
        move.Redo();
        history.Push(move);
    }

    /// <summary>The part, and for an added part its added descendants.</summary>
    private static IEnumerable<Part> Moved(Part part)
    {
        yield return part;
        if (part.IsCustom)
            foreach (var child in part.Children.Where(c => c.IsCustom))
                foreach (var p in Moved(child))
                    yield return p;
    }
}
