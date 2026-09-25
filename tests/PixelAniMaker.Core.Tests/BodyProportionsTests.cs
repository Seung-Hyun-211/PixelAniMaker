using System.Runtime.CompilerServices;
using PixelAniMaker.Core.Animation;
using PixelAniMaker.Core.Export;
using PixelAniMaker.Core.Imaging;
using PixelAniMaker.Core.Rigging;
using PixelAniMaker.Core.Rigging.Body;

namespace PixelAniMaker.Core.Tests;

public class BodyProportionsTests
{
    private static string Templates([CallerFilePath] string here = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "src", "PixelAniMaker.App", "Assets", "Templates"));

    private static readonly Dictionary<double, Character> Built = [];

    private static Character Mannequin(double heads)
    {
        lock (Built)
        {
            if (!Built.TryGetValue(heads, out var c))
                Built[heads] = c = MannequinBuilder.Build(BodyProportions.For(heads)).Build();
            return c;
        }
    }

    [Fact]
    public void Every_table_row_adds_up_to_its_head_count_and_values_between_rows_are_blended()
    {
        Assert.All(BodyProportions.Table, r => Assert.Equal(r.Heads, 1 + r.Neck + r.Torso + r.Legs, 6));
        var p = BodyProportions.For(6.5);
        Assert.Equal(6.5, 1 + p.Neck + p.Torso + p.Legs, 6);
        Assert.InRange(p.Legs, BodyProportions.For(6).Legs, BodyProportions.For(7).Legs);
        Assert.Equal(23, p.HeadPixels);
        Assert.Equal(30, BodyProportions.For(7, 30).HeadPixels);
        Assert.Throws<ArgumentOutOfRangeException>(() => BodyProportions.For(9));
        Assert.Throws<ArgumentOutOfRangeException>(() => BodyProportions.For(3.5));
        // taller bodies: longer legs for their size, smaller heads across
        Assert.True(BodyProportions.For(8).Legs / 8 > BodyProportions.For(5).Legs / 5);
        Assert.True(BodyProportions.For(8).HeadHalfWidth < BodyProportions.For(5).HeadHalfWidth);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void A_generated_body_has_the_template_parts_in_every_stored_direction(double heads)
    {
        var template = CharacterSpec.Parse(File.ReadAllText(Path.Combine(Templates(), "chibi96", "skeleton.json")));
        var c = Mannequin(heads);
        Assert.Equal(template.Parts.Select(p => (p.Name, p.Label, p.Parent)),
            c.Parts.Select(p => (p.Name, p.Label, p.Parent?.Name)));
        Assert.True(c.HasThreeQuarter);
        Assert.All(c.Parts, p => Assert.All(c.StoredDirections, d => Assert.NotEmpty(p.View(d).DrawnPixels())));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(8)]
    public void The_body_is_as_many_heads_tall_as_asked(double heads)
    {
        var p = BodyProportions.For(heads);
        var c = Mannequin(heads);
        foreach (var d in c.StoredDirections)
        {
            var ys = c.Parts.SelectMany(part => part.View(d).DrawnPixels().Select(px => px.Y)).ToList();
            Assert.Equal(c.Height - 1 - MannequinBuilder.Margin, ys.Max());   // feet on the bottom margin
            Assert.InRange(ys.Max() + 1 - ys.Min(), p.BodyHeightPixels - 1, p.BodyHeightPixels + 1);
        }
        var head = c.Find("head")!.View(Direction.Front);
        var headRows = head.DrawnPixels().Select(px => px.Y).ToList();
        Assert.True(headRows.Max() - headRows.Min() + 1 >= p.HeadPixels);   // the head plus its neck
    }

    [Theory]
    [InlineData(5)]
    [InlineData(8)]
    public void Every_joint_sits_on_its_parent(double heads)
    {
        var c = Mannequin(heads);
        foreach (var part in c.Parts.Where(p => p.Parent is not null))
            foreach (var d in c.StoredDirections)
            {
                var pivot = part.View(d).RestPivot;
                var parent = part.Parent!.View(d);
                Assert.True(parent.DrawnPixels().Any(px =>
                        Math.Abs(px.X + 0.5 - pivot.X) <= 1.5 && Math.Abs(px.Y + 0.5 - pivot.Y) <= 1.5),
                    $"{part.Name} in {d}");
            }
    }

    [Fact]
    public void Arms_go_in_front_of_the_chest_from_the_front_and_the_far_arm_behind_everything_from_the_side()
    {
        var c = Mannequin(7);
        List<string> Order(Direction d) => c.DrawOrder(d).Select(p => p.Name).ToList();
        Assert.True(Order(Direction.Front).IndexOf("upper_arm_r") > Order(Direction.Front).IndexOf("chest"));
        Assert.True(Order(Direction.Front).IndexOf("pelvis") > Order(Direction.Front).IndexOf("thigh_l"));
        Assert.Equal("upper_arm_r", Order(Direction.Left)[0]);
        Assert.Equal("hand_l", Order(Direction.FrontLeft)[^1]);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void The_default_clips_fit_the_canvas_once_scaled_to_the_body(double heads)
    {
        var c = MannequinBuilder.Build(BodyProportions.For(heads)).Build();
        CanvasResize.ApplyUnrecorded(c, CanvasMargins.Default);
        var clips = AnimationJson.Parse(File.ReadAllText(Path.Combine(Templates(), "chibi96", "animations.json")));
        float k = (float)(BodyProportions.For(heads).BodyHeightPixels / BodyProportions.For(4).BodyHeightPixels);
        foreach (var clip in clips)
            clip.ScaleOffsets(k);
        var hits = EdgeCheck.Find(c, clips, new Compositor(), c.Directions);
        Assert.True(hits.Count == 0, EdgeCheck.Summary(hits) + string.Join(", ", hits.Select(h => $"{h.Clip}/{h.Direction}/{h.Frame}/{h.Sides}")));
    }

    [Fact]
    public void Scaling_offsets_rounds_to_whole_pixels_and_keeps_rotations()
    {
        var clip = new AnimationClip("jump", 4);
        clip.SetKey(Direction.Left, new Keyframe(1, new PoseData(new Dictionary<string, double> { ["head"] = 10 }, new(3, -9)), Easing.Linear));
        clip.ScaleOffsets(1.5f);
        var pose = clip.KeyAt(Direction.Left, 1)!.Pose;
        Assert.Equal(new System.Numerics.Vector2(4, -14), pose.Offset);   // 4.5 rounds to even, -13.5 too
        Assert.Equal(10, pose.Get("head"));
    }

    [Fact]
    public void The_same_proportions_draw_the_same_pixels()
    {
        var a = MannequinBuilder.Build(BodyProportions.For(6), jointDiscs: false);
        var b = MannequinBuilder.Build(BodyProportions.For(6), jointDiscs: false);
        Assert.Equal(a.Spec.ToJson(), b.Spec.ToJson());
        Assert.All(a.Images, kv => Assert.Equal(kv.Value.Pixels, b.Images[kv.Key].Pixels));
        var withDiscs = MannequinBuilder.Build(BodyProportions.For(6));
        Assert.Contains(withDiscs.Images["front/shin_r.png"].Pixels, px => px == new Rgba(176, 150, 120));
        Assert.DoesNotContain(a.Images["front/shin_r.png"].Pixels, px => px == new Rgba(176, 150, 120));
    }
}
