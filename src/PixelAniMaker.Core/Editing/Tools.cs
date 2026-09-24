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

/// <summary>
/// Freehand line. With <paramref name="fixedIndex"/> it paints that index regardless of colour
/// selection. With <paramref name="pixelPerfect"/> the extra corner pixel of every L-shaped step is
/// removed, so freehand lines stay one pixel thin.
/// </summary>
public sealed class PencilTool(string name, int? fixedIndex = null, bool pixelPerfect = false) : ITool
{
    public static PencilTool Pencil { get; } = new("연필");
    public static PencilTool PixelPerfectPencil { get; } = new("연필", pixelPerfect: true);
    public static PencilTool Eraser { get; } = new("지우개", Palette.TransparentIndex);

    public IStroke Begin(EditorDocument document, int x, int y, bool secondary) =>
        new Stroke(document, document.BeginEdit(name), fixedIndex ?? document.Colors.Get(secondary), pixelPerfect, x, y);

    private sealed class Stroke : IStroke
    {
        private readonly EditorDocument _document;
        private readonly PixelEdit _edit;
        private readonly int _index;
        private readonly bool _pixelPerfect;
        private readonly List<(int X, int Y)> _path = [];

        public Stroke(EditorDocument document, PixelEdit edit, int index, bool pixelPerfect, int x, int y)
        {
            (_document, _edit, _index, _pixelPerfect) = (document, edit, index, pixelPerfect);
            Paint(Raster.Line(x, y, x, y));
        }

        public void Move(int x, int y)
        {
            var (lx, ly) = _path[^1];
            Paint(Raster.Line(lx, ly, x, y).Skip(1));
        }

        public void End() => _document.Commit(_edit);

        private void Paint(IEnumerable<(int X, int Y)> points)
        {
            bool changed = false;
            foreach (var p in points)
            {
                changed |= _edit.Set(p.X, p.Y, _index);
                _path.Add(p);
                if (_pixelPerfect && _path.Count >= 3 && IsCorner(_path[^3], _path[^2], _path[^1]))
                {
                    var (cx, cy) = _path[^2];
                    changed |= _edit.Set(cx, cy, _edit.Original(cx, cy));
                    _path.RemoveAt(_path.Count - 2);
                }
            }
            if (changed)
                _document.NotifyPixelsChanged();
        }

        /// <summary>b is the elbow of an L between a and c (a and c touch diagonally).</summary>
        private static bool IsCorner((int X, int Y) a, (int X, int Y) b, (int X, int Y) c) =>
            Math.Abs(a.X - c.X) == 1 && Math.Abs(a.Y - c.Y) == 1 && (a.X == b.X || a.Y == b.Y) && (c.X == b.X || c.Y == b.Y);
    }
}

/// <summary>
/// Drag outside the selection to select a rectangle; drag inside it to move the selected pixels
/// (transparent pixels are not carried, the hole left behind becomes transparent). A click without
/// dragging outside the selection clears it.
/// </summary>
public sealed class SelectMoveTool : ITool
{
    public static SelectMoveTool Instance { get; } = new();

    public IStroke Begin(EditorDocument document, int x, int y, bool secondary) =>
        document.Selection is { } s && s.Contains(x, y)
            ? new MoveStroke(document, s, x, y)
            : new SelectStroke(document, x, y);

    private sealed class SelectStroke : IStroke
    {
        private readonly EditorDocument _document;
        private readonly int _x0, _y0;
        private bool _dragged;

        public SelectStroke(EditorDocument document, int x, int y)
        {
            (_document, _x0, _y0) = (document, x, y);
            document.Selection = null;
        }

        public void Move(int x, int y)
        {
            _dragged |= x != _x0 || y != _y0;
            if (_dragged)
                _document.Selection = PixelRect.FromCorners(_x0, _y0, x, y);
        }

        public void End() { }
    }

    private sealed class MoveStroke : IStroke
    {
        private readonly EditorDocument _document;
        private readonly PixelEdit _edit;
        private readonly PixelRect _source;
        private readonly int[] _lifted;
        private readonly int _x0, _y0;
        private int _dx, _dy;

        public MoveStroke(EditorDocument document, PixelRect source, int x, int y)
        {
            (_document, _source, _x0, _y0) = (document, source, x, y);
            _edit = document.BeginEdit("선택 이동");
            _lifted = source.Pixels().Select(p => document.Image[p.X, p.Y]).ToArray();
        }

        public void Move(int x, int y)
        {
            (_dx, _dy) = (x - _x0, y - _y0);
            _edit.RevertAll();
            foreach (var (px, py) in _source.Pixels())
                _edit.Set(px, py, Palette.TransparentIndex);
            int i = 0;
            foreach (var (px, py) in _source.Pixels())
            {
                int value = _lifted[i++];
                if (value != Palette.TransparentIndex)
                    _edit.Set(px + _dx, py + _dy, value);
            }
            _document.Selection = _source.Offset(_dx, _dy);
            _document.NotifyPixelsChanged();
        }

        public void End() => _document.Commit(_edit);
    }
}

/// <summary>A shape dragged from a start point; the preview is redrawn on every move and kept on release.</summary>
public sealed class ShapeTool(string name, Func<int, int, int, int, IEnumerable<(int X, int Y)>> shape) : ITool
{
    public static ShapeTool Line { get; } = new("선", Raster.Line);
    public static ShapeTool Rectangle { get; } = new("사각형", Raster.Rectangle);
    public static ShapeTool Ellipse { get; } = new("원", Raster.Ellipse);

    public IStroke Begin(EditorDocument document, int x, int y, bool secondary)
    {
        var stroke = new Stroke(document, document.BeginEdit(name), document.Colors.Get(secondary), x, y, shape);
        stroke.Move(x, y);
        return stroke;
    }

    private sealed class Stroke(EditorDocument document, PixelEdit edit, int index, int x0, int y0,
        Func<int, int, int, int, IEnumerable<(int X, int Y)>> shape) : IStroke
    {
        public void Move(int x, int y)
        {
            edit.RevertAll();
            foreach (var (px, py) in shape(x0, y0, x, y))
                edit.Set(px, py, index);
            document.NotifyPixelsChanged();
        }

        public void End() => document.Commit(edit);
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
