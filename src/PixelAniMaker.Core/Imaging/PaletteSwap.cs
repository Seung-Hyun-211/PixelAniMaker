using PixelAniMaker.Core.History;

namespace PixelAniMaker.Core.Imaging;

/// <summary>
/// Undoable change of the whole palette from an imported colour list. <see cref="Swap"/> replaces
/// colours by index — the drawing keeps its colour numbers, so this makes a colour variant of the
/// character; <see cref="Merge"/> only adds the colours that are missing.
/// </summary>
public sealed class PaletteSwap(Palette palette, string name, IReadOnlyList<Rgba> before, IReadOnlyList<Rgba> after) : IUndoableAction
{
    public string Name => name;

    public void Undo() => palette.ReplaceAll(before);

    public void Redo() => palette.ReplaceAll(after);

    /// <summary>Colour i of the list goes to palette index i + 1; extra imported colours are appended.</summary>
    public static IReadOnlyList<Rgba> Swapped(Palette palette, IReadOnlyList<Rgba> imported)
    {
        var current = palette.Colors.Skip(1).ToList();
        int count = Math.Max(current.Count, imported.Count);
        return Enumerable.Range(0, count).Select(i => i < imported.Count ? imported[i] : current[i]).ToList();
    }

    /// <summary>The current colours followed by the imported ones that are not in the palette yet.</summary>
    public static IReadOnlyList<Rgba> Merged(Palette palette, IReadOnlyList<Rgba> imported)
    {
        var current = palette.Colors.Skip(1).ToList();
        return [.. current, .. imported.Where(c => !c.IsTransparent).Distinct().Except(current)];
    }

    /// <summary>A copy of the palette as it would be after <see cref="Swap"/> (for previews).</summary>
    public static Palette Preview(Palette palette, IReadOnlyList<Rgba> imported)
    {
        var copy = palette.Clone();
        copy.ReplaceAll(Swapped(palette, imported));
        return copy;
    }

    public static void Swap(Palette palette, UndoHistory history, IReadOnlyList<Rgba> imported) =>
        Apply(palette, history, "팔레트 교체", Swapped(palette, imported));

    public static void Merge(Palette palette, UndoHistory history, IReadOnlyList<Rgba> imported) =>
        Apply(palette, history, "팔레트 색 추가", Merged(palette, imported));

    private static void Apply(Palette palette, UndoHistory history, string name, IReadOnlyList<Rgba> after)
    {
        var before = palette.Colors.Skip(1).ToList();
        if (before.SequenceEqual(after))
            return;
        var change = new PaletteSwap(palette, name, before, after);
        change.Redo();
        history.Push(change);
    }
}
