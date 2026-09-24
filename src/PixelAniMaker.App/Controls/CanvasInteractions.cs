using Avalonia;
using PixelAniMaker.App.Services;
using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.App.Controls;

/// <summary>A mouse interaction on the canvas, in canvas pixel coordinates (fractional).</summary>
internal interface ICanvasInteraction
{
    void Begin(Point canvas, bool secondary, bool snap);
    void Move(Point canvas, bool snap);
    void End();
}

/// <summary>Draws on the active part: canvas pixels are mapped back into the part's own image.</summary>
internal sealed class DrawInteraction(EditorSession session) : ICanvasInteraction
{
    public void Begin(Point canvas, bool secondary, bool snap)
    {
        var (x, y) = ToLocal(canvas);
        session.ActiveDocument.BeginStroke(session.CurrentTool.Tool, x, y, secondary);
    }

    public void Move(Point canvas, bool snap)
    {
        var (x, y) = ToLocal(canvas);
        session.ActiveDocument.ContinueStroke(x, y);
    }

    public void End() => session.ActiveDocument.EndStroke();

    private (int X, int Y) ToLocal(Point canvas) =>
        session.ActiveTransform.ToLocalPixel((int)Math.Floor(canvas.X), (int)Math.Floor(canvas.Y));
}

/// <summary>
/// Picks the part under the cursor and rotates it around its joint while dragging.
/// Shift snaps to 15°. The whole drag is one undo step.
/// </summary>
internal sealed class PoseInteraction(EditorSession session) : ICanvasInteraction
{
    private const double SnapDegrees = 15;

    private Part? _part;
    private Point _pivot;
    private double _startAngle;
    private double _startRotation;

    public void Begin(Point canvas, bool secondary, bool snap)
    {
        short owner = session.Composite.OwnerAt((int)Math.Floor(canvas.X), (int)Math.Floor(canvas.Y));
        if (owner == CompositeResult.NoPart)
            return;
        _part = session.Character.Parts[owner];
        session.ActivePart = _part;
        var pivot = session.Transforms[_part].Pivot;
        _pivot = new Point(pivot.X, pivot.Y);
        _startAngle = AngleTo(canvas);
        _startRotation = session.Character.Pose.Get(_part.Name);
    }

    public void Move(Point canvas, bool snap)
    {
        if (_part is not null)
            session.Character.Pose.Set(_part.Name, Rotation(canvas, snap));
    }

    public void End()
    {
        if (_part is null)
            return;
        PoseChange.Record(session.Character.Pose, session.Character.History, _part.Name,
            _startRotation, session.Character.Pose.Get(_part.Name));
        _part = null;
    }

    private double Rotation(Point canvas, bool snap)
    {
        double degrees = _startRotation + AngleTo(canvas) - _startAngle;
        return snap ? Math.Round(degrees / SnapDegrees) * SnapDegrees : degrees;
    }

    private double AngleTo(Point p) => Math.Atan2(p.Y - _pivot.Y, p.X - _pivot.X) * 180 / Math.PI;
}
