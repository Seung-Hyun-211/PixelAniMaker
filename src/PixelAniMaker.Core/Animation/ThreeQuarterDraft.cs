using PixelAniMaker.Core.History;
using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.Core.Animation;

/// <summary>
/// Drafts empty 3/4 tracks from the classic ones. A front-3/4 key sits halfway between the front and
/// the side key, a back-3/4 key halfway between the back and the side key — except the arms, which
/// come from the side track: a 3/4 view has the same near and far arm as the side view, while the
/// front view often swings the other arm (blending those would half-raise both). Keys are made at
/// every frame either source has a key; tracks that already have keys are left alone.
/// </summary>
public static class ThreeQuarterDraft
{
    private static readonly (Direction Target, Direction From)[] Sources =
        [(Direction.FrontLeft, Direction.Front), (Direction.BackLeft, Direction.Back)];

    public static bool IsArm(string part) =>
        part.StartsWith("upper_arm", StringComparison.Ordinal) || part.StartsWith("forearm", StringComparison.Ordinal)
        || part.StartsWith("hand", StringComparison.Ordinal);

    /// <summary>The keys a draft would add to <paramref name="clip"/> (none for tracks that already have keys).</summary>
    public static IReadOnlyList<(Direction Direction, Keyframe Key)> Make(AnimationClip clip)
    {
        var result = new List<(Direction, Keyframe)>();
        foreach (var (target, from) in Sources)
        {
            if (clip.Keys(target).Count > 0)
                continue;
            var fromKeys = clip.Keys(from);
            var sideKeys = clip.Keys(Direction.Left);
            foreach (int frame in fromKeys.Concat(sideKeys).Select(k => k.Frame).Distinct().Order())
            {
                var main = clip.Evaluate(from, frame);
                var side = clip.Evaluate(Direction.Left, frame);
                var blend = AnimationClip.Blend(main, side, 0.5);
                var rotations = blend.Rotations
                    .Where(r => !IsArm(r.Key))
                    .Concat(side.Rotations.Where(r => IsArm(r.Key)))
                    .ToDictionary(r => r.Key, r => r.Value);
                var easing = fromKeys.FirstOrDefault(k => k.Frame == frame)?.Easing
                             ?? sideKeys.FirstOrDefault(k => k.Frame == frame)?.Easing ?? Easing.EaseInOut;
                result.Add((target, new Keyframe(frame, new PoseData(rotations, blend.Offset), easing)));
            }
        }
        return result;
    }

    /// <summary>Drafts every clip's empty 3/4 tracks as one undo step; false when there was nothing to draft.</summary>
    public static bool Apply(IEnumerable<AnimationClip> clips, UndoHistory history)
    {
        var added = clips.SelectMany(c => Make(c).Select(k => (Clip: c, k.Direction, k.Key))).ToList();
        if (added.Count == 0)
            return false;
        var change = new DraftChange(added);
        change.Redo();
        history.Push(change);
        return true;
    }

    private sealed class DraftChange(IReadOnlyList<(AnimationClip Clip, Direction Direction, Keyframe Key)> keys) : IUndoableAction
    {
        public string Name => "반측면 동작 초안";

        public void Redo()
        {
            foreach (var (clip, direction, key) in keys)
                clip.SetKey(direction, key);
        }

        public void Undo()
        {
            foreach (var (clip, direction, key) in keys)
                clip.RemoveKey(direction, key.Frame);
        }
    }
}
