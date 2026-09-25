using PixelAniMaker.Core.History;

namespace PixelAniMaker.Core.Rigging;

public enum DrawOrderMove
{
    /// <summary>One place towards the viewer (drawn after the next part).</summary>
    Forward,

    /// <summary>One place away from the viewer.</summary>
    Backward,

    ToFront,
    ToBack,
}

/// <summary>
/// Editing which of the overlapping parts is drawn in front (doc/plan-draw-order.md). A part moves together
/// with its detail parts (eyes, bust), which follow their parent. Mirrored directions share the order of
/// their source direction, like their drawings. Every edit renumbers the direction's orders 0, 1, 2 …
/// </summary>
public static class DrawOrderEdit
{
    /// <summary>The part a detail part follows (itself for a normal part).</summary>
    public static Part UnitHead(Part part)
    {
        while (part.IsDetail && part.Parent is not null)
            part = part.Parent;
        return part;
    }

    /// <summary>The parts that move with <paramref name="part"/>'s unit, in drawing order.</summary>
    private static List<Part> Unit(IReadOnlyList<Part> sequence, Part part)
    {
        var head = UnitHead(part);
        return sequence.Where(p => UnitHead(p) == head).ToList();
    }

    /// <summary>Parts back to front where <paramref name="direction"/> takes its order from (the source of a mirrored direction).</summary>
    public static IReadOnlyList<Part> Sequence(Character character, Direction direction) =>
        character.DrawOrder(direction.Source()).ToList();

    /// <summary>The sequence after moving <paramref name="part"/>'s unit, or null when it cannot move that way.</summary>
    public static IReadOnlyList<Part>? Moved(IReadOnlyList<Part> sequence, Part part, DrawOrderMove move)
    {
        var unit = Unit(sequence, part);
        var head = unit.FirstOrDefault(p => p == UnitHead(part));
        if (head is null)
            return null;
        var rest = sequence.Where(p => !unit.Contains(p)).ToList();
        int at = sequence.Take(sequence.ToList().IndexOf(unit[0])).Count(p => !unit.Contains(p));   // unit's slot in rest
        bool IsHead(Part p) => !p.IsDetail;
        int insert;
        switch (move)
        {
            case DrawOrderMove.Forward:
                int next = rest.FindIndex(at, IsHead);
                if (next < 0)
                    return null;
                insert = next + 1;
                while (insert < rest.Count && rest[insert].IsDetail && UnitHead(rest[insert]) == rest[next])
                    insert++;
                break;
            case DrawOrderMove.Backward:
                int previous = at == 0 ? -1 : rest.FindLastIndex(at - 1, IsHead);
                if (previous < 0)
                    return null;
                insert = previous;
                break;
            case DrawOrderMove.ToFront:
                if (!rest.Skip(at).Any(IsHead))
                    return null;
                insert = rest.Count;
                break;
            default:
                if (!rest.Take(at).Any(IsHead))
                    return null;
                insert = 0;
                break;
        }
        rest.InsertRange(insert, unit);
        return rest;
    }

    /// <summary>
    /// <paramref name="sequence"/> with the pose's drawing order changes applied: each changed part (with its
    /// detail parts) goes just in front of or behind its anchor's unit, in part name order. Unknown parts,
    /// detail parts and anchors in the part's own unit are ignored.
    /// </summary>
    public static IReadOnlyList<Part> WithOverrides(Character character, IReadOnlyList<Part> sequence, PoseData? pose)
    {
        if (pose?.Order is not { Count: > 0 } overrides)
            return sequence;
        var result = sequence.ToList();
        foreach (var (name, order) in overrides.OrderBy(o => o.Key, StringComparer.Ordinal))
        {
            if (character.Find(name) is not { IsDetail: false } part || character.Find(order.Anchor) is not { } anchor
                || UnitHead(anchor) == part)
                continue;
            var anchorHead = UnitHead(anchor);
            var unit = Unit(result, part);
            if (unit.Count == 0 || !result.Any(p => UnitHead(p) == anchorHead))
                continue;
            result.RemoveAll(unit.Contains);
            int first = result.FindIndex(p => UnitHead(p) == anchorHead), last = result.FindLastIndex(p => UnitHead(p) == anchorHead);
            result.InsertRange(order.Front ? last + 1 : first, unit);
        }
        return result;
    }

    public static bool CanMove(Character character, Part part, Direction direction, DrawOrderMove move) =>
        !part.IsDetail && Moved(Sequence(character, direction), part, move) is not null;

    /// <summary>Moves the part (with its detail parts) in the direction's drawing order as one undo step.</summary>
    public static bool Move(Character character, Part part, Direction direction, DrawOrderMove move, UndoHistory history)
    {
        if (part.IsDetail || Moved(Sequence(character, direction), part, move) is not { } sequence)
            return false;
        history.Do(new DrawOrderChange(Renumber(character, direction.Source(), sequence)));
        return true;
    }

    /// <summary>New orders 0, 1, 2 … for the source direction's views and the separately drawn mirrored views that follow it.</summary>
    private static List<(PartView View, int Before, int After)> Renumber(Character character, Direction source, IReadOnlyList<Part> sequence)
    {
        var mirrors = DirectionExtensions.Every.Where(d => d != source && d.Source() == source).ToList();
        var changes = new List<(PartView, int, int)>();
        for (int i = 0; i < sequence.Count; i++)
        {
            var part = sequence[i];
            foreach (var view in mirrors.Where(part.HasOwnView).Select(part.View).Prepend(part.View(source)).Distinct())
                if (view.DrawOrder != i)
                    changes.Add((view, view.DrawOrder, i));
        }
        return changes;
    }
}

/// <summary>Undoable change of part views' drawing orders.</summary>
public sealed class DrawOrderChange(IReadOnlyList<(PartView View, int Before, int After)> changes) : IUndoableAction
{
    public string Name => "그리는 순서";

    public void Undo()
    {
        foreach (var (view, before, _) in changes)
            view.DrawOrder = before;
    }

    public void Redo()
    {
        foreach (var (view, _, after) in changes)
            view.DrawOrder = after;
    }
}
