using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PixelAniMaker.App.Services;
using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.App.Controls;

/// <summary>Shows the baked frame at the animation's playback position for one direction.</summary>
public sealed class AnimationPlayer : Control
{
    public static readonly StyledProperty<AnimationSession?> AnimationProperty =
        AvaloniaProperty.Register<AnimationPlayer, AnimationSession?>(nameof(Animation));

    public static readonly StyledProperty<Direction> DirectionProperty =
        AvaloniaProperty.Register<AnimationPlayer, Direction>(nameof(Direction));

    public static readonly StyledProperty<int> ScaleProperty =
        AvaloniaProperty.Register<AnimationPlayer, int>(nameof(Scale), 1);

    static AnimationPlayer()
    {
        AffectsMeasure<AnimationPlayer>(ScaleProperty);
        AffectsRender<AnimationPlayer>(AnimationProperty, DirectionProperty, ScaleProperty);
    }

    public AnimationPlayer() => RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);

    public AnimationSession? Animation
    {
        get => GetValue(AnimationProperty);
        set => SetValue(AnimationProperty, value);
    }

    public Direction Direction
    {
        get => GetValue(DirectionProperty);
        set => SetValue(DirectionProperty, value);
    }

    public int Scale
    {
        get => GetValue(ScaleProperty);
        set => SetValue(ScaleProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != AnimationProperty)
            return;
        if (change.OldValue is AnimationSession old)
        {
            old.FramesUpdated -= OnFramesUpdated;
            old.PropertyChanged -= OnAnimationPropertyChanged;
        }
        if (change.NewValue is AnimationSession a)
        {
            a.FramesUpdated += OnFramesUpdated;
            a.PropertyChanged += OnAnimationPropertyChanged;
        }
        InvalidateMeasure();
    }

    private void OnFramesUpdated(object? sender, EventArgs e) => InvalidateMeasure();

    private void OnAnimationPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AnimationSession.PlaybackFrame))
            InvalidateVisual();
    }

    private Bitmap? CurrentBitmap => Animation?.Frame(Direction, Animation.PlaybackFrame);

    protected override Size MeasureOverride(Size availableSize) =>
        CurrentBitmap is { } b ? new Size(b.PixelSize.Width * Scale, b.PixelSize.Height * Scale) : default;

    public override void Render(DrawingContext context)
    {
        if (CurrentBitmap is not { } bitmap)
            return;
        var dest = new Rect(0, 0, bitmap.PixelSize.Width * Scale, bitmap.PixelSize.Height * Scale);
        context.FillRectangle(CheckerBrushes.Large, dest);
        context.DrawImage(bitmap, new Rect(bitmap.Size), dest);
    }
}
