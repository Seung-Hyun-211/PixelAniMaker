using System.Numerics;
using PixelAniMaker.Core.Animation;
using PixelAniMaker.Core.History;

namespace PixelAniMaker.Core.Rigging;

/// <summary>
/// How far the drawing moved on the canvas after a resize: <paramref name="X"/> in normal directions,
/// <paramref name="MirroredX"/> in mirrored ones (they flip the whole frame), <paramref name="Y"/> in all.
/// </summary>
public sealed class CanvasShift(int x, int mirroredX, int y) : EventArgs
{
    public int X { get; } = x;
    public int MirroredX { get; } = mirroredX;
    public int Y { get; } = y;

    public int XFor(Direction direction) => direction.IsMirrored() ? MirroredX : X;
}

/// <summary>Pixels added (negative: removed) on each side of the canvas.</summary>
public sealed record CanvasMargins(int Left, int Top, int Right, int Bottom)
{
    /// <summary>Room for jumps, raised arms, hats and tails around the 96×128 mannequin (→ 128×160, feet stay near the bottom).</summary>
    public static CanvasMargins Default { get; } = new(16, 24, 16, 8);

    public bool IsZero => Left == 0 && Top == 0 && Right == 0 && Bottom == 0;
}

/// <summary>
/// Changes the canvas size by adding or removing margins. Nothing is cropped: every part view moves with
/// its image and joint (so the character keeps its pixels and just sits elsewhere on the canvas), and
/// frame touch-ups move with the frames. Poses are relative and stay as they are. One undo step.
/// </summary>
public static class CanvasResize
{
    public const int MinSize = 8, MaxSize = 1024;

    /// <summary>The size after adding <paramref name="margins"/>.</summary>
    public static (int Width, int Height) SizeWith(Character character, CanvasMargins margins) =>
        (character.Width + margins.Left + margins.Right, character.Height + margins.Top + margins.Bottom);

    public static bool CanApply(Character character, CanvasMargins margins)
    {
        var (w, h) = SizeWith(character, margins);
        return !margins.IsZero && w is >= MinSize and <= MaxSize && h is >= MinSize and <= MaxSize;
    }

    /// <summary>Resizes (undoable); false when the new size would be out of range or nothing changes.</summary>
    public static bool Apply(Character character, IEnumerable<AnimationClip> clips, CanvasMargins margins, UndoHistory history)
    {
        if (!CanApply(character, margins))
            return false;
        history.Do(new CanvasResizeChange(character, clips.ToList(), margins));
        return true;
    }

    /// <summary>Resizes without an undo step, for a character that is being set up (a new project's template).</summary>
    public static void ApplyUnrecorded(Character character, CanvasMargins margins)
    {
        if (CanApply(character, margins))
            new CanvasResizeChange(character, [], margins).Redo();
    }
}

/// <summary>Undoable canvas resize: size, every stored part view, and the clips' touch-ups.</summary>
public sealed class CanvasResizeChange(Character character, IReadOnlyList<AnimationClip> clips, CanvasMargins margins) : IUndoableAction
{
    public string Name => "캔버스 크기";

    public void Redo() => Shift(1);

    public void Undo() => Shift(-1);

    private void Shift(int sign)
    {
        // mirrored directions are drawn by flipping the whole frame, so their content moves by the right margin
        var shift = new CanvasShift(margins.Left * sign, margins.Right * sign, margins.Top * sign);
        var source = new Vector2(shift.X, shift.Y);
        foreach (var view in character.Parts.SelectMany(p => DirectionExtensions.Every.Where(p.HasOwnView).Select(p.View)).Distinct())
            view.MoveBy(source);
        foreach (var clip in clips)
            foreach (var (direction, frame) in clip.Touchups.Frames.ToList())
            {
                int dx = shift.XFor(direction), dy = shift.Y;
                var moved = new PixelOverrides(clip.Touchups.Get(direction, frame).Select(kv =>
                    KeyValuePair.Create((kv.Key.X + dx, kv.Key.Y + dy), kv.Value)));
                clip.Touchups.Set(direction, frame, moved);
            }
        int w = character.Width + (margins.Left + margins.Right) * sign, h = character.Height + (margins.Top + margins.Bottom) * sign;
        character.SetCanvasSize(w, h, shift);
    }
}
