using System.Runtime.CompilerServices;
using PixelAniMaker.Core.Animation;
using PixelAniMaker.Core.Export;
using PixelAniMaker.Core.Rigging;
using PixelAniMaker.Core.Rigging.Body;

namespace PixelAniMaker.Core.Tests;

public class BodyShapeTests
{
    private static string Templates([CallerFilePath] string here = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "src", "PixelAniMaker.App", "Assets", "Templates"));

    public static IEnumerable<object[]> Shapes() => Enum.GetValues<BodyShape>().Select(s => new object[] { s });

    /// <summary>Widest drawn span of a part at rest, in pixels.</summary>
    private static int Width(Character c, string part, Direction d = Direction.Front)
    {
        var xs = c.Find(part)!.View(d).DrawnPixels().Select(p => p.X).ToList();
        return xs.Max() - xs.Min() + 1;
    }

    [Fact]
    public void Every_shape_has_factors_and_standard_changes_nothing()
    {
        Assert.All(Enum.GetValues<BodyShape>(), s => Assert.True(BodyShapeFactors.Table.ContainsKey(s), s.ToString()));
        var plain = BodyProportions.For(6);
        var standard = BodyProportions.For(6, shape: BodyShape.Standard);
        Assert.Equal(plain.ShoulderHalf, standard.ShoulderHalf);
        Assert.Equal(plain.ThighRadius, standard.ThighRadius);
        Assert.Equal(MannequinBuilder.Build(plain).Spec.ToJson(), MannequinBuilder.Build(standard).Spec.ToJson());
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public void A_shape_changes_widths_but_not_lengths(BodyShape shape)
    {
        var standard = BodyProportions.For(6);
        var p = BodyProportions.For(6, shape: shape);
        Assert.Equal(shape, p.Shape);
        Assert.Equal(standard.BodyHeightPixels, p.BodyHeightPixels);
        Assert.Equal((standard.Neck, standard.Torso, standard.Legs, standard.Arm), (p.Neck, p.Torso, p.Legs, p.Arm));
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public void Every_shape_builds_the_template_parts_with_joints_on_their_parents(BodyShape shape)
    {
        var template = CharacterSpec.Parse(File.ReadAllText(Path.Combine(Templates(), "chibi96", "skeleton.json")));
        var c = MannequinBuilder.Build(BodyProportions.For(6, shape: shape)).Build();
        Assert.Equal(template.Parts.Select(p => p.Name), c.Parts.Select(p => p.Name));
        foreach (var part in c.Parts.Where(p => p.Parent is not null))
            foreach (var d in c.StoredDirections)
            {
                Assert.NotEmpty(part.View(d).DrawnPixels());
                var pivot = part.View(d).RestPivot;
                Assert.True(part.Parent!.View(d).DrawnPixels().Any(px =>
                    Math.Abs(px.X + 0.5 - pivot.X) <= 1.5 && Math.Abs(px.Y + 0.5 - pivot.Y) <= 1.5), $"{shape}: {part.Name} in {d}");
            }
    }

    [Fact]
    public void Shapes_differ_where_they_should()
    {
        Character Build(BodyShape s) => MannequinBuilder.Build(BodyProportions.For(6, shape: s), jointDiscs: false).Build();
        var standard = Build(BodyShape.Standard);
        var feminine = Build(BodyShape.Feminine);
        var chubby = Build(BodyShape.Chubby);
        var muscular = Build(BodyShape.Muscular);
        var slim = Build(BodyShape.Slim);

        // feminine: hips wider than the chest, shoulders narrower than the standard body
        Assert.True(Width(feminine, "pelvis") > Width(feminine, "chest"));
        Assert.True(Width(feminine, "chest") < Width(standard, "chest"));
        Assert.True(Width(feminine, "waist") < Width(standard, "waist"));
        // chubby: a belly from the side, thick legs
        Assert.True(Width(chubby, "waist", Direction.Left) > Width(standard, "waist", Direction.Left));
        Assert.True(Width(chubby, "thigh_l") > Width(standard, "thigh_l"));
        // muscular: broad chest and heavy arms; slim: thin arms
        Assert.True(Width(muscular, "chest") > Width(standard, "chest"));
        Assert.True(Width(muscular, "upper_arm_l") > Width(standard, "upper_arm_l"));
        Assert.True(Width(slim, "upper_arm_l") < Width(standard, "upper_arm_l"));
    }

    [Fact]
    public void Custom_factors_replace_the_shape_and_the_head_factor_widens_the_head()
    {
        var custom = new BodyShapeFactors(0.5, 0.6, 0.7, 0.8, 0.9, 0.7, 0.8, Head: 1.1);
        var p = BodyProportions.For(7, 22, BodyShape.Masculine, custom);
        var plain = BodyProportions.For(7, 22);
        Assert.Equal(plain.ShoulderHalf * 0.5, p.ShoulderHalf, 9);
        Assert.Equal(plain.HipHalf * 0.7, p.HipHalf, 9);
        Assert.Equal(plain.HeadHalfWidth * 1.1, p.HeadHalfWidth, 9);
        Assert.Equal(plain.BodyHeightPixels, p.BodyHeightPixels);

        int Head(BodyProportions b) => MannequinBuilder.Build(b, jointDiscs: false).Build().Find("head")!.View(Direction.Front)
            .DrawnPixels().GroupBy(px => px.Y).Max(row => row.Max(px => px.X) - row.Min(px => px.X) + 1);
        Assert.True(Head(BodyProportions.For(7, 22, factors: BodyShapeFactors.One with { Head = 1.15 })) > Head(plain));
    }

    [Theory]
    [InlineData(BodyShape.Chubby, 4)]
    [InlineData(BodyShape.Muscular, 8)]
    public void The_default_clips_fit_the_widest_shapes(BodyShape shape, double heads)
    {
        var c = MannequinBuilder.Build(BodyProportions.For(heads, shape: shape)).Build();
        CanvasResize.ApplyUnrecorded(c, CanvasMargins.Default);
        var clips = AnimationJson.Parse(File.ReadAllText(Path.Combine(Templates(), "chibi96", "animations.json")));
        float k = (float)(BodyProportions.For(heads).BodyHeightPixels / BodyProportions.For(4).BodyHeightPixels);
        foreach (var clip in clips)
            clip.ScaleOffsets(k);
        var hits = EdgeCheck.Find(c, clips, new Compositor(), c.Directions);
        Assert.True(hits.Count == 0, EdgeCheck.Summary(hits));
    }
}
