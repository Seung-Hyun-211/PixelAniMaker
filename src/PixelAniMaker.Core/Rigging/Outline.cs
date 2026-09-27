using PixelAniMaker.Core.Imaging;

namespace PixelAniMaker.Core.Rigging;

/// <summary>
/// Automatic outline settings. When enabled, composites get a clean 1px outline after rotation;
/// when disabled, the parts' own pixels are used as drawn (e.g. for engines that add outlines in a shader).
/// Off by default: it needs outline colours, which the character template sets.
/// </summary>
public sealed class OutlineSettings
{
    private bool _enabled;
    private int _outlineIndex = Palette.TransparentIndex;
    private int _innerIndex = Palette.TransparentIndex;

    public event EventHandler? Changed;

    public bool Enabled
    {
        get => _enabled;
        set => Set(ref _enabled, value);
    }

    /// <summary>Palette index for the silhouette edge.</summary>
    public int OutlineIndex
    {
        get => _outlineIndex;
        set => Set(ref _outlineIndex, value);
    }

    /// <summary>Palette index for edges where a part overlaps the part behind it.</summary>
    public int InnerIndex
    {
        get => _innerIndex;
        set => Set(ref _innerIndex, value);
    }

    private void Set<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;
        field = value;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>Recolours the edge pixels of a composite: silhouette → outline, overlaps → inner line.</summary>
internal static class OutlinePass
{
    /// <param name="rank">Draw rank per owner (index into Character.Parts); higher is in front.</param>
    /// <param name="joined">Owners (a, b) meeting at pixel (x, y) where no inner line is drawn (smooth joints).</param>
    public static void Apply(CompositeResult c, int[] rank, OutlineSettings settings, Func<short, short, int, int, bool>? joined = null)
    {
        var owners = c.Owners;
        var result = (ushort[])c.Indices.Clone();
        for (int y = 0; y < c.Height; y++)
        {
            for (int x = 0; x < c.Width; x++)
            {
                short o = owners[y * c.Width + x];
                if (o == CompositeResult.NoPart)
                    continue;
                bool silhouette = false, overlap = false;
                foreach (var (nx, ny) in Neighbours(x, y))
                {
                    short n = c.OwnerAt(nx, ny);
                    if (n == CompositeResult.NoPart)
                        silhouette = true;
                    else if (rank[n] < rank[o] && !(joined?.Invoke(o, n, x, y) ?? false))
                        overlap = true;
                }
                if (silhouette)
                    result[y * c.Width + x] = (ushort)settings.OutlineIndex;
                else if (overlap)
                    result[y * c.Width + x] = (ushort)settings.InnerIndex;
            }
        }
        result.CopyTo(c.Indices, 0);
    }

    private static (int, int)[] Neighbours(int x, int y) => [(x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)];
}
