using PixelAniMaker.Core.History;
using PixelAniMaker.Core.Imaging;

namespace PixelAniMaker.Core.Editing;

public enum ToolKind
{
    Pencil,
    Eraser,
    Fill,
    Eyedropper,
}

/// <summary>
/// One editable pixel image with its palette, colour selection and undo history.
/// Input comes in image pixel coordinates; the UI layer converts from screen space.
/// </summary>
public sealed class EditorDocument
{
    private PixelEditAction? _stroke;
    private ToolKind _strokeTool;
    private bool _strokeSecondary;
    private bool _eyedropping;
    private int _lastX, _lastY;
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

    // ------------------------------------------------------------------ strokes

    /// <param name="secondary">True for the right mouse button: draws with the secondary colour.</param>
    public void BeginStroke(ToolKind tool, int x, int y, bool secondary = false)
    {
        EndStroke();
        _strokeTool = tool;
        _strokeSecondary = secondary;
        _lastX = x;
        _lastY = y;

        switch (tool)
        {
            case ToolKind.Pencil:
            case ToolKind.Eraser:
                _stroke = new PixelEditAction(tool == ToolKind.Pencil ? "연필" : "지우개", Image, RaisePixelsChanged);
                if (_stroke.Set(x, y, StrokeIndex()))
                    RaisePixelsChanged();
                break;
            case ToolKind.Fill:
                var fill = new PixelEditAction("채우기", Image, RaisePixelsChanged);
                FloodFill(fill, x, y, StrokeIndex());
                Commit(fill);
                break;
            case ToolKind.Eyedropper:
                _eyedropping = true;
                PickColor(x, y);
                break;
        }
    }

    public void ContinueStroke(int x, int y)
    {
        if (_stroke is not null)
        {
            int index = StrokeIndex();
            bool changed = false;
            foreach (var (px, py) in Line(_lastX, _lastY, x, y))
                changed |= _stroke.Set(px, py, index);
            if (changed)
                RaisePixelsChanged();
        }
        else if (_eyedropping)
        {
            PickColor(x, y);
        }
        _lastX = x;
        _lastY = y;
    }

    public void EndStroke()
    {
        _eyedropping = false;
        if (_stroke is null)
            return;
        var stroke = _stroke;
        _stroke = null;
        Commit(stroke);
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

    // ------------------------------------------------------------------ helpers

    private int StrokeIndex() => _strokeTool == ToolKind.Eraser
        ? Palette.TransparentIndex
        : _strokeSecondary ? _secondaryIndex : _primaryIndex;

    private void Commit(PixelEditAction action)
    {
        if (action.IsEmpty)
            return;
        History.Push(action);
        RaisePixelsChanged();
    }

    private void PickColor(int x, int y)
    {
        if (!Image.InBounds(x, y))
            return;
        if (_strokeSecondary)
            SecondaryIndex = Image[x, y];
        else
            PrimaryIndex = Image[x, y];
    }

    private void SetColor(ref int field, int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(value, Palette.Count);
        if (field == value)
            return;
        field = value;
        ColorSelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RaisePixelsChanged() => PixelsChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>4-connected flood fill of the region that shares the colour at (x, y).</summary>
    private void FloodFill(PixelEditAction action, int x, int y, int index)
    {
        if (!Image.InBounds(x, y))
            return;
        int target = Image[x, y];
        if (target == index)
            return;
        var stack = new Stack<(int X, int Y)>();
        stack.Push((x, y));
        while (stack.Count > 0)
        {
            var (cx, cy) = stack.Pop();
            if (!Image.InBounds(cx, cy) || Image[cx, cy] != target)
                continue;
            action.Set(cx, cy, index);
            stack.Push((cx + 1, cy));
            stack.Push((cx - 1, cy));
            stack.Push((cx, cy + 1));
            stack.Push((cx, cy - 1));
        }
    }

    /// <summary>Bresenham line, both end points included.</summary>
    public static IEnumerable<(int X, int Y)> Line(int x0, int y0, int x1, int y1)
    {
        int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;
        while (true)
        {
            yield return (x0, y0);
            if (x0 == x1 && y0 == y1)
                yield break;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }
}
