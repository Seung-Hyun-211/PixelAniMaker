using PixelAniMaker.Core.Imaging;

namespace PixelAniMaker.Core.Editing;

/// <summary>Primary (left button) and secondary (right button) palette indices.</summary>
public sealed class ColorSelection
{
    private readonly Palette _palette;
    private int _primary;
    private int _secondary = Palette.TransparentIndex;

    public ColorSelection(Palette palette)
    {
        _palette = palette;
        _primary = palette.Count > 1 ? 1 : Palette.TransparentIndex;
        palette.Changed += (_, _) => KeepInRange();
    }

    public event EventHandler? Changed;

    public int Primary
    {
        get => _primary;
        set => Set(ref _primary, value);
    }

    public int Secondary
    {
        get => _secondary;
        set => Set(ref _secondary, value);
    }

    public int Get(bool secondary) => secondary ? _secondary : _primary;

    public void Set(bool secondary, int index)
    {
        if (secondary)
            Secondary = index;
        else
            Primary = index;
    }

    /// <summary>The palette can shrink (undoing an import); fall back to the last colour.</summary>
    private void KeepInRange()
    {
        int last = _palette.Count - 1;
        if (_primary > last)
            Primary = last;
        if (_secondary > last)
            Secondary = last;
    }

    private void Set(ref int field, int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(value, _palette.Count);
        if (field == value)
            return;
        field = value;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
