using System.Numerics;
using PixelAniMaker.App.Services;
using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.App.Controls;

/// <summary>
/// A mouse interaction on the canvas. Points are canvas pixels (fractional) in the coordinates of the
/// current direction's source view, i.e. already un-mirrored for the Right view.
/// </summary>
internal interface ICanvasInteraction
{
    void Begin(CanvasInput input);
    void Move(CanvasInput input);
    void End();
}

/// <param name="Point">Canvas point in source-view coordinates.</param>
/// <param name="Display">Canvas point as shown (not un-mirrored), for editing finished frames.</param>
/// <param name="Secondary">Right mouse button.</param>
/// <param name="Snap">Shift: snap angles.</param>
/// <param name="UseActivePart">Alt: act on the selected part instead of the one under the cursor.</param>
internal readonly record struct CanvasInput(Vector2 Point, Vector2 Display, bool Secondary, bool Snap, bool UseActivePart);

/// <summary>Paints on the finished frame (touch-up layer), in display coordinates.</summary>
internal sealed class TouchupInteraction(EditorSession session, TouchupSession touchup) : ICanvasInteraction
{
    public void Begin(CanvasInput input)
    {
        var (x, y) = Pixel(input);
        touchup.Document?.BeginStroke(session.ActiveTool, x, y, input.Secondary);
    }

    public void Move(CanvasInput input)
    {
        var (x, y) = Pixel(input);
        touchup.Document?.ContinueStroke(x, y);
    }

    public void End() => touchup.Document?.EndStroke();

    private static (int X, int Y) Pixel(CanvasInput input) =>
        ((int)MathF.Floor(input.Display.X), (int)MathF.Floor(input.Display.Y));
}

/// <summary>
/// Draws on the active part: canvas pixels are mapped back into the part's own image. Does nothing
/// while the active part is hidden or locked.
/// </summary>
internal sealed class DrawInteraction(EditorSession session) : ICanvasInteraction
{
    private bool _drawing;

    public void Begin(CanvasInput input)
    {
        _drawing = session.CanDrawActivePart;
        if (!_drawing)
            return;
        session.BeginStroke();                      // may grow the part: map the point afterwards
        var (x, y) = ToLocal(input.Point);
        session.ActiveDocument.BeginStroke(session.ActiveTool, x, y, input.Secondary);
    }

    public void Move(CanvasInput input)
    {
        if (!_drawing)
            return;
        var (x, y) = ToLocal(input.Point);
        session.ActiveDocument.ContinueStroke(x, y);
    }

    public void End()
    {
        if (_drawing)
        {
            session.ActiveDocument.EndStroke();
            session.EndStroke();
        }
        _drawing = false;
    }

    private (int X, int Y) ToLocal(Vector2 canvas) =>
        session.ActiveTransform.ToLocalPixel((int)MathF.Floor(canvas.X), (int)MathF.Floor(canvas.Y));
}

/// <summary>
/// Skeleton editing (pose mode + 뼈대 편집): a drag that starts on a joint dot moves only that joint; one that
/// starts on a part moves the part with the parts under it (Alt: the selected part). Shift keeps the move
/// horizontal or vertical. The move follows the mouse in whatever pose is shown and lands on whole pixels; the
/// whole drag is one undo step. Locked and hidden parts are left alone.
/// </summary>
internal sealed class SkeletonInteraction(EditorSession session) : ICanvasInteraction
{
    private Part? _part;
    private SkeletonHandle _handle;
    private Vector2 _start, _applied;
    private PoseData _pose = PoseData.Rest;

    public void Begin(CanvasInput input)
    {
        var (part, handle) = Pick(input);
        if (part is null)
            return;
        session.ActivePart = part;
        if (session.IsLocked(part))
            return;
        _part = part;
        _handle = handle;
        _start = input.Point;
        _applied = Vector2.Zero;
        _pose = session.CurrentPose.Snapshot();
        session.Character.History.BeginGroup(handle == SkeletonHandle.Joint ? "관절 옮기기" : "뼈대 옮기기");
    }

    public void Move(CanvasInput input)
    {
        if (_part is null)
            return;
        var drag = input.Point - _start;
        if (input.Snap)
            drag = MathF.Abs(drag.X) >= MathF.Abs(drag.Y) ? new Vector2(drag.X, 0) : new Vector2(0, drag.Y);
        var rest = SkeletonEdit.ToRest(session.Character, _part, session.Direction, _pose, drag);
        var target = new Vector2(MathF.Round(rest.X), MathF.Round(rest.Y));
        if (target == _applied)
            return;
        SkeletonEdit.Apply(session.Character, _part, session.Direction, target - _applied, _handle, session.SkeletonMirror);
        _applied = target;
    }

    public void End()
    {
        if (_part is not null)
            session.Character.History.EndGroup();
        _part = null;
    }

    /// <summary>The nearest joint dot within reach of the cursor, else the part under it (or the selected one).</summary>
    private (Part?, SkeletonHandle) Pick(CanvasInput input)
    {
        float reach = 5f / Math.Max(1, session.Zoom) + 0.5f;   // the dots are drawn 4 screen pixels wide
        var joint = session.Transforms
            .Where(kv => !session.IsHidden(kv.Key))
            .Select(kv => (Part: kv.Key, Distance: Vector2.Distance(kv.Value.Pivot, input.Point)))
            .Where(j => j.Distance <= reach)
            .OrderBy(j => j.Part == session.ActivePart ? 0 : 1).ThenBy(j => j.Distance)
            .FirstOrDefault();
        if (!input.UseActivePart && joint.Part is not null)
            return (joint.Part, SkeletonHandle.Joint);
        short owner = session.SourceComposite.OwnerAt((int)MathF.Floor(input.Point.X), (int)MathF.Floor(input.Point.Y));
        var part = input.UseActivePart || owner == CompositeResult.NoPart ? session.ActivePart : session.Character.Parts[owner];
        return (part, SkeletonHandle.Part);
    }
}

/// <summary>
/// Picks the part under the cursor and rotates it around its joint while dragging. With Alt, or when
/// nothing is under the cursor, rotates the selected part (for parts hidden behind others).
/// Shift snaps to 15°. The whole drag is one undo step. A locked part is selected but not rotated, and
/// a part with a rotation limit stops at its ends. Grabbing a detail part turns its parent.
/// </summary>
internal sealed class PoseInteraction(EditorSession session) : ICanvasInteraction
{
    private const double SnapDegrees = 15;

    private Part? _part;
    private Vector2 _pivot;
    private double _startAngle;
    private double _startRotation;
    private PoseData _before = PoseData.Rest;

    public void Begin(CanvasInput input)
    {
        short owner = session.SourceComposite.OwnerAt((int)MathF.Floor(input.Point.X), (int)MathF.Floor(input.Point.Y));
        _part = input.UseActivePart || owner == CompositeResult.NoPart
            ? session.ActivePart
            : session.Character.Parts[owner];
        if (_part is { IsDetail: true, Parent: { } parent })
            _part = parent;                  // details (eyes, bust) have no rotation of their own: turn what they sit on
        session.ActivePart = _part;
        if (session.IsLocked(_part))
        {
            _part = null;
            return;
        }
        _pivot = session.Transforms[_part].Pivot;
        _startAngle = AngleTo(input.Point);
        _startRotation = session.CurrentPose.Get(_part.Name);
        _before = session.CurrentPose.Snapshot();
    }

    public void Move(CanvasInput input)
    {
        if (_part is not null)
            session.CurrentPose.Set(_part.Name, _part.ClampRotation(Rotation(input.Point, input.Snap)));
    }

    public void End()
    {
        if (_part is null)
            return;
        PoseChange.Record(session.CurrentPose, session.Character.History, _before);
        _part = null;
    }

    private double Rotation(Vector2 canvas, bool snap)
    {
        double degrees = _startRotation + AngleTo(canvas) - _startAngle;
        return snap ? Math.Round(degrees / SnapDegrees) * SnapDegrees : degrees;
    }

    private double AngleTo(Vector2 p) => Math.Atan2(p.Y - _pivot.Y, p.X - _pivot.X) * 180 / Math.PI;
}
