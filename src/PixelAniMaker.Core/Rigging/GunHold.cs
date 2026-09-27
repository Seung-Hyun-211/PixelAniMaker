using System.Numerics;
using PixelAniMaker.Core.Animation;
using PixelAniMaker.Core.History;

namespace PixelAniMaker.Core.Rigging;

/// <summary>
/// Fits the arms of gun poses to this character's body (two-bone inverse kinematics), keeping where the gun points:
/// a rifle's stock goes to the right shoulder (to the right hip when the muzzle is raised high, carried at port arms),
/// the right hand to the grip and the left hand to the handguard — or to the magazine when the pose already has it
/// near there (reloading). A pistol is held out along its aim with both hands on the grip, or in front of the chest
/// when it points up or down. Elbows bend downwards. Clips are shared between bodies of any size; this makes the
/// hands meet the gun on each one.
/// </summary>
public static class GunHold
{
    /// <summary>Points on the gun view (base-image pixels, as attachment points).</summary>
    public const string Butt = "개머리판", Guard = "총열 덮개", Magazine = "탄창";

    /// <summary>Refits every key that turns the gun, in every direction, as one undo step; false when nothing changed.</summary>
    public static bool Apply(Character character, IEnumerable<AnimationClip> clips, UndoHistory history)
    {
        var changes = Plan(character, clips);
        if (changes.Count == 0)
            return false;
        history.Do(new FitChange(changes));
        return true;
    }

    /// <summary>Refits the clips' gun keys without an undo step (for clips being set up, e.g. just imported).</summary>
    public static void FitUnrecorded(Character character, IEnumerable<AnimationClip> clips) => new FitChange(Plan(character, clips)).Redo();

    private static List<(AnimationClip Clip, Direction Direction, Keyframe Old, Keyframe New)> Plan(Character character, IEnumerable<AnimationClip> clips)
    {
        var result = new List<(AnimationClip, Direction, Keyframe, Keyframe)>();
        if (character.Find(GunPart.Name) is null)
            return result;
        foreach (var clip in clips)
            foreach (var direction in character.StoredDirections)
                foreach (var key in clip.Keys(direction).Where(k => k.Pose.Rotations.ContainsKey(GunPart.Name)))
                {
                    var pose = Fit(character, direction, key.Pose);
                    if (!pose.Rotations.SequenceEqual(key.Pose.Rotations))
                        result.Add((clip, direction, key, key with { Pose = pose }));
                }
        return result;
    }

    /// <summary>The pose with the arms and the gun's own turn refitted; the gun keeps pointing the same way.</summary>
    public static PoseData Fit(Character character, Direction direction, PoseData pose)
    {
        var gunPart = character.Find(GunPart.Name) ?? throw new InvalidOperationException("The character has no gun.");
        var view = gunPart.View(direction);
        var t = character.ComputeTransforms(direction, pose);
        float aim = t[gunPart].Angle;                                    // the gun picture's turn on the canvas
        Vector2 Offset(string point) =>                                   // from the grip, at rest
            view.Attachments.All.TryGetValue(point, out var p) ? view.RestPosition + p - view.RestPivot : Vector2.Zero;
        Vector2 Turned(string point) => PartTransform.Rotate(Offset(point), aim);
        var muzzle = PartTransform.Rotate(new Vector2(0, 1), aim);          // the muzzle points down at rest
        var shoulder = t[character.Find("upper_arm_r")!].Pivot;
        bool pistol = Offset(Butt).LengthSquared() < 0.01f;

        Vector2 grip;
        if (!pistol)
        {
            // port arms when the muzzle is up high: the stock at the right hip
            var anchor = muzzle.Y < -0.6f && character.Find("thigh_r") is { } thigh ? t[thigh].Pivot : shoulder;
            grip = anchor - Turned(Butt);
        }
        else
        {
            float reach = ArmLength(character, direction, "r", view.RestPivot);
            grip = MathF.Abs(muzzle.X) > 0.5f
                ? shoulder + muzzle * reach * 0.88f                      // held out along the aim
                : (shoulder + t[character.Find("upper_arm_l")!].Pivot) / 2 + new Vector2(0, 0.35f * reach);   // in front of the chest
        }

        var rotations = pose.Rotations.ToDictionary(kv => kv.Key, kv => kv.Value);
        Reach(character, direction, t, "r", grip, view.RestPivot, rotations);
        t = character.ComputeTransforms(direction, pose with { Rotations = rotations });
        rotations[GunPart.Name] = Degrees(aim - t[character.Find("hand_r")!].Angle);

        // the support hand: onto the handguard, or the magazine when it is already reaching for that
        t = character.ComputeTransforms(direction, pose with { Rotations = rotations });
        var gunNow = t[gunPart];
        var handL = character.Find("hand_l")!;
        var palm = Palm(handL.View(direction));
        var palmNow = t[handL].Pivot + PartTransform.Rotate(palm - handL.View(direction).RestPivot, t[handL].Angle);
        var guard = gunNow.Pivot + Turned(Guard);
        var magazine = gunNow.Pivot + Turned(Magazine);
        float lengthL = ArmLength(character, direction, "l", palm);
        var target = !pistol && Vector2.Distance(palmNow, magazine) < Vector2.Distance(palmNow, guard) * 0.6f ? magazine : guard;
        if (Vector2.Distance(palmNow, target) < 0.9f * lengthL)       // a hand away from the gun (pulling the magazine) stays
            Reach(character, direction, t, "l", target, palm, rotations);
        return pose with { Rotations = rotations };
    }

    /// <summary>The palm of a hand view at rest: across its middle, a little below its middle along it (canvas).</summary>
    private static Vector2 Palm(PartView hand)
    {
        var pixels = hand.DrawnPixels().ToList();
        return pixels.Count == 0 ? hand.RestPivot
            : new Vector2((float)pixels.Average(p => p.X + 0.5), pixels.Min(p => p.Y) + 0.55f * (pixels.Max(p => p.Y) - pixels.Min(p => p.Y) + 1));
    }

    private static float ArmLength(Character c, Direction d, string side, Vector2 handPoint)
    {
        var s = c.Find("upper_arm_" + side)!.View(d).RestPivot;
        var e = c.Find("forearm_" + side)!.View(d).RestPivot;
        return Vector2.Distance(s, e) + Vector2.Distance(e, handPoint);
    }

    /// <summary>
    /// Turns the upper arm and forearm of one side so that <paramref name="handPoint"/> (a point on the hand at rest,
    /// canvas) lands on <paramref name="target"/>, elbow down; the hand lines up with the forearm. Out of reach, the
    /// arm points straight at the target.
    /// </summary>
    private static void Reach(Character c, Direction d, IReadOnlyDictionary<Part, PartTransform> t, string side, Vector2 target,
        Vector2 handPoint, Dictionary<string, double> rotations)
    {
        var upper = c.Find("upper_arm_" + side)!;
        var fore = c.Find("forearm_" + side)!;
        var s0 = upper.View(d).RestPivot;
        var e0 = fore.View(d).RestPivot;
        Vector2 u0 = e0 - s0, f0 = handPoint - e0;
        float l1 = u0.Length(), l2 = f0.Length();
        var shoulder = t[upper].Pivot;
        float parent = upper.Parent is { } p ? t[p].Angle : 0;

        var toTarget = target - shoulder;
        float dist = Math.Clamp(toTarget.Length(), MathF.Abs(l1 - l2) + 0.01f, l1 + l2 - 0.01f);
        float toward = MathF.Atan2(toTarget.Y, toTarget.X);
        float bend = MathF.Acos(Math.Clamp((l1 * l1 + dist * dist - l2 * l2) / (2 * l1 * dist), -1, 1));
        // of the two elbows, the lower one
        var elbowA = shoulder + l1 * new Vector2(MathF.Cos(toward + bend), MathF.Sin(toward + bend));
        var elbowB = shoulder + l1 * new Vector2(MathF.Cos(toward - bend), MathF.Sin(toward - bend));
        var elbow = elbowA.Y >= elbowB.Y ? elbowA : elbowB;
        var hand = shoulder + Vector2.Normalize(toTarget) * dist;

        float upperAbs = Angle(elbow - shoulder) - Angle(u0);
        float foreAbs = Angle(hand - elbow) - Angle(f0);
        rotations[upper.Name] = Degrees(upperAbs - parent);
        rotations[fore.Name] = Degrees(foreAbs - upperAbs);
        rotations["hand_" + side] = 0;
    }

    private static float Angle(Vector2 v) => MathF.Atan2(v.Y, v.X);

    private static double Degrees(float radians) => Math.Round(Pose.Normalize(radians * 180 / Math.PI), 1);

    private sealed class FitChange(IReadOnlyList<(AnimationClip Clip, Direction Direction, Keyframe Old, Keyframe New)> changes) : IUndoableAction
    {
        public string Name => "총 잡는 자세 맞추기";

        public void Redo()
        {
            foreach (var (clip, direction, _, key) in changes)
                clip.SetKey(direction, key);
        }

        public void Undo()
        {
            foreach (var (clip, direction, key, _) in changes)
                clip.SetKey(direction, key);
        }
    }
}
