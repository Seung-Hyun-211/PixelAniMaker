using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PixelAniMaker.App.Services;

namespace PixelAniMaker.App.Controls;

/// <summary>
/// Base for controls that render the session's image: nearest-neighbour scaling, repaint on
/// image updates, and a hook for session property changes.
/// </summary>
public abstract class SessionControl : Control
{
    public static readonly StyledProperty<EditorSession?> SessionProperty =
        AvaloniaProperty.Register<SessionControl, EditorSession?>(nameof(Session));

    static SessionControl() => AffectsRender<SessionControl>(SessionProperty);

    protected SessionControl() => RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);

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
            old.PropertyChanged -= OnSessionPropertyChangedCore;
        }
        if (change.NewValue is EditorSession session)
        {
            session.ImageUpdated += OnImageUpdated;
            session.PropertyChanged += OnSessionPropertyChangedCore;
        }
        OnSessionAttached();
    }

    /// <summary>Called after <see cref="Session"/> is set or replaced.</summary>
    protected virtual void OnSessionAttached() { }

    /// <summary>Called when a session property changes; the control is repainted afterwards.</summary>
    protected virtual void OnSessionPropertyChanged(string? propertyName) { }

    /// <summary>Draws the checkerboard and a <paramref name="width"/>×<paramref name="height"/> bitmap into <paramref name="dest"/>.</summary>
    protected static void DrawImage(DrawingContext context, Bitmap bitmap, int width, int height, Rect dest)
    {
        context.FillRectangle(CheckerBrushes.Large, dest);
        context.DrawImage(bitmap, new Rect(0, 0, width, height), dest);
    }

    private void OnImageUpdated(object? sender, EventArgs e) => InvalidateVisual();

    private void OnSessionPropertyChangedCore(object? sender, PropertyChangedEventArgs e)
    {
        OnSessionPropertyChanged(e.PropertyName);
        InvalidateVisual();
    }
}
