using PixelAniMaker.Core.History;
using PixelAniMaker.Core.Imaging;

namespace PixelAniMaker.Core.Editing;

/// <summary>A set of pixel changes (one stroke or one fill) that can be undone as a unit.</summary>
public sealed class PixelEdit : IUndoableAction
{
    private readonly IndexedImage _image;
    private readonly Action _onApplied;
    // key = y * width + x ; value = (before, after)
    private readonly Dictionary<int, (ushort Before, ushort After)> _changes = [];

    public PixelEdit(string name, IndexedImage image, Action onApplied)
    {
        Name = name;
        _image = image;
        _onApplied = onApplied;
    }

    public string Name { get; }

    public bool IsEmpty => _changes.Count == 0;

    /// <summary>Applies a pixel change immediately and records it. Returns true when the pixel changed.</summary>
    public bool Set(int x, int y, int index)
    {
        if (!_image.InBounds(x, y))
            return false;
        int before = _image[x, y];
        if (!_image.Set(x, y, index))
            return false;
        int key = y * _image.Width + x;
        _changes[key] = _changes.TryGetValue(key, out var c)
            ? (c.Before, (ushort)index)
            : ((ushort)before, (ushort)index);
        return true;
    }

    public void Undo() => Apply(before: true);

    public void Redo() => Apply(before: false);

    private void Apply(bool before)
    {
        foreach (var (key, (b, a)) in _changes)
            _image.Set(key % _image.Width, key / _image.Width, before ? b : a);
        _onApplied();
    }
}
