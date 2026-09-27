using System.Numerics;
using PixelAniMaker.Core.History;

namespace PixelAniMaker.Core.Rigging;

/// <summary>What a skeleton drag moves.</summary>
public enum SkeletonHandle
{
    /// <summary>The part's picture and joint together, with every part hanging under it (the arm with its hand).</summary>
    Part,

    /// <summary>Only the joint the part turns around; the picture stays.</summary>
    Joint,
}

/// <summary>Undoable shift of part views' pictures and joints at rest, in whole pixels.</summary>
public sealed class SkeletonMove(string name, IReadOnlyList<(PartView View, Vector2 Image, Vector2 Joint)> moves) : IUndoableAction
{
    public string Name => name;

    public IReadOnlyList<(PartView View, Vector2 Image, Vector2 Joint)> Moves => moves;

    public void Redo() => Shift(1);

    public void Undo() => Shift(-1);

    private void Shift(float sign)
    {
        foreach (var (view, image, joint) in moves)
            view.SetRest(view.RestPosition + sign * image, view.RestPivot + sign * joint);
    }
}

/// <summary>
/// Editing the skeleton itself, for any part (template parts included), in one direction at a time: moving a
/// part with the parts under it, or moving just its joint. With <c>mirror</c> the left/right counterpart moves
/// too — reflected across the centre line in the front and back views, the same way in the others (the two
/// sides overlap there). Rotations in poses and clips are kept; they turn around the moved joints.
/// </summary>
public static class SkeletonEdit
{
    /// <summary>The move of <paramref name="part"/> by <paramref name="delta"/> (rest coordinates, rounded to whole pixels).</summary>
    public static SkeletonMove Plan(Character character, Part part, Direction direction, Vector2 delta, SkeletonHandle handle,
        bool mirror = false)
    {
        delta = new Vector2(MathF.Round(delta.X), MathF.Round(delta.Y));
        var moves = new List<(PartView, Vector2, Vector2)>();
        var seen = new HashSet<PartView>();   // a mirrored direction may share its source's view

        void Add(Part p, Vector2 d)
        {
            if (handle == SkeletonHandle.Joint)
            {
                if (seen.Add(p.View(direction)))
                    moves.Add((p.View(direction), Vector2.Zero, d));
                return;
            }
            foreach (var q in p.SelfAndDescendants())
                if (seen.Add(q.View(direction)))
                    moves.Add((q.View(direction), d, d));
        }

        Add(part, delta);
        var partner = Symmetry.Counterpart(character, part);
        if (mirror && partner != part)
            Add(partner, Symmetry.Applies(direction) ? new Vector2(-delta.X, delta.Y) : delta);
        return new SkeletonMove(handle == SkeletonHandle.Part ? "뼈대 옮기기" : "관절 옮기기", moves);
    }

    /// <summary>Plans and applies the move as one undo step; false when it moves nothing.</summary>
    public static bool Apply(Character character, Part part, Direction direction, Vector2 delta, SkeletonHandle handle,
        bool mirror = false)
    {
        var move = Plan(character, part, direction, delta, handle, mirror);
        if (move.Moves.All(m => m.Image == Vector2.Zero && m.Joint == Vector2.Zero))
            return false;
        character.History.Do(move);
        return true;
    }

    /// <summary>
    /// A drag on the posed canvas as a rest-space delta for <paramref name="part"/>: the rotation of the part it hangs
    /// from is taken out, so the part follows the mouse whatever the pose.
    /// </summary>
    public static Vector2 ToRest(Character character, Part part, Direction direction, PoseData pose, Vector2 canvasDelta)
    {
        if (part.Parent is not { } parent)
            return canvasDelta;
        float angle = character.ComputeTransforms(direction, pose)[parent].Angle;
        return PartTransform.Rotate(canvasDelta, -angle);
    }
}
