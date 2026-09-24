using PixelAniMaker.Core.History;
using PixelAniMaker.Core.Imaging;

namespace PixelAniMaker.Core.Editing;

/// <summary>
/// Drawing target: one pixel image plus the palette, undo history and colour selection it shares with
/// the rest of the project. Input comes in image pixel coordinates.
/// </summary>
public sealed class EditorDocument
{
    private IStroke? _stroke;

    public EditorDocument(IndexedImage image, Palette palette, UndoHistory history, ColorSelection colors)
    {
        Image = image;
        Palette = palette;
        History = history;
        Colors = colors;
    }

    /// <summary>A standalone document with its own palette (black, white) and history.</summary>
    public static EditorDocument CreateBlank(int width, int height)
    {
        var palette = new Palette();
        palette.GetOrAdd(new Rgba(0, 0, 0));
        palette.GetOrAdd(new Rgba(255, 255, 255));
        return new EditorDocument(new IndexedImage(width, height), palette, new UndoHistory(), new ColorSelection(palette));
    }

    public IndexedImage Image { get; }
    public Palette Palette { get; }
    public UndoHistory History { get; }
    public ColorSelection Colors { get; }

    /// <summary>Raised while a stroke changes pixels. Undo/redo are reported through <see cref="History"/>.</summary>
    public event EventHandler? PixelsChanged;

    // ------------------------------------------------------------------ strokes

    public void BeginStroke(ITool tool, int x, int y, bool secondary = false)
    {
        EndStroke();
        _stroke = tool.Begin(this, x, y, secondary);
    }

    public void ContinueStroke(int x, int y) => _stroke?.Move(x, y);

    public void EndStroke()
    {
        var stroke = _stroke;
        _stroke = null;
        stroke?.End();
    }

    public void Undo()
    {
        EndStroke();
        History.Undo();
    }

    public void Redo()
    {
        EndStroke();
        History.Redo();
    }

    // ------------------------------------------------------------------ tool API

    /// <summary>Starts a recorded pixel change; pass it to <see cref="Commit"/> when finished.</summary>
    public PixelEdit BeginEdit(string name) => new(name, Image, NotifyPixelsChanged);

    /// <summary>Adds a finished edit to the history (ignored when nothing changed).</summary>
    public void Commit(PixelEdit edit)
    {
        if (edit.IsEmpty)
            return;
        History.Push(edit);
        NotifyPixelsChanged();
    }

    public void NotifyPixelsChanged() => PixelsChanged?.Invoke(this, EventArgs.Empty);
}
