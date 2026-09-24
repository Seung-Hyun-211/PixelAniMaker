using System.Numerics;
using PixelAniMaker.Core.Animation;
using PixelAniMaker.Core.History;
using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.Core.Tests;

public class AnimationTests
{
    private static PoseData P(double arm, float y = 0) =>
        new(new Dictionary<string, double> { ["arm"] = arm }, new Vector2(0, y));

    private static AnimationClip Clip(bool loop = true, Easing easing = Easing.Linear)
    {
        var clip = new AnimationClip("test", 8, loop: loop);
        clip.SetKey(Direction.Front, new Keyframe(0, P(0), easing));
        clip.SetKey(Direction.Front, new Keyframe(4, P(40, 4), easing));
        return clip;
    }

    [Fact]
    public void Linear_blend_between_keys()
    {
        var pose = Clip().Evaluate(Direction.Front, 2);
        Assert.Equal(20, pose.Get("arm"), 6);
        Assert.Equal(2f, pose.Offset.Y, 4);
    }

    [Fact]
    public void Ease_in_out_starts_slow()
    {
        var pose = Clip(easing: Easing.EaseInOut).Evaluate(Direction.Front, 1);
        Assert.True(pose.Get("arm") < 10);   // linear would be 10
    }

    [Fact]
    public void Step_holds_the_previous_key()
    {
        Assert.Equal(0, Clip(easing: Easing.Step).Evaluate(Direction.Front, 3).Get("arm"));
    }

    [Fact]
    public void Loop_wraps_from_last_key_to_first()
    {
        // frames 4..8 blend 40 -> 0 when looping
        Assert.Equal(20, Clip().Evaluate(Direction.Front, 6).Get("arm"), 6);
        Assert.Equal(40, Clip(loop: false).Evaluate(Direction.Front, 6).Get("arm"));
    }

    [Fact]
    public void Rotation_blends_the_short_way_round()
    {
        var pose = AnimationClip.Blend(P(170), P(-170), 0.5);
        Assert.Equal(180, Math.Abs(pose.Get("arm")), 6);
    }

    [Fact]
    public void Tracks_are_per_direction_and_right_uses_left()
    {
        var clip = Clip();
        Assert.Same(PoseData.Rest, clip.Evaluate(Direction.Back, 2));
        clip.SetKey(Direction.Left, new Keyframe(0, P(15)));
        Assert.Equal(15, clip.Evaluate(Direction.Right, 3).Get("arm"));
    }

    [Fact]
    public void Keyframe_changes_are_undoable()
    {
        var clip = Clip();
        var history = new UndoHistory();
        KeyframeChange.Apply(clip, history, Direction.Front, 4, new Keyframe(4, P(90)));
        KeyframeChange.Apply(clip, history, Direction.Front, 0, null);
        Assert.Null(clip.KeyAt(Direction.Front, 0));

        history.Undo();
        Assert.NotNull(clip.KeyAt(Direction.Front, 0));
        history.Undo();
        Assert.Equal(40, clip.KeyAt(Direction.Front, 4)!.Pose.Get("arm"));
    }

    [Fact]
    public void Keys_are_listed_in_frame_order()
    {
        var clip = Clip();
        clip.SetKey(Direction.Front, new Keyframe(2, P(10)));
        Assert.Equal([0, 2, 4], clip.Keys(Direction.Front).Select(k => k.Frame));
        Assert.Empty(clip.Keys(Direction.Back));
    }

    [Fact]
    public void Json_clips_parse()
    {
        var clips = AnimationJson.Parse("""
            {"animations":[{"name":"걷기","frames":8,"fps":10,"loop":true,"tracks":{
              "left":[{"frame":0,"rotations":{"thigh_l":25}},{"frame":4,"easing":"linear","offset":[0,-1]}]}}]}
            """);
        var walk = Assert.Single(clips);
        Assert.Equal(8, walk.FrameCount);
        Assert.Equal(25, walk.KeyAt(Direction.Left, 0)!.Pose.Get("thigh_l"));
        Assert.Equal(Easing.Linear, walk.KeyAt(Direction.Left, 4)!.Easing);
        Assert.Equal(-1f, walk.KeyAt(Direction.Right, 4)!.Pose.Offset.Y);
    }

    [Fact]
    public void Json_rejects_right_track()
    {
        Assert.Throws<FormatException>(() => AnimationJson.Parse(
            """{"animations":[{"name":"a","frames":1,"fps":1,"loop":false,"tracks":{"right":[]}}]}"""));
    }
}
