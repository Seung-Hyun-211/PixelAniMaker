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
/// <param name="Secondary">Right mouse button.</param>
/// <param name="Snap">Shift: snap angles.</param>
/// <param name="UseActivePart">Alt: act on the selected part instead of the one under the cursor.</param>
internal readonly record struct CanvasInput(Vector2 Point, bool Secondary, bool Snap, bool UseActivePart);

/// <summary>Draws on the active part: canvas pixels are mapped back into the part's own image.</summary>
internal sealed class DrawInteraction(EditorSession session) : ICanvasInteraction
{
    public void Begin(CanvasInput input)
    {
        var (x, y) = ToLocal(input.Point);
        session.ActiveDocument.BeginStroke(session.CurrentTool.Tool, x, y, input.Secondary);
    }

    public void Move(CanvasInput input)
    {
        var (x, y) = ToLocal(input.Point);
        session.ActiveDocument.ContinueStroke(x, y);
    }

    public void End() => session.ActiveDocument.EndStroke();

    private (int X, int Y) ToLocal(Vector2 canvas) =>
        session.ActiveTransform.ToLocalPixel((int)MathF.Floor(canvas.X), (int)MathF.Floor(canvas.Y));
}

/// <summary>
/// Picks the part under the cursor and rotates it around its joint while dragging. With Alt, or when
/// nothing is under the cursor, rotates the selected part (for parts hidden behind others).
/// Shift snaps to 15°. The whole drag is one undo step.
/// </summary>
internal sealed class PoseInteraction(EditorSession session) : ICanvasInteraction
{
    private const double SnapDegrees = 15;

    private Part? _part;
    private Vector2 _pivot;
    private double _startAngle;
    private double _startRotation;

    public void Begin(CanvasInput input)
    {
        short owner = session.SourceComposite.OwnerAt((int)MathF.Floor(input.Point.X), (int)MathF.Floor(input.Point.Y));
        _part = input.UseActivePart || owner == CompositeResult.NoPart
            ? session.ActivePart
            : session.Character.Parts[owner];
        session.ActivePart = _part;
        _pivot = session.Transforms[_part].Pivot;
        _startAngle = AngleTo(input.Point);
        _startRotation = session.CurrentPose.Get(_part.Name);
    }

    public void Move(CanvasInput input)
    {
        if (_part is not null)
            session.CurrentPose.Set(_part.Name, Rotation(input.Point, input.Snap));
    }

    public void End()
    {
        if (_part is null)
            return;
        PoseChange.Record(session.CurrentPose, session.Character.History, _part.Name,
            _startRotation, session.CurrentPose.Get(_part.Name));
        _part = null;
    }

    private double Rotation(Vector2 canvas, bool snap)
    {
        double degrees = _startRotation + AngleTo(canvas) - _startAngle;
        return snap ? Math.Round(degrees / SnapDegrees) * SnapDegrees : degrees;
    }

    private double AngleTo(Vector2 p) => Math.Atan2(p.Y - _pivot.Y, p.X - _pivot.X) * 180 / Math.PI;
}
