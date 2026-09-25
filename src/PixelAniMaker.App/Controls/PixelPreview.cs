using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using PixelAniMaker.App.Services;
using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.App.Controls;

/// <summary>Shows one direction of the posed character at a fixed integer scale. Clicking it switches to that direction.</summary>
public sealed class PixelPreview : SessionControl
{
    public static readonly StyledProperty<int> ScaleProperty =
        AvaloniaProperty.Register<PixelPreview, int>(nameof(Scale), 1);

    public static readonly StyledProperty<Direction> DirectionProperty =
        AvaloniaProperty.Register<PixelPreview, Direction>(nameof(Direction));

    private static readonly IPen CurrentPen = new Pen(new SolidColorBrush(Color.FromRgb(77, 163, 255)), 2);

    static PixelPreview()
    {
        AffectsMeasure<PixelPreview>(SessionProperty, ScaleProperty);
        AffectsRender<PixelPreview>(ScaleProperty, DirectionProperty);
    }

    public PixelPreview() => Cursor = new Cursor(StandardCursorType.Hand);

    public int Scale
    {
        get => GetValue(ScaleProperty);
        set => SetValue(ScaleProperty, value);
    }

    public Direction Direction
    {
        get => GetValue(DirectionProperty);
        set => SetValue(DirectionProperty, value);
    }

    protected override void OnSessionPropertyChanged(string? propertyName)
    {
        if (propertyName is nameof(EditorSession.Character) or nameof(EditorSession.CanvasSize))
            InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size availableSize) =>
        Session is { } s ? new Size(s.Character.Width * Scale, s.Character.Height * Scale) : default;

    public override void Render(DrawingContext context)
    {
        if (Session is not { } s)
            return;
        int w = s.Character.Width, h = s.Character.Height;
        var dest = new Rect(0, 0, w * Scale, h * Scale);
        DrawImage(context, s.PreviewBitmap(Direction), w, h, dest);
        if (s.Direction == Direction)
            context.DrawRectangle(null, CurrentPen, dest.Deflate(1));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (Session is { } s)
            s.Direction = Direction;
        e.Handled = true;
    }
}
