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
        if (propertyName == nameof(EditorSession.Document))
            InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size availableSize) =>
        Session is { } s
            ? new Size(s.Document.Image.Width * Scale, s.Document.Image.Height * Scale)
            : default;

    public override void Render(DrawingContext context)
    {
        if (Session is not { } s)
            return;
        var img = s.Document.Image;
        DrawImage(context, s, new Rect(0, 0, img.Width * Scale, img.Height * Scale));
    }
}
