namespace PixelAniMaker.Core.Imaging;

/// <summary>
/// Indexed colour palette. Index 0 is always transparent; there is no limit on the number of colours.
/// </summary>
public sealed class Palette
{
    public const int TransparentIndex = 0;

    private readonly List<Rgba> _colors = [Rgba.Transparent];

    public event EventHandler? Changed;

    public int Count => _colors.Count;

    public Rgba this[int index] => _colors[index];

    public IReadOnlyList<Rgba> Colors => _colors;

    /// <summary>Returns the index of <paramref name="color"/>, adding it when missing. Fully transparent maps to 0.</summary>
    public int GetOrAdd(Rgba color)
    {
        if (color.IsTransparent)
            return TransparentIndex;
        int i = IndexOf(color);
        if (i >= 0)
            return i;
        if (_colors.Count > ushort.MaxValue)
            throw new InvalidOperationException("Palette is full.");
        _colors.Add(color);
        Changed?.Invoke(this, EventArgs.Empty);
        return _colors.Count - 1;
    }

    public int IndexOf(Rgba color)
    {
        if (color.IsTransparent)
            return TransparentIndex;
        for (int i = 1; i < _colors.Count; i++)
            if (_colors[i] == color)
                return i;
        return -1;
    }

    /// <summary>Replaces a colour in place; every pixel using that index changes with it.</summary>
    public void Set(int index, Rgba color)
    {
        if (index == TransparentIndex)
            throw new ArgumentOutOfRangeException(nameof(index), "The transparent entry cannot be changed.");
        _colors[index] = color;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Replaces every colour after the transparent entry. The list must not be shorter than the
    /// highest index in use — callers only shrink it to undo an earlier append.
    /// </summary>
    public void ReplaceAll(IReadOnlyList<Rgba> colors)
    {
        _colors.RemoveRange(1, _colors.Count - 1);
        _colors.AddRange(colors);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public Palette Clone()
    {
        var p = new Palette();
        p._colors.Clear();
        p._colors.AddRange(_colors);
        return p;
    }
}
