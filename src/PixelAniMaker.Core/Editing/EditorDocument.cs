using PixelAniMaker.Core.History;
using PixelAniMaker.Core.Imaging;

namespace PixelAniMaker.Core.Editing;

/// <summary>
/// One editable pixel image with its palette, colour selection and undo history.
/// Input comes in image pixel coordinates; the UI layer converts from screen space.
/// </summary>
public sealed class EditorDocument
{
    private IStroke? _stroke;
    private int _primaryIndex;
    private int _secondaryIndex = Palette.TransparentIndex;

    public EditorDocument(IndexedImage image, Palette palette)
    {
        Image = image;
        Palette = palette;
        _primaryIndex = palette.Count > 1 ? 1 : Palette.TransparentIndex;
    }

    public static EditorDocument CreateBlank(int width, int height)
    {
        var palette = new Palette();
        palette.GetOrAdd(new Rgba(0, 0, 0));
        palette.GetOrAdd(new Rgba(255, 255, 255));
        return new EditorDocument(new IndexedImage(width, height), palette);
    }

    public IndexedImage Image { get; }
    public Palette Palette { get; }
    public UndoHistory History { get; } = new();

    /// <summary>Raised whenever pixels change (drawing, undo, redo).</summary>
    public event EventHandler? PixelsChanged;

    /// <summary>Raised when the primary or secondary colour changes.</summary>
    public event EventHandler? ColorSelectionChanged;

    public int PrimaryIndex
    {
        get => _primaryIndex;
        set => SetColor(ref _primaryIndex, value);
    }

    public int SecondaryIndex
    {
        get => _secondaryIndex;
        set => SetColor(ref _secondaryIndex, value);
    }

    public int ColorIndex(bool secondary) => secondary ? _secondaryIndex : _primaryIndex;

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

    private void SetColor(ref int field, int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(value, Palette.Count);
        if (field == value)
            return;
        field = value;
        ColorSelectionChanged?.Invoke(this, EventArgs.Empty);
    }
}
