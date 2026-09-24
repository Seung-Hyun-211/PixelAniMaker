using PixelAniMaker.Core.History;
using PixelAniMaker.Core.Imaging;

namespace PixelAniMaker.Core.Editing;

/// <summary>
/// Symmetric editing: every pixel set is also set at <see cref="Map"/>(x, y) in <see cref="Target"/>
/// (the same image for centre parts, the counterpart part's image for left/right parts).
/// </summary>
public sealed record PixelMirror(IndexedImage Target, Func<int, int, (int X, int Y)> Map);

/// <summary>A set of pixel changes (one stroke or one fill) that can be undone as a unit.</summary>
public sealed class PixelEdit : IUndoableAction
{
    private readonly IndexedImage _image;
    private readonly Action _onApplied;
    // key = y * width + x ; value = (before, after)
    private readonly Dictionary<int, (ushort Before, ushort After)> _changes = [];
    private readonly Func<int, int, (int X, int Y)>? _map;
    // mirrored changes on another image; null when mirroring into this image (or not mirroring)
    private readonly PixelEdit? _twin;

    public PixelEdit(string name, IndexedImage image, Action onApplied, PixelMirror? mirror = null)
    {
        Name = name;
        _image = image;
        _onApplied = onApplied;
        _map = mirror?.Map;
        if (mirror is not null && mirror.Target != image)
            _twin = new PixelEdit(name, mirror.Target, () => { });
    }

    public string Name { get; }

    public bool IsEmpty => _changes.Count == 0 && _twin?.IsEmpty != false;

    /// <summary>
    /// Applies a pixel change immediately (and its mirror, when mirroring) and records it. Returns true
    /// when a pixel changed.
    /// </summary>
    public bool Set(int x, int y, int index)
    {
        bool changed = SetOwn(x, y, index);
        if (_map is not null)
        {
            var (mx, my) = _map(x, y);
            changed |= (_twin ?? this).SetOwn(mx, my, index);
        }
        return changed;
    }

    private bool SetOwn(int x, int y, int index)
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

    /// <summary>The value a pixel had before this edit touched it (its current value if untouched).</summary>
    public int Original(int x, int y) =>
        _changes.TryGetValue(y * _image.Width + x, out var c) ? c.Before : _image[x, y];

    /// <summary>Puts every touched pixel back and forgets the changes (for shape previews).</summary>
    public void RevertAll()
    {
        _twin?.RevertAll();
        foreach (var (key, (before, _)) in _changes)
            _image.Set(key % _image.Width, key / _image.Width, before);
        _changes.Clear();
    }

    public void Undo() => Apply(before: true);

    public void Redo() => Apply(before: false);

    private void Apply(bool before)
    {
        _twin?.Apply(before);
        foreach (var (key, (b, a)) in _changes)
            _image.Set(key % _image.Width, key / _image.Width, before ? b : a);
        _onApplied();
    }
}
