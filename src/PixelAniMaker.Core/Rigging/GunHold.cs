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
/// hands meet the gun on each one. See <see cref="WeaponHold"/> for fitting whole clips.
/// </summary>
public static class GunHold
{
    /// <summary>Points on the gun view (base-image pixels, as attachment points).</summary>
    public const string Butt = "개머리판", Guard = "총열 덮개", Magazine = "탄창";

    /// <summary>Refits every weapon key (gun and sword) as one undo step; see <see cref="WeaponHold.Apply"/>.</summary>
    public static bool Apply(Character character, IEnumerable<AnimationClip> clips, UndoHistory history) =>
        WeaponHold.Apply(character, clips, history);

    public static void FitUnrecorded(Character character, IEnumerable<AnimationClip> clips) => WeaponHold.FitUnrecorded(character, clips);

    /// <summary>The pose with the arms and the gun's own turn refitted; the gun keeps pointing the same way.</summary>
    public static PoseData Fit(Character character, Direction direction, PoseData pose)
    {
        var gunPart = character.Find(GunPart.Name) ?? throw new InvalidOperationException("The character has no gun.");
        var view = gunPart.View(direction);
        var t = character.ComputeTransforms(direction, pose);
        float aim = t[gunPart].Angle;                                    // the gun picture's turn on the canvas
        Vector2 Offset(string point) => HeldItem.Offset(view, point) ?? Vector2.Zero;   // from the grip, at rest
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
            float reach = ArmIk.Length(character, direction, "r", view.RestPivot);
            grip = MathF.Abs(muzzle.X) > 0.5f
                ? shoulder + muzzle * reach * 0.88f                      // held out along the aim
                : (shoulder + t[character.Find("upper_arm_l")!].Pivot) / 2 + new Vector2(0, 0.35f * reach);   // in front of the chest
        }

        var rotations = pose.Rotations.ToDictionary(kv => kv.Key, kv => kv.Value);
        ArmIk.Reach(character, direction, t, "r", grip, view.RestPivot, rotations);
        t = character.ComputeTransforms(direction, pose with { Rotations = rotations });
        rotations[GunPart.Name] = ArmIk.Degrees(aim - t[character.Find("hand_r")!].Angle);

        // the support hand: onto the handguard, or the magazine when it is already reaching for that
        t = character.ComputeTransforms(direction, pose with { Rotations = rotations });
        var gunNow = t[gunPart];
        var handL = character.Find("hand_l")!;
        var palm = HeldItem.Palm(handL.View(direction));
        var palmNow = t[handL].Pivot + PartTransform.Rotate(palm - handL.View(direction).RestPivot, t[handL].Angle);
        var guard = gunNow.Pivot + Turned(Guard);
        var magazine = gunNow.Pivot + Turned(Magazine);
        float lengthL = ArmIk.Length(character, direction, "l", palm);
        var target = !pistol && Vector2.Distance(palmNow, magazine) < Vector2.Distance(palmNow, guard) * 0.6f ? magazine : guard;
        if (Vector2.Distance(palmNow, target) < 0.9f * lengthL)       // a hand away from the gun (pulling the magazine) stays
            ArmIk.Reach(character, direction, t, "l", target, palm, rotations);
        return pose with { Rotations = rotations };
    }
}

/// <summary>
/// Refits the keys of clips that turn a held weapon to this character's body: gun keys with <see cref="GunHold"/>,
/// two-handed sword keys with <see cref="SwordHold"/>. Other keys are left alone.
/// </summary>
public static class WeaponHold
{
    /// <summary>Refits every weapon key, in every direction, as one undo step; false when nothing changed.</summary>
    public static bool Apply(Character character, IEnumerable<AnimationClip> clips, UndoHistory history)
    {
        var changes = Plan(character, clips);
        if (changes.Count == 0)
            return false;
        history.Do(new FitChange(changes));
        return true;
    }

    /// <summary>Refits the clips' weapon keys without an undo step (for clips being set up, e.g. just imported).</summary>
    public static void FitUnrecorded(Character character, IEnumerable<AnimationClip> clips) => new FitChange(Plan(character, clips)).Redo();

    private static List<(AnimationClip Clip, Direction Direction, Keyframe Old, Keyframe New)> Plan(Character character, IEnumerable<AnimationClip> clips)
    {
        var result = new List<(AnimationClip, Direction, Keyframe, Keyframe)>();
        bool gun = character.Find(GunPart.Name) is not null, sword = SwordHold.NeedsFitting(character);
        if (!gun && !sword)
            return result;
        foreach (var clip in clips)
            foreach (var direction in character.StoredDirections)
                foreach (var key in clip.Keys(direction))
                {
                    var pose = key.Pose;
                    if (gun && pose.Rotations.ContainsKey(GunPart.Name))
                        pose = GunHold.Fit(character, direction, pose);
                    if (sword && pose.Rotations.ContainsKey(SwordPart.Name))
                        pose = SwordHold.Fit(character, direction, pose);
                    if (!pose.Rotations.SequenceEqual(key.Pose.Rotations))
                        result.Add((clip, direction, key, key with { Pose = pose }));
                }
        return result;
    }

    private sealed class FitChange(IReadOnlyList<(AnimationClip Clip, Direction Direction, Keyframe Old, Keyframe New)> changes) : IUndoableAction
    {
        public string Name => "무기 잡는 자세 맞추기";

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
