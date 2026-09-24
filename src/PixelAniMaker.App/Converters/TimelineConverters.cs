using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using PixelAniMaker.Core.Animation;

namespace PixelAniMaker.App.Converters;

/// <summary>0-based frame index → 1-based frame number.</summary>
public sealed class FrameNumberConverter : IValueConverter
{
    public static FrameNumberConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int i ? (i + 1).ToString(culture) : value;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Easing → Korean label.</summary>
public sealed class EasingLabelConverter : IValueConverter
{
    public static EasingLabelConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Easing e ? e.Label() : value;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>true → highlight brush, false → transparent.</summary>
public sealed class BoolBrushConverter(IBrush whenTrue) : IValueConverter
{
    public static BoolBrushConverter Current { get; } = new(new SolidColorBrush(Color.FromRgb(77, 163, 255)));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? whenTrue : Brushes.Transparent;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
