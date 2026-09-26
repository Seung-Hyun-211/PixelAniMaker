using System.Numerics;
using PixelAniMaker.Core.Editing;
using PixelAniMaker.Core.History;
using PixelAniMaker.Core.Imaging;

namespace PixelAniMaker.Core.Rigging;

/// <summary>
/// Grows and trims a part view's drawing area, so strokes can go past the shape the part started with
/// (hair past the head, a skirt past the hips). Every layer keeps the same size; the view's rest
/// position and attachment points move so the drawing stays exactly where it was on the canvas.
/// </summary>
public static class PartViewBounds
{
    /// <summary>Empty pixels kept around the drawing when a view is trimmed.</summary>
    public const int Padding = 2;

    /// <summary>The view's current area in its own image pixels.</summary>
    public static PixelRect Current(PartView view) =>
        new(0, 0, view.Layers[0].Image.Width - 1, view.Layers[0].Image.Height - 1);

    /// <summary>
    /// The view's area grown to cover the whole canvas as the part is placed now (in its image pixels;
    /// may start below 0). A rotated part gets the box around the turned canvas.
    /// </summary>
    public static PixelRect CoveringCanvas(PartTransform t, int width, int height)
    {
        Vector2[] corners = [new(0, 0), new(width, 0), new(0, height), new(width, height)];
        var local = corners.Select(t.ToLocal).ToList();
        var cover = new PixelRect(
            (int)MathF.Floor(local.Min(p => p.X)), (int)MathF.Floor(local.Min(p => p.Y)),
            (int)MathF.Ceiling(local.Max(p => p.X)), (int)MathF.Ceiling(local.Max(p => p.Y)));
        return Union(cover, Current(t.View));
    }

    /// <summary>
    /// The smallest area that still holds <paramref name="keep"/> and every drawn pixel of every layer,
    /// with <see cref="Padding"/> around pixels drawn outside <paramref name="keep"/>; limited to the current area.
    /// </summary>
    public static PixelRect Trimmed(PartView view, PixelRect keep)
    {
        var area = keep;
        foreach (var layer in view.Layers.All)
        {
            var image = layer.Image;
            for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                    if (image[x, y] != Palette.TransparentIndex && !keep.Contains(x, y))
                        area = Union(area, new PixelRect(x - Padding, y - Padding, x + Padding, y + Padding));
        }
        var current = Current(view);
        return new PixelRect(Math.Max(area.X0, current.X0), Math.Max(area.Y0, current.Y0),
            Math.Min(area.X1, current.X1), Math.Min(area.Y1, current.Y1));
    }

    /// <summary>
    /// Gives the view the area <paramref name="bounds"/> (in its current image pixels) as one undoable step.
    /// Nothing happens when it already has that area.
    /// </summary>
    public static void Resize(PartView view, PixelRect bounds, UndoHistory history)
    {
        if (bounds == Current(view))
            return;
        history.Do(new PartViewResize(view, bounds));
    }

    public static PixelRect Union(PixelRect a, PixelRect b) =>
        new(Math.Min(a.X0, b.X0), Math.Min(a.Y0, b.Y0), Math.Max(a.X1, b.X1), Math.Max(a.Y1, b.Y1));
}

/// <summary>
/// Undoable change of a view's drawing area. The resized layers are made once; redo puts them in and undo
/// puts the old layer objects back, so strokes recorded on either keep finding the image they changed.
/// </summary>
public sealed class PartViewResize : IUndoableAction
{
    private readonly PartView _view;
    private readonly IReadOnlyList<(PartLayer Layer, string Name, bool Visible)> _layersBefore, _layersAfter;
    private readonly Vector2 _positionBefore, _positionAfter;
    private readonly IReadOnlyDictionary<string, Vector2> _pointsBefore, _pointsAfter;

    public PartViewResize(PartView view, PixelRect bounds)
    {
        _view = view;
        var offset = new Vector2(bounds.X0, bounds.Y0);
        _layersBefore = view.Layers.Snapshot();
        _layersAfter = _layersBefore.Select(l => (new PartLayer(l.Name, Crop(l.Layer.Image, bounds), l.Visible), l.Name, l.Visible)).ToList();
        _positionBefore = view.RestPosition;
        _positionAfter = view.RestPosition + offset;
        _pointsBefore = view.Attachments.Snapshot();
        _pointsAfter = _pointsBefore.ToDictionary(p => p.Key, p => p.Value - offset);
    }

    public string Name => "그리기 영역";

    public void Redo() => Apply(_layersAfter, _positionAfter, _pointsAfter);

    public void Undo() => Apply(_layersBefore, _positionBefore, _pointsBefore);

    private void Apply(IReadOnlyList<(PartLayer, string, bool)> layers, Vector2 position, IReadOnlyDictionary<string, Vector2> points)
    {
        _view.Layers.Restore(layers);
        _view.SetRest(position, _view.RestPivot);
        _view.Attachments.Restore(points);
    }

    /// <summary>The part of <paramref name="image"/> inside <paramref name="bounds"/>; outside it is transparent.</summary>
    private static IndexedImage Crop(IndexedImage image, PixelRect bounds)
    {
        var result = new IndexedImage(bounds.Width, bounds.Height);
        for (int y = 0; y < result.Height; y++)
            for (int x = 0; x < result.Width; x++)
                if (image.InBounds(x + bounds.X0, y + bounds.Y0))
                    result.Set(x, y, image[x + bounds.X0, y + bounds.Y0]);
        return result;
    }
}
