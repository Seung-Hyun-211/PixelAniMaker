using System.Numerics;
using PixelAniMaker.Core.Animation;
using PixelAniMaker.Core.Project;
using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.Core.Tests;

public class SecondaryMotionTests
{
    /// <summary>The bust fixture with a bust (bust preset on) and a clip that bobs the body up and down.</summary>
    private static (Character Character, AnimationClip Clip) Bobbing(int frames = 6, bool loop = true)
    {
        var c = BustPartTests.Body();
        BustPart.Add(c, BustSize.Large);
        var clip = new AnimationClip("run", frames, 10, loop);
        for (int f = 0; f < frames; f += 2)
            foreach (var d in DirectionExtensions.Stored)
                clip.SetKey(d, new Keyframe(f, new PoseData(new Dictionary<string, double>(), new Vector2(0, f % 4 == 0 ? 0 : -6)), Easing.Linear));
        return (c, clip);
    }

    private static Vector2 Offset(SecondaryFrame frame, Character c) =>
        frame.Offsets.GetValueOrDefault(c.Find(BustPart.Name)!);

    [Fact]
    public void The_bust_is_added_with_the_bust_preset_and_bobbing_makes_it_lag_within_the_limit()
    {
        var (c, clip) = Bobbing();
        Assert.Equal(SecondarySettings.Bust, c.Find(BustPart.Name)!.Secondary);
        var frames = SecondaryMotion.Solve(c, clip, Direction.Front);

        Assert.Contains(frames, f => Offset(f, c) != Vector2.Zero);
        Assert.All(frames, f => Assert.InRange(MathF.Abs(Offset(f, c).Y), 0, SecondarySettings.Bust.Max));
        Assert.All(frames, f => Assert.InRange(MathF.Abs(Offset(f, c).X), 0, MathF.Floor(SecondarySettings.Bust.Max / 2)));
        Assert.Equal(frames.Select(f => Offset(f, c)), SecondaryMotion.Solve(c, clip, Direction.Front).Select(f => Offset(f, c)));
    }

    [Fact]
    public void A_still_clip_or_zero_strength_or_no_settings_add_nothing()
    {
        var c = BustPartTests.Body();
        BustPart.Add(c, BustSize.Large);
        var still = new AnimationClip("idle", 4);
        Assert.All(SecondaryMotion.Solve(c, still, Direction.Left), f => Assert.True(f.IsEmpty));

        var (bobbing, clip) = Bobbing();
        var bust = bobbing.Find(BustPart.Name)!;
        SecondaryChange.Apply(bust, bobbing.History, SecondarySettings.Bust with { Strength = 0 });
        Assert.All(SecondaryMotion.Solve(bobbing, clip, Direction.Front), f => Assert.True(f.IsEmpty));
        bobbing.History.Undo();
        Assert.Equal(SecondarySettings.Bust, bust.Secondary);
        SecondaryChange.Apply(bust, bobbing.History, null);
        Assert.False(SecondaryMotion.HasAny(bobbing));
    }

    [Fact]
    public void A_looping_clip_joins_its_last_frame_to_the_first_without_a_jump()
    {
        var (c, clip) = Bobbing(8);
        var y = SecondaryMotion.Solve(c, clip, Direction.Front).Select(f => Offset(f, c).Y).ToList();
        float seam = MathF.Abs(y[^1] - y[0]);
        float largestStep = Enumerable.Range(1, y.Count - 1).Max(i => MathF.Abs(y[i] - y[i - 1]));
        Assert.True(seam <= largestStep, $"seam {seam} > largest step {largestStep}: {string.Join(",", y)}");
    }

    [Fact]
    public void Holding_a_frame_longer_changes_what_follows()
    {
        var (c, clip) = Bobbing(8, loop: false);
        var before = SecondaryMotion.Solve(c, clip, Direction.Front).Select(f => Offset(f, c)).ToList();
        clip.SetHold(1, 4);
        var after = SecondaryMotion.Solve(c, clip, Direction.Front).Select(f => Offset(f, c)).ToList();
        Assert.Equal(before.Take(2), after.Take(2));                   // frames up to the held one are the same
        Assert.NotEqual(before.Skip(2), after.Skip(2));
    }

    [Fact]
    public void A_deform_keeps_the_top_row_and_moves_the_bottom()
    {
        var c = BustPartTests.Body();
        BustPart.Add(c, BustSize.Large);
        var bust = c.Find(BustPart.Name)!;
        short owner = (short)c.IndexOf(bust);
        var compositor = new Compositor();
        (int Top, int Bottom) Rows(CompositeResult r)
        {
            var ys = Enumerable.Range(0, r.Owners.Length).Where(i => r.Owners[i] == owner).Select(i => i / r.Width).ToList();
            return (ys.Min(), ys.Max());
        }
        var still = Rows(compositor.Compose(c, Direction.Left));
        var down = Rows(compositor.Compose(c, Direction.Left, secondary: new SecondaryFrame(
            new Dictionary<Part, Vector2> { [bust] = new(0, 2) }, new Dictionary<Part, float>())));
        Assert.Equal(still.Top, down.Top);
        Assert.Equal(still.Bottom + 2, down.Bottom);
    }

    [Fact]
    public void A_swinging_part_gets_extra_angles_within_its_limit_when_the_body_moves_sideways()
    {
        var c = BustPartTests.Body();
        var head = c.Find("head")!;
        SecondaryChange.Apply(head, c.History, SecondarySettings.Hair);
        var clip = new AnimationClip("sway", 6, 10);
        for (int f = 0; f < 6; f += 3)
            foreach (var d in DirectionExtensions.Stored)
                clip.SetKey(d, new Keyframe(f, new PoseData(new Dictionary<string, double>(), new Vector2(f == 0 ? -8 : 8, 0)), Easing.Linear));

        var angles = SecondaryMotion.Solve(c, clip, Direction.Front).Select(f => f.Angles.GetValueOrDefault(head)).ToList();
        Assert.Contains(angles, a => a != 0);
        Assert.All(angles, a => Assert.InRange(MathF.Abs(a), 0, SecondarySettings.Hair.Max));
        Assert.Contains(angles, a => a > 0);
        Assert.Contains(angles, a => a < 0);
    }

    [Fact]
    public void Settings_and_the_export_switch_are_saved_and_the_switch_turns_sway_off_in_exports()
    {
        var (c, clip) = Bobbing();
        c.SecondaryInExport = false;
        using var ms = new MemoryStream();
        ProjectFile.Save(new ProjectData(c, [clip]), ms, new RawCodec());
        ms.Position = 0;
        var loaded = ProjectFile.Load(ms, new RawCodec());
        Assert.False(loaded.Character.SecondaryInExport);
        Assert.Equal(SecondarySettings.Bust, loaded.Character.Find(BustPart.Name)!.Secondary);

        var compositor = new Compositor();
        var off = SpriteBaker.Bake(c, clip, compositor, directions: [Direction.Front])[Direction.Front];
        var on = SpriteBaker.Bake(c, clip, compositor, directions: [Direction.Front], secondary: true)[Direction.Front];
        var plain = Enumerable.Range(0, clip.FrameCount).Select(f => compositor.Compose(c, Direction.Front, clip.Evaluate(Direction.Front, f))).ToList();
        Assert.All(Enumerable.Range(0, clip.FrameCount), f => Assert.Equal(plain[f].Indices, off[f].Indices));
        Assert.Contains(Enumerable.Range(0, clip.FrameCount), f => !plain[f].Indices.SequenceEqual(on[f].Indices));
    }
}
