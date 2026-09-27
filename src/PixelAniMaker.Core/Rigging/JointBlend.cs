using System.Numerics;

namespace PixelAniMaker.Core.Rigging;

/// <summary>
/// Smooth joints: near its joint a part bends with the part it hangs from instead of turning as a block, so a bent
/// elbow or knee neither opens a gap on the outside nor shows a seam line; gaps that are left are filled from the
/// nearest part. Positions are blended, never colours (every pixel stays a palette colour). Off by default.
/// </summary>
public sealed class JointBlendSettings
{
    public const int MinRadius = 1, MaxRadius = 24;

    private bool _enabled;
    private int _radius = 6;

    public event EventHandler? Changed;

    public bool Enabled
    {
        get => _enabled;
        set => Set(ref _enabled, value);
    }

    /// <summary>How far from the joint (pixels) the bend spreads.</summary>
    public int Radius
    {
        get => _radius;
        set => Set(ref _radius, Math.Clamp(value, MinRadius, MaxRadius));
    }

    private void Set<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;
        field = value;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>The bend of one part at its joint, for drawing (see <see cref="JointBlendSettings"/>).</summary>
/// <param name="Bend">The part's own rotation relative to its parent (radians).</param>
/// <param name="Shared">
/// The parent bends too (it has no other child): each side then takes half, so both meet at the middle angle at
/// the joint. Otherwise the child alone bends, from the parent's angle at the joint.
/// </param>
internal readonly record struct JointBend(short Child, short Parent, Vector2 Joint, float Bend, float Radius, bool Shared)
{
    /// <summary>Weight of the bend at distance r from the joint: 1 at the joint, easing to 0 at the radius.</summary>
    public float Falloff(float r)
    {
        float w = Math.Clamp(r / Radius, 0, 1);
        return 1 - w * w * (3 - 2 * w);
    }

    /// <summary>Rotation the child takes off at distance r from the joint (all of its share at the joint, none from the radius on).</summary>
    public float Remaining(float r) => Falloff(r) * Bend * (Shared ? 0.5f : 1);

    /// <summary>Rotation the parent adds around this joint at distance r (towards the child), when shared.</summary>
    public float ParentTurn(float r) => Shared ? Falloff(r) * Bend * 0.5f : 0;

    public bool Near(int x, int y) => Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), Joint) <= Radius + 1;
}

internal static class JointBlendPass
{
    /// <summary>
    /// Fills empty pixels near blended joints that the two parts close in on (left and right, or above and below,
    /// are both drawn by them), with the colour of the part most of the neighbours belong to.
    /// </summary>
    public static void FillGaps(CompositeResult c, IReadOnlyList<JointBend> joints)
    {
        for (int pass = 0; pass < 2; pass++)
        {
            var fills = new List<(int I, ushort Index, short Owner)>();
            foreach (var j in joints)
            {
                int r = (int)MathF.Ceiling(j.Radius) + 1;
                for (int y = (int)j.Joint.Y - r; y <= (int)j.Joint.Y + r; y++)
                    for (int x = (int)j.Joint.X - r; x <= (int)j.Joint.X + r; x++)
                    {
                        if ((uint)x >= (uint)c.Width || (uint)y >= (uint)c.Height || c.Owners[y * c.Width + x] != CompositeResult.NoPart || !j.Near(x, y))
                            continue;
                        bool Ours(int nx, int ny) => c.OwnerAt(nx, ny) is var o && (o == j.Child || o == j.Parent);
                        if (!(Ours(x - 1, y) && Ours(x + 1, y)) && !(Ours(x, y - 1) && Ours(x, y + 1)))
                            continue;
                        int child = 0, parent = 0;
                        foreach (var (dx, dy) in Around)
                        {
                            short o = c.OwnerAt(x + dx, y + dy);
                            if (o == j.Child) child++;
                            else if (o == j.Parent) parent++;
                        }
                        short owner = child >= parent ? j.Child : j.Parent;
                        foreach (var (dx, dy) in Around)
                            if (c.OwnerAt(x + dx, y + dy) == owner)
                            {
                                fills.Add((y * c.Width + x, c.Indices[(y + dy) * c.Width + x + dx], owner));
                                break;
                            }
                    }
            }
            if (fills.Count == 0)
                return;
            foreach (var (i, index, owner) in fills)
            {
                c.Indices[i] = index;
                c.Owners[i] = owner;
            }
        }
    }

    /// <summary>True where a and b meet at a blended joint (no seam line is drawn there).</summary>
    public static bool Joined(IReadOnlyList<JointBend> joints, short a, short b, int x, int y)
    {
        foreach (var j in joints)
            if ((a == j.Child && b == j.Parent || a == j.Parent && b == j.Child) && j.Near(x, y))
                return true;
        return false;
    }

    private static readonly (int, int)[] Around = [(-1, 0), (1, 0), (0, -1), (0, 1), (-1, -1), (1, -1), (-1, 1), (1, 1)];
}
