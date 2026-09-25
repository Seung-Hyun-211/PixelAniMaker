using System.Numerics;
using PixelAniMaker.Core.History;
using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.Core.Animation;

/// <summary>How a part follows the body late.</summary>
public enum SecondaryMode
{
    /// <summary>Soft volumes (bust): the image's top row stays, lower rows follow the lag more.</summary>
    Deform,

    /// <summary>Hanging parts (hair, tails): an extra rotation around the joint, towards where the part lags.</summary>
    Swing,
}

/// <summary>
/// Secondary motion of one part: a damped spring follows the part (its joint for <see cref="SecondaryMode.Deform"/>,
/// the middle of its image for <see cref="SecondaryMode.Swing"/>); the lag becomes a warp or an extra angle.
/// </summary>
/// <param name="Period">Frames for one bounce (smaller = firmer).</param>
/// <param name="Damping">0 = keeps bouncing, 1 = settles at once.</param>
/// <param name="Strength">Multiplier on the lag.</param>
/// <param name="Max">Deform: vertical limit in px (horizontal is half); swing: limit in degrees.</param>
public sealed record SecondarySettings(SecondaryMode Mode, float Period, float Damping, float Strength, float Max)
{
    public static SecondarySettings Bust { get; } = new(SecondaryMode.Deform, 4, 0.4f, 1, 2);
    public static SecondarySettings Hair { get; } = new(SecondaryMode.Swing, 3, 0.3f, 1, 20);
    public static SecondarySettings Cloth { get; } = new(SecondaryMode.Swing, 5, 0.25f, 1.2f, 35);

    /// <summary>The same settings kept inside their ranges.</summary>
    public SecondarySettings Clamped() => this with
    {
        Period = Math.Clamp(Period, 2, 12),
        Damping = Math.Clamp(Damping, 0.1f, 1),
        Strength = Math.Clamp(Strength, 0, 2),
        Max = Math.Clamp(Max, 0, Mode == SecondaryMode.Deform ? 6 : 45),
    };
}

/// <summary>What secondary motion adds to one frame: warp offsets (px) and extra angles (degrees) per part.</summary>
public sealed record SecondaryFrame(IReadOnlyDictionary<Part, Vector2> Offsets, IReadOnlyDictionary<Part, float> Angles)
{
    public static SecondaryFrame None { get; } = new(new Dictionary<Part, Vector2>(), new Dictionary<Part, float>());

    public bool IsEmpty => Offsets.Count == 0 && Angles.Count == 0;

    /// <summary><paramref name="pose"/> with this frame's extra angles added.</summary>
    public PoseData Apply(PoseData pose)
    {
        if (Angles.Count == 0)
            return pose;
        var rotations = new Dictionary<string, double>(pose.Rotations);
        foreach (var (part, degrees) in Angles)
            rotations[part.Name] = pose.Get(part.Name) + degrees;
        return pose with { Rotations = rotations };
    }
}

/// <summary>
/// Computes secondary motion for a clip in one direction. Nothing is stored in the clip: frames get the
/// result when they are made. Looping clips run three warm-up loops so the last frame joins the first
/// without a jump; frame holds count as longer time. Deterministic, results in whole pixels / degrees.
/// </summary>
public static class SecondaryMotion
{
    private const int StepsPerTick = 16;
    private const int WarmUpLoops = 3;

    public static bool HasAny(Character character) => character.Parts.Any(p => p.Secondary is not null);

    public static IReadOnlyList<SecondaryFrame> Solve(Character character, AnimationClip clip, Direction direction)
    {
        int n = clip.FrameCount;
        var parts = character.Hierarchy().Where(p => p.Secondary is not null).ToList();
        if (parts.Count == 0)
            return Enumerable.Repeat(SecondaryFrame.None, n).ToList();

        var offsets = Enumerable.Range(0, n).Select(_ => new Dictionary<Part, Vector2>()).ToList();
        var angles = Enumerable.Range(0, n).Select(_ => new Dictionary<Part, float>()).ToList();
        foreach (var part in parts)                                  // parents first: children follow their swing
        {
            var settings = part.Secondary!.Clamped();
            var poses = Enumerable.Range(0, n).Select(f => new SecondaryFrame(offsets[f], angles[f])
                .Apply(clip.Evaluate(direction, f))).ToList();
            var transforms = poses.Select(p => character.ComputeTransforms(direction, p)[part]).ToList();
            var targets = transforms.Select(t => settings.Mode == SecondaryMode.Deform ? t.Pivot : Middle(t)).ToList();
            var lag = Spring(clip, targets, settings);
            for (int f = 0; f < n; f++)
            {
                if (settings.Mode == SecondaryMode.Deform)
                {
                    var off = lag[f] * settings.Strength;
                    float maxX = MathF.Floor(settings.Max / 2);
                    var px = new Vector2(Math.Clamp(MathF.Round(off.X), -maxX, maxX), Math.Clamp(MathF.Round(off.Y), -settings.Max, settings.Max));
                    if (px != Vector2.Zero)
                        offsets[f][part] = px;
                }
                else
                {
                    var joint = transforms[f].Pivot;
                    Vector2 from = targets[f] - joint, to = targets[f] + lag[f] - joint;
                    if (from.LengthSquared() < 1e-6f)
                        continue;
                    float degrees = MathF.Atan2(from.X * to.Y - from.Y * to.X, Vector2.Dot(from, to)) * 180 / MathF.PI;
                    degrees = MathF.Round(Math.Clamp(degrees * settings.Strength, -settings.Max, settings.Max));
                    if (degrees != 0)
                        angles[f][part] = degrees;
                }
            }
        }
        return Enumerable.Range(0, n).Select(f => new SecondaryFrame(offsets[f], angles[f])).ToList();
    }

    /// <summary>Lag (spring position − target) at the start of every frame.</summary>
    private static Vector2[] Spring(AnimationClip clip, IReadOnlyList<Vector2> targets, SecondarySettings s)
    {
        int n = targets.Count;
        double omega = 2 * Math.PI / (s.Period / clip.Fps);
        var x = targets[0];
        var v = Vector2.Zero;
        var lag = new Vector2[n];
        int loops = clip.Loop ? WarmUpLoops + 1 : 1;
        for (int loop = 0; loop < loops; loop++)
        {
            for (int f = 0; f < n; f++)
            {
                lag[f] = x - targets[f];
                var next = clip.Loop ? targets[(f + 1) % n] : targets[Math.Min(f + 1, n - 1)];
                int steps = StepsPerTick * clip.Hold(f);
                double h = clip.Hold(f) / (double)clip.Fps / steps;
                for (int i = 0; i < steps; i++)
                {
                    var target = Vector2.Lerp(targets[f], next, (i + 1f) / steps);
                    var acc = (float)(omega * omega) * (target - x) - (float)(2 * s.Damping * omega) * v;
                    v += acc * (float)h;
                    x += v * (float)h;
                }
            }
        }
        return lag;
    }

    /// <summary>Canvas position of the middle of the drawn image (what a hanging part swings by).</summary>
    private static Vector2 Middle(PartTransform t) => t.ToCanvas(new Vector2(t.Image.Width / 2f, t.Image.Height / 2f));
}

/// <summary>Undoable set or removal (null) of a part's secondary motion.</summary>
public sealed class SecondaryChange(Part part, SecondarySettings? before, SecondarySettings? after) : IUndoableAction
{
    public string Name => "흔들림 (2차 모션)";

    public void Undo() => part.Secondary = before;

    public void Redo() => part.Secondary = after;

    public static void Apply(Part part, UndoHistory history, SecondarySettings? settings)
    {
        if (part.Secondary == settings)
            return;
        var change = new SecondaryChange(part, part.Secondary, settings);
        change.Redo();
        history.Push(change);
    }
}
