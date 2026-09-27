using System.Numerics;
using System.Runtime.CompilerServices;
using PixelAniMaker.Core.Animation;
using PixelAniMaker.Core.Rigging;
using PixelAniMaker.Core.Rigging.Body;

namespace PixelAniMaker.Core.Tests;

public class SwordPartTests
{
    private static string Templates([CallerFilePath] string here = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "src", "PixelAniMaker.App", "Assets", "Templates"));

    private static AnimationClip Clip(string name) =>
        AnimationJson.Parse(File.ReadAllText(Path.Combine(Templates(), "chibi96", "animations.json"))).Single(c => c.Name == name);

    private static Character Body(double heads = 7) => MannequinBuilder.Build(BodyProportions.For(heads)).Build();

    [Fact]
    public void Adding_a_sword_puts_it_in_the_right_hand_blade_down_and_makes_room_in_one_undo_step()
    {
        var c = Body();
        var (width, height) = (c.Width, c.Height);
        SwordPart.Add(c, SwordKind.Sword);
        var sword = c.Find(SwordPart.Name)!;
        Assert.Same(c.Find("hand_r"), sword.Parent);
        var (side, top) = SwordPart.Room(c, SwordKind.Sword);
        Assert.Equal((width + 2 * side, height + top), (c.Width, c.Height));
        foreach (var d in c.StoredDirections)
        {
            var drawn = sword.View(d).DrawnPixels().ToList();
            float grip = sword.View(d).RestPivot.Y;
            Assert.True(drawn.Max(p => p.Y) - grip > 3 * (grip - drawn.Min(p => p.Y)), $"{d}: blade below the hand");
        }
        c.History.Undo();
        Assert.Null(c.Find(SwordPart.Name));
        Assert.Equal((width, height), (c.Width, c.Height));
    }

    [Fact]
    public void The_thrust_points_the_blade_forward_in_the_side_view()
    {
        var c = Body();
        SwordPart.Add(c, SwordKind.Sword);
        var clip = Clip("검 · 찌르기");
        var t = c.ComputeTransforms(Direction.Left, clip.Evaluate(Direction.Left, 2))[c.Find(SwordPart.Name)!];
        var blade = PartTransform.Rotate(new Vector2(0, 1), t.Angle);   // the tip is at the bottom of the rest picture
        Assert.True(blade.X < -0.95f, $"blade direction {blade}");
    }

    [Theory]
    [InlineData(SwordKind.Greatsword, "양손검 · 대기")]
    [InlineData(SwordKind.Katana, "양손검 · 내려베기")]
    [InlineData(SwordKind.Katana, "양손검 · 가로베기")]
    public void A_two_handed_sword_gets_the_left_hand_on_the_pommel_end(SwordKind kind, string name)
    {
        var c = Body();
        SwordPart.Add(c, kind);
        var clip = Clip(name);
        Assert.True(WeaponHold.Apply(c, [clip], c.History));
        var sword = c.Find(SwordPart.Name)!;
        var hand = c.Find("hand_l")!;
        foreach (var d in new[] { Direction.Front, Direction.Left })
            foreach (var key in clip.Keys(d))
            {
                var t = c.ComputeTransforms(d, key.Pose);
                var view = sword.View(d);
                var pommel = t[sword].Pivot + PartTransform.Rotate(view.RestPosition + view.Attachments.All[SwordHold.Pommel] - view.RestPivot, t[sword].Angle);
                var pixels = hand.View(d).DrawnPixels().ToList();
                var palm = new Vector2((float)pixels.Average(p => p.X + 0.5), pixels.Min(p => p.Y) + 0.55f * (pixels.Max(p => p.Y) - pixels.Min(p => p.Y) + 1));
                var palmNow = t[hand].Pivot + PartTransform.Rotate(palm - hand.View(d).RestPivot, t[hand].Angle);
                Assert.True(Vector2.Distance(pommel, palmNow) < 2.5f, $"{d} frame {key.Frame}: {Vector2.Distance(pommel, palmNow):0.0}px");
            }
    }

    [Fact]
    public void A_one_handed_sword_leaves_the_left_arm_as_the_clip_has_it()
    {
        var c = Body();
        SwordPart.Add(c, SwordKind.Sword);
        var clip = Clip("검 · 내려베기");
        var before = clip.Keys(Direction.Left).Select(k => k.Pose.Get("upper_arm_l")).ToList();
        Assert.False(WeaponHold.Apply(c, [clip], c.History));
        Assert.Equal(before, clip.Keys(Direction.Left).Select(k => k.Pose.Get("upper_arm_l")));
    }
}
