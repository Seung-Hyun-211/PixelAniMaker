using PixelAniMaker.Core.Imaging;

namespace PixelAniMaker.Core.Editing;

/// <summary>A drawing tool. New tools are added as new implementations; the document never changes.</summary>
public interface ITool
{
    /// <summary>Starts an interaction. Returns the stroke to feed with further moves, or null when done.</summary>
    /// <param name="secondary">True for the right mouse button (secondary colour).</param>
    IStroke? Begin(EditorDocument document, int x, int y, bool secondary);
}

/// <summary>An interaction in progress (between mouse down and mouse up).</summary>
public interface IStroke
{
    void Move(int x, int y);
    void End();
}

/// <summary>Freehand line. With <paramref name="fixedIndex"/> it paints that index regardless of colour selection.</summary>
public sealed class PencilTool(string name, int? fixedIndex = null) : ITool
{
    public static PencilTool Pencil { get; } = new("연필");
    public static PencilTool Eraser { get; } = new("지우개", Palette.TransparentIndex);

    public IStroke Begin(EditorDocument document, int x, int y, bool secondary) =>
        new Stroke(document, document.BeginEdit(name), fixedIndex ?? document.Colors.Get(secondary), x, y);

    private sealed class Stroke : IStroke
    {
        private readonly EditorDocument _document;
        private readonly PixelEdit _edit;
        private readonly int _index;
        private int _lastX, _lastY;

        public Stroke(EditorDocument document, PixelEdit edit, int index, int x, int y)
        {
            (_document, _edit, _index, _lastX, _lastY) = (document, edit, index, x, y);
            Paint(Raster.Line(x, y, x, y));
        }

        public void Move(int x, int y)
        {
            Paint(Raster.Line(_lastX, _lastY, x, y));
            (_lastX, _lastY) = (x, y);
        }

        public void End() => _document.Commit(_edit);

        private void Paint(IEnumerable<(int X, int Y)> points)
        {
            bool changed = false;
            foreach (var (px, py) in points)
                changed |= _edit.Set(px, py, _index);
            if (changed)
                _document.NotifyPixelsChanged();
        }
    }
}

/// <summary>Fills the 4-connected region under the cursor.</summary>
public sealed class FillTool : ITool
{
    public static FillTool Instance { get; } = new();

    public IStroke? Begin(EditorDocument document, int x, int y, bool secondary)
    {
        var edit = document.BeginEdit("채우기");
        int index = document.Colors.Get(secondary);
        foreach (var (px, py) in Raster.FloodRegion(document.Image, x, y).ToList())
            edit.Set(px, py, index);
        document.Commit(edit);
        return null;
    }
}

/// <summary>Picks the colour under the cursor into the primary (or secondary) slot while dragging.</summary>
public sealed class EyedropperTool : ITool
{
    public static EyedropperTool Instance { get; } = new();

    public IStroke Begin(EditorDocument document, int x, int y, bool secondary)
    {
        var stroke = new Stroke(document, secondary);
        stroke.Move(x, y);
        return stroke;
    }

    private sealed class Stroke(EditorDocument document, bool secondary) : IStroke
    {
        public void Move(int x, int y)
        {
            if (document.Image.InBounds(x, y))
                document.Colors.Set(secondary, document.Image[x, y]);
        }

        public void End() { }
    }
}
