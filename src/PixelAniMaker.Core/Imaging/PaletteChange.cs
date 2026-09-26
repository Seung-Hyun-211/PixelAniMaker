using PixelAniMaker.Core.History;

namespace PixelAniMaker.Core.Imaging;

/// <summary>Undoable replacement of one palette colour (every pixel using it changes too).</summary>
public sealed class PaletteChange(Palette palette, int index, Rgba before, Rgba after) : IUndoableAction
{
    public string Name => "색 바꾸기";

    public void Undo() => palette.Set(index, before);

    public void Redo() => palette.Set(index, after);

    public static void Apply(Palette palette, UndoHistory history, int index, Rgba color)
    {
        var before = palette[index];
        if (before == color)
            return;
        var change = new PaletteChange(palette, index, before, color);
        history.Do(change);
    }
}
