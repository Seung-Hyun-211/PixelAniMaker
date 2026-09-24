using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PixelAniMaker.App.Services;

namespace PixelAniMaker.App.Controls;

/// <summary>
/// Zoomable pixel editing surface. Left button draws with the primary colour, right button with the
/// secondary colour, middle button or Space+drag pans, the wheel zooms around the cursor.
/// </summary>
public sealed class PixelCanvas : Control
{
    public static readonly StyledProperty<EditorSession?> SessionProperty =
        AvaloniaProperty.Register<PixelCanvas, EditorSession?>(nameof(Session));

    private static readonly IBrush Background = new SolidColorBrush(Color.FromRgb(37, 37, 40));
    private static readonly IPen GridPen = new Pen(new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), 1);
    private static readonly IPen BorderPen = new Pen(new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)), 1);
    private static readonly IPen HoverPen = new Pen(new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)), 1);

    private Point _origin;          // screen position of the image's top-left corner
    private bool _placed;
    private bool _panning;
    private bool _drawing;
    private bool _spaceDown;
    private Point _panStart;
    private Point _originAtPanStart;
    private (int X, int Y)? _hover;

    static PixelCanvas()
    {
        FocusableProperty.OverrideDefaultValue<PixelCanvas>(true);
        ClipToBoundsProperty.OverrideDefaultValue<PixelCanvas>(true);
        AffectsRender<PixelCanvas>(SessionProperty);
    }

    public PixelCanvas() => RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);

    public EditorSession? Session
    {
        get => GetValue(SessionProperty);
        set => SetValue(SessionProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != SessionProperty)
            return;
        if (change.OldValue is EditorSession old)
        {
            old.ImageUpdated -= OnImageUpdated;
            old.PropertyChanged -= OnSessionPropertyChanged;
        }
        if (change.NewValue is EditorSession s)
        {
            s.ImageUpdated += OnImageUpdated;
            s.PropertyChanged += OnSessionPropertyChanged;
        }
        PlaceImage(Bounds.Size);
    }

    private void OnImageUpdated(object? sender, EventArgs e) => InvalidateVisual();

    private void OnSessionPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditorSession.Document))
            PlaceImage(Bounds.Size);
        if (e.PropertyName == nameof(EditorSession.Zoom) && Session is { } s)
        {
            // zoom around the cursor for wheel input, around the view centre otherwise
            ZoomAround(_zoomAnchor ?? new Point(Bounds.Width / 2, Bounds.Height / 2), _lastZoom);
            _lastZoom = s.Zoom;
            _zoomAnchor = null;
        }
        InvalidateVisual();
    }

    private Point? _zoomAnchor;
    private int _lastZoom = 4;

    // ------------------------------------------------------------------ layout

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (!_placed)
            PlaceImage(e.NewSize);
    }

    /// <summary>Centres the image and picks the largest zoom step that fits.</summary>
    private void PlaceImage(Size size)
    {
        _placed = false;
        if (Session is not { } s || size.Width <= 0 || size.Height <= 0)
            return;
        var img = s.Document.Image;
        double fit = Math.Min(size.Width / img.Width, size.Height / img.Height) * 0.9;
        int zoom = EditorSession.ZoomSteps.LastOrDefault(z => z <= fit, 1);
        _lastZoom = zoom;
        s.Zoom = zoom;
        _origin = new Point(Math.Round((size.Width - img.Width * zoom) / 2), Math.Round((size.Height - img.Height * zoom) / 2));
        _placed = true;
        InvalidateVisual();
    }

    private void ZoomAround(Point anchor, int oldZoom)
    {
        if (Session is not { } s || oldZoom == s.Zoom)
            return;
        // keep the image point under the anchor fixed
        var imagePoint = (anchor - _origin) / oldZoom;
        _origin = new Point(Math.Round(anchor.X - imagePoint.X * s.Zoom), Math.Round(anchor.Y - imagePoint.Y * s.Zoom));
    }

    // ------------------------------------------------------------------ rendering

    public override void Render(DrawingContext context)
    {
        context.FillRectangle(Background, new Rect(Bounds.Size));
        if (Session is not { } s || !_placed)
            return;

        var img = s.Document.Image;
        int z = s.Zoom;
        var dest = new Rect(_origin.X, _origin.Y, img.Width * z, img.Height * z);

        context.FillRectangle(CheckerBrushes.Large, dest);
        if (s.ShowTemplate && s.Template is { } template)
        {
            using (context.PushOpacity(0.35))
                context.DrawImage(template, new Rect(template.Size), dest);
        }
        context.DrawImage(s.Bitmap, new Rect(0, 0, img.Width, img.Height), dest);

        if (s.ShowGrid && z >= 6)
        {
            for (int x = 1; x < img.Width; x++)
            {
                double px = dest.X + x * z + 0.5;
                context.DrawLine(GridPen, new Point(px, dest.Top), new Point(px, dest.Bottom));
            }
            for (int y = 1; y < img.Height; y++)
            {
                double py = dest.Y + y * z + 0.5;
                context.DrawLine(GridPen, new Point(dest.Left, py), new Point(dest.Right, py));
            }
        }
        context.DrawRectangle(null, BorderPen, dest.Inflate(0.5));

        if (_hover is var (hx, hy) && img.InBounds(hx, hy))
            context.DrawRectangle(null, HoverPen, new Rect(dest.X + hx * z + 0.5, dest.Y + hy * z + 0.5, z - 1, z - 1));
    }

    // ------------------------------------------------------------------ input

    private (int X, int Y) ToPixel(Point p)
    {
        int z = Session!.Zoom;
        return ((int)Math.Floor((p.X - _origin.X) / z), (int)Math.Floor((p.Y - _origin.Y) / z));
    }

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
            var (x, y) = ToPixel(point.Position);
            _drawing = true;
            s.Document.BeginStroke(s.CurrentTool, x, y, secondary: props.IsRightButtonPressed);
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

        var (x, y) = ToPixel(pos);
        if (_drawing)
            s.Document.ContinueStroke(x, y);

        if (_hover != (x, y))
        {
            _hover = (x, y);
            s.CursorText = s.Document.Image.InBounds(x, y) ? $"{x}, {y}" : "";
            InvalidateVisual();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        // the last move events can be coalesced away, so finish the stroke at the release point
        if (_drawing && Session is { } s)
        {
            var (x, y) = ToPixel(e.GetPosition(this));
            s.Document.ContinueStroke(x, y);
        }
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
        if (_drawing)
            Session?.Document.EndStroke();
        _drawing = false;
        _panning = false;
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

    /// <summary>Re-centres the image and picks a zoom that fits the view.</summary>
    public void FitToView() => PlaceImage(Bounds.Size);
}
