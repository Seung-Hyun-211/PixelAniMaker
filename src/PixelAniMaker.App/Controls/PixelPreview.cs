using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PixelAniMaker.App.Services;

namespace PixelAniMaker.App.Controls;

/// <summary>Shows the current image at a fixed integer scale (actual size preview).</summary>
public sealed class PixelPreview : Control
{
    public static readonly StyledProperty<EditorSession?> SessionProperty =
        AvaloniaProperty.Register<PixelPreview, EditorSession?>(nameof(Session));

    public static readonly StyledProperty<int> ScaleProperty =
        AvaloniaProperty.Register<PixelPreview, int>(nameof(Scale), 1);

    static PixelPreview()
    {
        AffectsMeasure<PixelPreview>(SessionProperty, ScaleProperty);
        AffectsRender<PixelPreview>(SessionProperty, ScaleProperty);
    }

    public PixelPreview() => RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);

    public EditorSession? Session
    {
        get => GetValue(SessionProperty);
        set => SetValue(SessionProperty, value);
    }

    public int Scale
    {
        get => GetValue(ScaleProperty);
        set => SetValue(ScaleProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != SessionProperty)
            return;
        if (change.OldValue is EditorSession old)
        {
            old.ImageUpdated -= OnImageUpdated;
            old.PropertyChanged -= OnSessionChanged;
        }
        if (change.NewValue is EditorSession s)
        {
            s.ImageUpdated += OnImageUpdated;
            s.PropertyChanged += OnSessionChanged;
        }
    }

    private void OnImageUpdated(object? sender, EventArgs e) => InvalidateVisual();

    private void OnSessionChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditorSession.Document))
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
        var dest = new Rect(0, 0, img.Width * Scale, img.Height * Scale);
        context.FillRectangle(CheckerBrushes.Large, dest);
        context.DrawImage(s.Bitmap, new Rect(0, 0, img.Width, img.Height), dest);
    }
}
