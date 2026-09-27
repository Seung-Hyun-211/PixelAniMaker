using System.Numerics;
using System.Runtime.CompilerServices;
using PixelAniMaker.Core.Animation;
using PixelAniMaker.Core.Rigging;
using PixelAniMaker.Core.Rigging.Body;

namespace PixelAniMaker.Core.Tests;

public class GunPartTests
{
    private static string Templates([CallerFilePath] string here = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "src", "PixelAniMaker.App", "Assets", "Templates"));

    private static Character Body(double heads = 6) => MannequinBuilder.Build(BodyProportions.For(heads)).Build();

    [Fact]
    public void Adding_a_gun_puts_it_in_the_right_hand_and_widens_the_canvas_in_one_undo_step()
    {
        var c = Body();
        int width = c.Width;
        Assert.True(GunPart.CanAdd(c));
        GunPart.Add(c, GunKind.Rifle);

        var gun = c.Find(GunPart.Name)!;
        Assert.Same(c.Find("hand_r"), gun.Parent);
        Assert.True(gun.IsCustom);
        Assert.Equal(width + 2 * GunPart.Room(c, GunKind.Rifle), c.Width);
        Assert.False(GunPart.CanAdd(c));

        c.History.Undo();
        Assert.Null(c.Find(GunPart.Name));
        Assert.Equal(width, c.Width);
        c.History.Redo();
        GunPart.Remove(c);
        Assert.Null(c.Find(GunPart.Name));
    }

    [Theory]
    [InlineData(GunKind.Rifle)]
    [InlineData(GunKind.Pistol)]
    public void At_rest_the_grip_is_in_the_hand_and_the_muzzle_points_down(GunKind kind)
    {
        var c = Body();
        GunPart.Add(c, kind);
        var gun = c.Find(GunPart.Name)!;
        foreach (var d in c.StoredDirections)
        {
            var hand = c.Find("hand_r")!.View(d).DrawnPixels().ToList();
            var joint = gun.View(d).RestPivot;
            Assert.InRange(joint.X, hand.Min(p => p.X), hand.Max(p => p.X) + 1);
            Assert.InRange(joint.Y, hand.Min(p => p.Y), hand.Max(p => p.Y) + 1);
            var drawn = gun.View(d).DrawnPixels().ToList();
            Assert.True(drawn.Max(p => p.Y) - drawn.Min(p => p.Y) > drawn.Max(p => p.X) - drawn.Min(p => p.X), $"{d}: hangs down");
            Assert.True(drawn.Max(p => p.Y) - joint.Y > joint.Y - drawn.Min(p => p.Y), $"{d}: muzzle below the grip");
        }
        if (kind == GunKind.Rifle)
            Assert.InRange(gun.View(Direction.Left).DrawnPixels().Max(p => p.Y) - gun.View(Direction.Left).DrawnPixels().Min(p => p.Y),
                0.3 * GunPart.BodyHeight(c), 0.5 * GunPart.BodyHeight(c));
    }

    [Fact]
    public void It_is_drawn_over_the_chest_from_the_front_and_side_and_behind_the_body_from_the_back()
    {
        var c = Body();
        GunPart.Add(c, GunKind.Rifle);
        List<string> Order(Direction d) => c.DrawOrder(d).Select(p => p.Name).ToList();
        foreach (var d in new[] { Direction.Front, Direction.Left })
        {
            Assert.True(Order(d).IndexOf("gun") > Order(d).IndexOf("chest"), $"{d}");
            Assert.True(Order(d).IndexOf("gun") < Order(d).IndexOf("upper_arm_l"), $"{d}: under the near/left arm");
        }
        Assert.Equal("gun", Order(Direction.Back)[0]);
    }

    /// <summary>Where a named gun point is on the canvas in a pose.</summary>
    private static Vector2 GunPoint(Character c, Direction d, PoseData pose, string point)
    {
        var gun = c.Find(GunPart.Name)!;
        var view = gun.View(d);
        var t = c.ComputeTransforms(d, pose)[gun];
        return t.Pivot + PartTransform.Rotate(view.RestPosition + view.Attachments.All[point] - view.RestPivot, t.Angle);
    }

    private static Vector2 Palm(Character c, Direction d, PoseData pose, string hand)
    {
        var part = c.Find(hand)!;
        var pixels = part.View(d).DrawnPixels().ToList();
        var palm = new Vector2((float)pixels.Average(p => p.X + 0.5), pixels.Min(p => p.Y) + 0.55f * (pixels.Max(p => p.Y) - pixels.Min(p => p.Y) + 1));
        var t = c.ComputeTransforms(d, pose)[part];
        return t.Pivot + PartTransform.Rotate(palm - part.View(d).RestPivot, t.Angle);
    }

    [Theory]
    [InlineData(5, "소총 · 조준")]
    [InlineData(7, "소총 · 조준")]
    [InlineData(8, "소총 · 대기")]
    [InlineData(6, "소총 · 앉아 쏴")]
    public void Fitting_puts_the_stock_on_the_shoulder_and_the_left_hand_on_the_handguard(double heads, string name)
    {
        var c = Body(heads);
        GunPart.Add(c, GunKind.Rifle);
        var clip = AnimationJson.Parse(File.ReadAllText(Path.Combine(Templates(), "chibi96", "animations.json"))).Single(x => x.Name == name);
        float before = c.ComputeTransforms(Direction.Left, clip.Evaluate(Direction.Left, 0))[c.Find(GunPart.Name)!].Angle;
        GunHold.FitUnrecorded(c, [clip]);
        foreach (var d in new[] { Direction.Left, Direction.Front })
        {
            var pose = clip.Evaluate(d, 0);
            var shoulder = c.ComputeTransforms(d, pose)[c.Find("upper_arm_r")!].Pivot;
            Assert.True(Vector2.Distance(GunPoint(c, d, pose, GunHold.Butt), shoulder) < 2.5f, $"{d}: stock on the shoulder");
            Assert.True(Vector2.Distance(GunPoint(c, d, pose, GunHold.Guard), Palm(c, d, pose, "hand_l")) < 2.5f, $"{d}: left hand on the handguard");
        }
        float after = c.ComputeTransforms(Direction.Left, clip.Evaluate(Direction.Left, 0))[c.Find(GunPart.Name)!].Angle;
        Assert.Equal(Pose.Normalize(before * 180 / Math.PI), Pose.Normalize(after * 180 / Math.PI), 0);   // still aims the same way
    }

    [Fact]
    public void Fitting_is_one_undo_step_and_a_pistol_is_held_with_both_hands()
    {
        var c = Body(7);
        GunPart.Add(c, GunKind.Pistol);
        var clip = AnimationJson.Parse(File.ReadAllText(Path.Combine(Templates(), "chibi96", "animations.json"))).Single(x => x.Name == "권총 · 조준");
        var original = clip.Evaluate(Direction.Left, 0);
        Assert.True(GunHold.Apply(c, [clip], c.History));
        var pose = clip.Evaluate(Direction.Left, 0);
        Assert.True(Vector2.Distance(Palm(c, Direction.Left, pose, "hand_l"), Palm(c, Direction.Left, pose, "hand_r")) < 3, "both hands on the grip");
        c.History.Undo();
        Assert.Equal(original.Rotations, clip.Evaluate(Direction.Left, 0).Rotations);
    }

    [Fact]
    public void The_aim_clip_levels_the_gun_in_the_side_view()
    {
        var c = Body(7);
        GunPart.Add(c, GunKind.Rifle);
        var aim = AnimationJson.Parse(File.ReadAllText(Path.Combine(Templates(), "chibi96", "animations.json"))).Single(x => x.Name == "소총 · 조준");
        var t = c.ComputeTransforms(Direction.Left, aim.Evaluate(Direction.Left, 0))[c.Find(GunPart.Name)!];
        // the muzzle is at the bottom of the rest picture: +90° turns it forward (screen left)
        var muzzle = PartTransform.Rotate(new Vector2(0, 1), t.Angle);
        Assert.True(muzzle.X < -0.95f, $"muzzle direction {muzzle}");
        // and the grip sits in front of the chest, about shoulder high
        var chest = c.ComputeTransforms(Direction.Left, aim.Evaluate(Direction.Left, 0))[c.Find("chest")!].Pivot;
        Assert.True(t.Pivot.Y < chest.Y, "above the bottom of the chest");
    }
}
