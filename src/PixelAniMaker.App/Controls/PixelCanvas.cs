using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using PixelAniMaker.App.Services;
using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.App.Controls;

/// <summary>
/// Zoomable view of the posed character. Draw mode edits the active part, pose mode rotates joints.
/// Middle button or Space+drag pans, the wheel zooms around the cursor.
/// </summary>
public sealed class PixelCanvas : SessionControl
{
    public static readonly StyledProperty<AnimationSession?> AnimationProperty =
        AvaloniaProperty.Register<PixelCanvas, AnimationSession?>(nameof(Animation));

    public static readonly StyledProperty<TouchupSession?> TouchupProperty =
        AvaloniaProperty.Register<PixelCanvas, TouchupSession?>(nameof(Touchup));

    private const int MinZoomForGrid = 6;
    private const double OnionOpacity = 0.3;

    private static readonly IBrush Background = new SolidColorBrush(Color.FromRgb(37, 37, 40));
    private static readonly IPen GridPen = new Pen(new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), 1);
    private static readonly IPen BorderPen = new Pen(new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)), 1);
    private static readonly IPen HoverPen = new Pen(new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)), 1);
    private static readonly IPen PartBoundsPen = new Pen(new SolidColorBrush(Color.FromArgb(200, 77, 163, 255)), 1, DashStyle.Dash);
    private static readonly IPen SelectionPen = new Pen(Brushes.White, 1, DashStyle.Dash);
    private static readonly IPen SelectionShadowPen = new Pen(Brushes.Black, 1);
    private static readonly IPen BonePen = new Pen(new SolidColorBrush(Color.FromArgb(160, 255, 200, 60)), 2);
    private static readonly IBrush JointBrush = new SolidColorBrush(Color.FromRgb(255, 200, 60));
    private static readonly IBrush ActiveJointBrush = new SolidColorBrush(Color.FromRgb(77, 163, 255));

    private Point _origin;          // screen position of the canvas's top-left corner
    private bool _placed;
    private bool _panning;
    private bool _spaceDown;
    private Point _panStart;
    private Point _originAtPanStart;
    private (int X, int Y)? _hover;
    private Point? _zoomAnchor;     // set during wheel zoom so the view zooms around the cursor
    private int _lastZoom = 4;
    private ICanvasInteraction? _interaction;

    static PixelCanvas()
    {
        FocusableProperty.OverrideDefaultValue<PixelCanvas>(true);
        ClipToBoundsProperty.OverrideDefaultValue<PixelCanvas>(true);
    }

    /// <summary>Source of the neighbouring frames drawn as onion skin.</summary>
    public AnimationSession? Animation
    {
        get => GetValue(AnimationProperty);
        set => SetValue(AnimationProperty, value);
    }

    /// <summary>Finished-frame canvas used in touch-up mode.</summary>
    public TouchupSession? Touchup
    {
        get => GetValue(TouchupProperty);
        set => SetValue(TouchupProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TouchupProperty)
        {
            if (change.OldValue is TouchupSession oldTouchup)
                oldTouchup.Updated -= OnAnimationChanged;
            if (change.NewValue is TouchupSession newTouchup)
                newTouchup.Updated += OnAnimationChanged;
            return;
        }
        if (change.Property != AnimationProperty)
            return;
        if (change.OldValue is AnimationSession old)
        {
            old.FramesUpdated -= OnAnimationChanged;
            old.PropertyChanged -= OnAnimationChanged;
        }
        if (change.NewValue is AnimationSession a)
        {
            a.FramesUpdated += OnAnimationChanged;
            a.PropertyChanged += OnAnimationChanged;
        }
    }

    private void OnAnimationChanged(object? sender, EventArgs e) => InvalidateVisual();

    protected override void OnSessionAttached() => PlaceImage(Bounds.Size);

    protected override void OnSessionPropertyChanged(string? propertyName)
    {
        if (propertyName == nameof(EditorSession.Character))
            PlaceImage(Bounds.Size);
        else if (propertyName == nameof(EditorSession.Zoom) && Session is { } s)
        {
            ZoomAround(_zoomAnchor ?? new Point(Bounds.Width / 2, Bounds.Height / 2), _lastZoom);
            _lastZoom = s.Zoom;
        }
    }

    // ------------------------------------------------------------------ layout

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (!_placed)
            PlaceImage(e.NewSize);
    }

    /// <summary>Centres the canvas and picks the largest zoom step that fits.</summary>
    private void PlaceImage(Size size)
    {
        _placed = false;
        if (Session is not { } s || size.Width <= 0 || size.Height <= 0)
            return;
        var c = s.Character;
        double fit = Math.Min(size.Width / c.Width, size.Height / c.Height) * 0.9;
        int zoom = EditorSession.ZoomSteps.LastOrDefault(z => z <= fit, 1);
        _lastZoom = zoom;
        s.Zoom = zoom;
        _origin = new Point(Math.Round((size.Width - c.Width * zoom) / 2), Math.Round((size.Height - c.Height * zoom) / 2));
        _placed = true;
        InvalidateVisual();
    }

    private void ZoomAround(Point anchor, int oldZoom)
    {
        if (Session is not { } s || oldZoom == s.Zoom)
            return;
        var canvasPoint = (anchor - _origin) / oldZoom;   // keep the canvas point under the anchor fixed
        _origin = new Point(Math.Round(anchor.X - canvasPoint.X * s.Zoom), Math.Round(anchor.Y - canvasPoint.Y * s.Zoom));
    }

    private Point ToScreen(double x, double y) => new(_origin.X + x * Session!.Zoom, _origin.Y + y * Session.Zoom);

    private Point ToCanvas(Point screen) => (screen - _origin) / Session!.Zoom;

    /// <summary>Screen point → source-view canvas point (what interactions work in).</summary>
    private System.Numerics.Vector2 ToSource(Point screen)
    {
        var c = ToCanvas(screen);
        return Session!.ToSource(c.X, c.Y);
    }

    /// <summary>Source-view canvas point → screen point.</summary>
    private Point SourceToScreen(System.Numerics.Vector2 p)
    {
        var d = Session!.FromSource(p);
        return ToScreen(d.X, d.Y);
    }

    // ------------------------------------------------------------------ rendering

    public override void Render(DrawingContext context)
    {
        context.FillRectangle(Background, new Rect(Bounds.Size));
        if (Session is not { } s || !_placed)
            return;

        int w = s.Character.Width, h = s.Character.Height, z = s.Zoom;
        var dest = new Rect(_origin.X, _origin.Y, w * z, h * z);
        context.FillRectangle(CheckerBrushes.Large, dest);
        DrawOnionSkin(context, s, dest);
        var image = s.TouchupMode && Touchup?.Bitmap is { } frame ? frame : s.CanvasBitmap;
        context.DrawImage(image, new Rect(0, 0, w, h), dest);

        if (s.ShowGrid && z >= MinZoomForGrid)
            DrawGrid(context, dest, w, h, z);
        context.DrawRectangle(null, BorderPen, dest.Inflate(0.5));

        if (s.PoseMode)
            DrawSkeleton(context, s);
        else if (!s.TouchupMode)
            DrawPartBounds(context, s.ActiveTransform);
        DrawSelection(context, s);

        if (_hover is var (hx, hy) && hx >= 0 && hy >= 0 && hx < w && hy < h)
            context.DrawRectangle(null, HoverPen, new Rect(ToScreen(hx, hy) + new Point(0.5, 0.5), new Size(z - 1, z - 1)));
    }

    /// <summary>Previous and next baked frames, faded, under the current image.</summary>
    private void DrawOnionSkin(DrawingContext context, EditorSession s, Rect dest)
    {
        if (Animation is not { OnionSkin: true, CurrentClip: { } clip } a)
            return;
        using (context.PushOpacity(OnionOpacity))
        {
            foreach (int delta in (int[])[-1, 1])
            {
                int frame = a.CurrentFrame + delta;
                if (clip.Loop)
                    frame = (frame + clip.FrameCount) % clip.FrameCount;
                if (frame != a.CurrentFrame && a.Frame(s.Direction, frame) is { } bitmap)
                    context.DrawImage(bitmap, new Rect(bitmap.Size), dest);
            }
        }
    }

    private static void DrawGrid(DrawingContext context, Rect dest, int w, int h, int z)
    {
        for (int x = 1; x < w; x++)
        {
            double px = dest.X + x * z + 0.5;
            context.DrawLine(GridPen, new Point(px, dest.Top), new Point(px, dest.Bottom));
        }
        for (int y = 1; y < h; y++)
        {
            double py = dest.Y + y * z + 0.5;
            context.DrawLine(GridPen, new Point(dest.Left, py), new Point(dest.Right, py));
        }
    }

    /// <summary>Dashed outline of the active part's image, following its rotation.</summary>
    private void DrawPartBounds(DrawingContext context, PartTransform t)
    {
        var img = t.Image;
        System.Numerics.Vector2[] corners = [new(0, 0), new(img.Width, 0), new(img.Width, img.Height), new(0, img.Height)];
        var pts = corners.Select(c => SourceToScreen(t.ToCanvas(c))).ToArray();
        for (int i = 0; i < pts.Length; i++)
            context.DrawLine(PartBoundsPen, pts[i], pts[(i + 1) % pts.Length]);
    }

    /// <summary>Marching-ants rectangle around the selection (on the part, following its rotation).</summary>
    private void DrawSelection(DrawingContext context, EditorSession s)
    {
        if (s.PoseMode)
            return;
        var doc = s.TouchupMode ? Touchup?.Document : s.ActiveDocument;
        if (doc?.Selection is not { } r)
            return;
        System.Numerics.Vector2[] corners = [new(r.X0, r.Y0), new(r.X1 + 1, r.Y0), new(r.X1 + 1, r.Y1 + 1), new(r.X0, r.Y1 + 1)];
        var pts = s.TouchupMode
            ? corners.Select(c => ToScreen(c.X, c.Y)).ToArray()
            : corners.Select(c => SourceToScreen(s.ActiveTransform.ToCanvas(c))).ToArray();
        for (int i = 0; i < pts.Length; i++)
        {
            context.DrawLine(SelectionShadowPen, pts[i], pts[(i + 1) % pts.Length]);
            context.DrawLine(SelectionPen, pts[i], pts[(i + 1) % pts.Length]);
        }
    }

    /// <summary>Bones between joints and a dot on every joint (the active one highlighted).</summary>
    private void DrawSkeleton(DrawingContext context, EditorSession s)
    {
        foreach (var (part, t) in s.Transforms)
        {
            if (part.Parent is not null)
            {
                context.DrawLine(BonePen, SourceToScreen(s.Transforms[part.Parent].Pivot), SourceToScreen(t.Pivot));
            }
        }
        foreach (var (part, t) in s.Transforms)
        {
            var brush = part == s.ActivePart ? ActiveJointBrush : JointBrush;
            context.DrawEllipse(brush, null, SourceToScreen(t.Pivot), 4, 4);
        }
    }

    // ------------------------------------------------------------------ input

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (Session is not { } s)
            return;
        Focus();
        var point = e.GetCurrentPoint(this);
        var props = point.Properties;

        if (props.IsMiddleButtonPressed || (_spaceDown && props.IsLeftButtonPressed))
        {
            _panning = true;
            _panStart = point.Position;
            _originAtPanStart = _origin;
        }
        else if (props.IsLeftButtonPressed || props.IsRightButtonPressed)
        {
            _interaction = s.PoseMode ? new PoseInteraction(s)
                : s.TouchupMode && Touchup is { } touchup ? new TouchupInteraction(s, touchup)
                : new DrawInteraction(s);
            _interaction.Begin(Input(point.Position, e, props.IsRightButtonPressed));
        }
        else
        {
            return;
        }
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (Session is not { } s)
            return;
        var pos = e.GetPosition(this);

        if (_panning)
        {
            _origin = _originAtPanStart + (pos - _panStart);
            InvalidateVisual();
            return;
        }

        _interaction?.Move(Input(pos, e));
        var canvas = ToCanvas(pos);

        var pixel = ((int)Math.Floor(canvas.X), (int)Math.Floor(canvas.Y));
        if (_hover != pixel)
        {
            _hover = pixel;
            bool inside = pixel.Item1 >= 0 && pixel.Item2 >= 0 && pixel.Item1 < s.Character.Width && pixel.Item2 < s.Character.Height;
            s.CursorText = inside ? $"{pixel.Item1}, {pixel.Item2}" : "";
            InvalidateVisual();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        // the last move events can be coalesced away, so finish at the release point
        _interaction?.Move(Input(e.GetPosition(this), e));
        EndInteraction();
        e.Pointer.Capture(null);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        EndInteraction();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hover = null;
        if (Session is { } s)
            s.CursorText = "";
        InvalidateVisual();
    }

    private void EndInteraction()
    {
        _interaction?.End();
        _interaction = null;
        _panning = false;
    }

    private CanvasInput Input(Point screen, PointerEventArgs e, bool secondary = false)
    {
        var display = ToCanvas(screen);
        return new(ToSource(screen), new System.Numerics.Vector2((float)display.X, (float)display.Y), secondary,
            e.KeyModifiers.HasFlag(KeyModifiers.Shift), e.KeyModifiers.HasFlag(KeyModifiers.Alt));
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (Session is not { } s || e.Delta.Y == 0)
            return;
        _zoomAnchor = e.GetPosition(this);
        s.ZoomBy(e.Delta.Y > 0 ? 1 : -1);
        _zoomAnchor = null;
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Space)
        {
            _spaceDown = true;
            Cursor = new Cursor(StandardCursorType.Hand);
            e.Handled = true;
        }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (e.Key == Key.Space)
        {
            _spaceDown = false;
            Cursor = Cursor.Default;
            e.Handled = true;
        }
    }
}
