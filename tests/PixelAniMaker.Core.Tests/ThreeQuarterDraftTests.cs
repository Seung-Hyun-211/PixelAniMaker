using System.Numerics;
using PixelAniMaker.Core.Animation;
using PixelAniMaker.Core.History;
using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.Core.Tests;

public class ThreeQuarterDraftTests
{
    private static PoseData Pose(Vector2 offset, params (string Part, double Degrees)[] r) =>
        new(r.ToDictionary(x => x.Part, x => x.Degrees), offset);

    /// <summary>Front waves the right arm, the side waves the near (left) arm; legs swing in both.</summary>
    private static AnimationClip Wave()
    {
        var clip = new AnimationClip("wave", 4);
        clip.SetKey(Direction.Front, new Keyframe(0, Pose(Vector2.Zero, ("upper_arm_r", 140), ("thigh_r", 4), ("head", -4))));
        clip.SetKey(Direction.Front, new Keyframe(2, Pose(new Vector2(0, 2), ("upper_arm_r", 120), ("thigh_r", -4)), Easing.Linear));
        clip.SetKey(Direction.Left, new Keyframe(0, Pose(Vector2.Zero, ("upper_arm_l", 115), ("thigh_r", 20))));
        clip.SetKey(Direction.Back, new Keyframe(1, Pose(Vector2.Zero, ("upper_arm_r", -140))));
        return clip;
    }

    [Fact]
    public void Arms_come_from_the_side_and_everything_else_is_halfway()
    {
        var clip = Wave();
        var keys = ThreeQuarterDraft.Make(clip);
        var front = keys.Where(k => k.Direction == Direction.FrontLeft).Select(k => k.Key).ToList();
        Assert.Equal([0, 2], front.Select(k => k.Frame));

        var first = front[0].Pose;
        Assert.Equal(115, first.Get("upper_arm_l"));      // side arm
        Assert.Equal(0, first.Get("upper_arm_r"));        // the front's waving arm is not blended in
        Assert.Equal(12, first.Get("thigh_r"), 6);        // (4 + 20) / 2
        Assert.Equal(-2, first.Get("head"), 6);           // (-4 + 0) / 2
        Assert.Equal(new Vector2(0, 1), front[1].Pose.Offset);   // (2 + 0) / 2
        Assert.Equal(Easing.Linear, front[1].Easing);     // taken from the front key at that frame

        var back = keys.Where(k => k.Direction == Direction.BackLeft).Select(k => k.Key).ToList();
        Assert.Equal([0, 1], back.Select(k => k.Frame));
        Assert.Equal(115, back[0].Pose.Get("upper_arm_l"));
    }

    [Fact]
    public void Tracks_with_keys_are_left_alone_and_the_draft_is_one_undo_step()
    {
        var clip = Wave();
        clip.SetKey(Direction.BackLeft, new Keyframe(0, Pose(Vector2.Zero, ("head", 9))));
        var other = new AnimationClip("idle", 2);
        var history = new UndoHistory();

        Assert.True(ThreeQuarterDraft.Apply([clip, other], history));
        Assert.Equal(2, clip.Keys(Direction.FrontLeft).Count);
        Assert.Equal(9, clip.Keys(Direction.BackLeft).Single().Pose.Get("head"));   // untouched
        Assert.Empty(other.Keys(Direction.FrontLeft));                             // nothing to draft from
        Assert.Single(history.Done);

        history.Undo();
        Assert.Empty(clip.Keys(Direction.FrontLeft));
        Assert.Single(clip.Keys(Direction.BackLeft));
        Assert.False(ThreeQuarterDraft.Apply([other], history));
    }
}
