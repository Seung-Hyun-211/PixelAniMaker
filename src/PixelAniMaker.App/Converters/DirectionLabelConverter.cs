using System.Globalization;
using Avalonia.Data.Converters;
using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.App.Converters;

/// <summary>Direction → Korean label (<see cref="Instance"/>) or its shortcut id (<see cref="ShortcutId"/>).</summary>
public sealed class DirectionLabelConverter(bool shortcutId) : IValueConverter
{
    public static DirectionLabelConverter Instance { get; } = new(false);
    public static DirectionLabelConverter ShortcutId { get; } = new(true);

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Direction d ? shortcutId ? $"Direction.{d}" : d.Label() : value;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>[label, shortcut id, shortcut texts] → "label  (key)", or just the label when there is no key.</summary>
public sealed class ShortcutLabelConverter : IMultiValueConverter
{
    public static ShortcutLabelConverter Instance { get; } = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        var label = values.Count > 0 ? values[0] as string ?? "" : "";
        if (values.Count > 2 && values[1] is string id && values[2] is IReadOnlyDictionary<string, string> texts
            && texts.GetValueOrDefault(id) is { Length: > 0 } key)
            return $"{label}  ({key})";
        return label;
    }
}
