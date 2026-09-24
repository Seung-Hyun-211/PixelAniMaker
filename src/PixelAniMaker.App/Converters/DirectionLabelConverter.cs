using System.Globalization;
using Avalonia.Data.Converters;
using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.App.Converters;

/// <summary>Direction → Korean label with its shortcut, e.g. "좌측면 (2)".</summary>
public sealed class DirectionLabelConverter : IValueConverter
{
    public static DirectionLabelConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Direction d ? $"{d.Label()} ({(int)d + 1})" : value;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
