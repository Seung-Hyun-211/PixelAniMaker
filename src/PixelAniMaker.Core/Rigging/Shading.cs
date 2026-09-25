using PixelAniMaker.Core.Imaging;

namespace PixelAniMaker.Core.Rigging;

/// <summary>Where the light comes from (the shadow falls on the opposite edges).</summary>
public enum LightFrom
{
    TopLeft,
    TopRight,
}

/// <summary>
/// Automatic shading: composites get a darker band along each part's edges facing away from the light,
/// in the nearest darker colour the palette already has. Off by default.
/// </summary>
public sealed class ShadingSettings
{
    public const int MaxWidth = 3;

    private bool _enabled;
    private LightFrom _light = LightFrom.TopLeft;
    private int _width = 1;

    public event EventHandler? Changed;

    public bool Enabled
    {
        get => _enabled;
        set => Set(ref _enabled, value);
    }

    public LightFrom Light
    {
        get => _light;
        set => Set(ref _light, value);
    }

    /// <summary>Band width in pixels (1–<see cref="MaxWidth"/>), not counting the outline.</summary>
    public int Width
    {
        get => _width;
        set => Set(ref _width, Math.Clamp(value, 1, MaxWidth));
    }

    private void Set<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;
        field = value;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>Darkens the shadow-side edge band of every part (before the outline pass).</summary>
internal static class ShadingPass
{
    /// <param name="group">Shading group per owner: detail parts share their parent's, so no band is drawn where an eye meets the face.</param>
    /// <param name="skip">Palette indices left alone (the outline colours).</param>
    public static void Apply(CompositeResult c, int[] group, Palette palette, ShadingSettings settings, bool outline, IReadOnlySet<int> skip)
    {
        var darker = DarkerColours(palette, skip);
        int dx = settings.Light == LightFrom.TopLeft ? 1 : -1;   // towards the shadow side
        int reach = settings.Width + (outline ? 1 : 0);          // the outermost pixel becomes outline
        var result = (ushort[])c.Indices.Clone();
        for (int y = 0; y < c.Height; y++)
        {
            for (int x = 0; x < c.Width; x++)
            {
                int i = y * c.Width + x;
                short o = c.Owners[i];
                if (o == CompositeResult.NoPart || darker[c.Indices[i]] is not { } dark)
                    continue;
                for (int k = 1; k <= reach; k++)
                {
                    if (OtherGroup(x + dx * k, y) || OtherGroup(x, y + k))
                    {
                        result[i] = (ushort)dark;
                        break;
                    }
                }

                bool OtherGroup(int nx, int ny)
                {
                    short n = c.OwnerAt(nx, ny);
                    return n == CompositeResult.NoPart || group[n] != group[o];
                }
            }
        }
        result.CopyTo(c.Indices, 0);
    }

    /// <summary>
    /// For each palette index, the palette colour closest to it at three quarters brightness, if one is
    /// clearly darker and close enough to read as a shade of it; null otherwise.
    /// </summary>
    internal static int?[] DarkerColours(Palette palette, IReadOnlySet<int> skip)
    {
        var colours = palette.Colors;
        var result = new int?[colours.Count];
        for (int i = 1; i < colours.Count; i++)
        {
            if (skip.Contains(i))
                continue;
            var c = colours[i];
            double target = Luma(c) * 0.75;
            int? best = null;
            double bestDistance = double.MaxValue;
            for (int j = 1; j < colours.Count; j++)
            {
                var d = colours[j];
                if (j == i || skip.Contains(j) || d.A == 0 || Luma(d) > Luma(c) - 8)
                    continue;
                double distance = Sq(d.R - c.R * 0.75) + Sq(d.G - c.G * 0.75) + Sq(d.B - c.B * 0.75);
                if (distance < bestDistance)
                    (best, bestDistance) = (j, distance);
            }
            if (best is not null && Math.Sqrt(bestDistance) <= 60 && Luma(colours[best.Value]) >= target * 0.6)
                result[i] = best;
        }
        return result;

        static double Sq(double v) => v * v;
    }

    private static double Luma(Rgba c) => 0.299 * c.R + 0.587 * c.G + 0.114 * c.B;
}
