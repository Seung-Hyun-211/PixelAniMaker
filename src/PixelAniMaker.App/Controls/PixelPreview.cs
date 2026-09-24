using Avalonia;
using Avalonia.Media;
using PixelAniMaker.App.Services;

namespace PixelAniMaker.App.Controls;

/// <summary>Shows the current image at a fixed integer scale (actual size preview).</summary>
public sealed class PixelPreview : SessionControl
{
    public static readonly StyledProperty<int> ScaleProperty =
        AvaloniaProperty.Register<PixelPreview, int>(nameof(Scale), 1);

    static PixelPreview()
    {
        AffectsMeasure<PixelPreview>(SessionProperty, ScaleProperty);
        AffectsRender<PixelPreview>(ScaleProperty);
    }

    public int Scale
    {
        get => GetValue(ScaleProperty);
        set => SetValue(ScaleProperty, value);
    }

    protected override void OnSessionPropertyChanged(string? propertyName)
    {
        if (propertyName == nameof(EditorSession.Character))
            InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size availableSize) =>
        Session is { } s ? new Size(s.Character.Width * Scale, s.Character.Height * Scale) : default;

    public override void Render(DrawingContext context)
    {
        if (Session is not { } s)
            return;
        int w = s.Character.Width, h = s.Character.Height;
        DrawImage(context, s.PreviewBitmap, w, h, new Rect(0, 0, w * Scale, h * Scale));
    }
}
