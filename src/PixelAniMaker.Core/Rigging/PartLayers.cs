using PixelAniMaker.Core.History;
using PixelAniMaker.Core.Imaging;

namespace PixelAniMaker.Core.Rigging;

/// <summary>One drawing layer of a part view (skin, clothes, hair …). All layers of a view have the same size.</summary>
public sealed class PartLayer(string name, IndexedImage image, bool visible = true)
{
    public string Name { get; set; } = name;
    public IndexedImage Image { get; } = image;
    public bool Visible { get; set; } = visible;
}

/// <summary>
/// The layers of a part view, bottom first. <see cref="Flattened"/> is what gets drawn: with a single
/// visible layer it is that layer's image itself; otherwise one image kept up to date from the visible
/// layers (upper layers cover lower ones where they are not transparent).
/// </summary>
public sealed class PartLayers
{
    public const string BaseName = "기본";

    private readonly List<PartLayer> _layers;
    private IndexedImage _flat;
    private (IndexedImage, int, bool)[] _flatSource = [];

    public PartLayers(IndexedImage baseImage)
    {
        _layers = [new PartLayer(BaseName, baseImage)];
        _flat = new IndexedImage(baseImage.Width, baseImage.Height);
    }

    public IReadOnlyList<PartLayer> All => _layers;

    public int Count => _layers.Count;

    public PartLayer this[int index] => _layers[index];

    public IndexedImage Flattened
    {
        get
        {
            if (_layers is [{ Visible: true } only])
                return only.Image;
            var source = _layers.Select(l => (l.Image, l.Image.Version, l.Visible)).ToArray();
            if (_flat.Width != _layers[0].Image.Width || _flat.Height != _layers[0].Image.Height)
                _flat = new IndexedImage(_layers[0].Image.Width, _layers[0].Image.Height);   // the layers were resized
            else if (source.SequenceEqual(_flatSource))
                return _flat;
            Flatten();
            _flatSource = source;
            return _flat;
        }
    }

    /// <summary>A blank layer the size of the others.</summary>
    public PartLayer CreateLayer(string name) => new(name, new IndexedImage(_layers[0].Image.Width, _layers[0].Image.Height));

    /// <summary>"name", "name 2", "name 3" … not used by another layer.</summary>
    public string FreeName(string stem)
    {
        if (_layers.All(l => l.Name != stem))
            return stem;
        int i = 2;
        while (_layers.Any(l => l.Name == $"{stem} {i}"))
            i++;
        return $"{stem} {i}";
    }

    internal IReadOnlyList<(PartLayer Layer, string Name, bool Visible)> Snapshot() =>
        _layers.Select(l => (l, l.Name, l.Visible)).ToList();

    internal void Restore(IReadOnlyList<(PartLayer Layer, string Name, bool Visible)> state)
    {
        _layers.Clear();
        foreach (var (layer, name, visible) in state)
        {
            layer.Name = name;
            layer.Visible = visible;
            _layers.Add(layer);
        }
    }

    /// <summary>Direct list access for <see cref="LayerChange"/> edits and loading.</summary>
    internal List<PartLayer> Items => _layers;

    private void Flatten()
    {
        var target = _flat.Pixels;
        var pixels = new ushort[target.Length];
        foreach (var layer in _layers.Where(l => l.Visible))
        {
            var src = layer.Image.Pixels;
            for (int i = 0; i < pixels.Length; i++)
                if (src[i] != Palette.TransparentIndex)
                    pixels[i] = src[i];
        }
        for (int i = 0; i < pixels.Length; i++)
            _flat.Set(i % _flat.Width, i / _flat.Width, pixels[i]);
    }
}

/// <summary>Undoable edit of a view's layer list: add, remove, reorder, rename, show/hide.</summary>
public sealed class LayerChange(PartLayers layers, string name,
    IReadOnlyList<(PartLayer, string, bool)> before, IReadOnlyList<(PartLayer, string, bool)> after) : IUndoableAction
{
    public string Name => name;

    public void Undo() => layers.Restore(before);

    public void Redo() => layers.Restore(after);

    /// <param name="edit">Changes the list (and layer names/visibility) in place.</param>
    public static void Apply(PartLayers layers, UndoHistory history, string name, Action<List<PartLayer>> edit)
    {
        var before = layers.Snapshot();
        edit(layers.Items);
        if (layers.Items.Count == 0)
        {
            layers.Restore(before);                        // a view always keeps one layer
            return;
        }
        var after = layers.Snapshot();
        if (before.SequenceEqual(after))
            return;
        history.Push(new LayerChange(layers, name, before, after));
    }
}
