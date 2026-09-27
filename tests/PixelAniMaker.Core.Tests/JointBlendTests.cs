using System.Numerics;
using PixelAniMaker.Core.Imaging;
using PixelAniMaker.Core.Project;
using PixelAniMaker.Core.Rigging;
using PixelAniMaker.Core.Rigging.Body;

namespace PixelAniMaker.Core.Tests;

public class JointBlendTests
{
    private static Character Body()
    {
        var c = MannequinBuilder.Build(BodyProportions.For(6), jointDiscs: false).Build();
        c.Outline.OutlineIndex = c.Palette.GetOrAdd(new Rgba(20, 20, 20));
        c.Outline.InnerIndex = c.Palette.GetOrAdd(new Rgba(200, 0, 200));
        c.Outline.Enabled = true;
        return c;
    }

    private static PoseData Bent(string part, double degrees) =>
        new(new Dictionary<string, double> { [part] = degrees }, Vector2.Zero);

    /// <summary>Empty pixels near the joint that the two parts close in on from both sides.</summary>
    private static int Holes(Character c, CompositeResult r, string child, Vector2 joint, float radius)
    {
        short a = (short)c.IndexOf(c.Find(child)!), b = (short)c.IndexOf(c.Find(child)!.Parent!);
        bool Ours(int x, int y) => r.OwnerAt(x, y) is var o && (o == a || o == b);
        int holes = 0;
        for (int y = 0; y < r.Height; y++)
            for (int x = 0; x < r.Width; x++)
                if (r.OwnerAt(x, y) == CompositeResult.NoPart && Vector2.Distance(new(x + 0.5f, y + 0.5f), joint) <= radius &&
                    (Ours(x - 1, y) && Ours(x + 1, y) || Ours(x, y - 1) && Ours(x, y + 1)))
                    holes++;
        return holes;
    }

    private static int InnerLines(Character c, CompositeResult r, Vector2 joint, float radius)
    {
        int n = 0;
        for (int y = 0; y < r.Height; y++)
            for (int x = 0; x < r.Width; x++)
                if (r.Indices[y * r.Width + x] == c.Outline.InnerIndex && Vector2.Distance(new(x + 0.5f, y + 0.5f), joint) <= radius)
                    n++;
        return n;
    }

    [Fact]
    public void Off_by_default_and_a_rest_pose_draws_the_same_pixels_either_way_apart_from_seam_lines()
    {
        var c = Body();
        Assert.False(c.JointBlend.Enabled);
        c.Outline.Enabled = false;
        var off = new Compositor().Compose(c, Direction.Left, PoseData.Rest);
        c.JointBlend.Enabled = true;
        var on = new Compositor().Compose(c, Direction.Left, PoseData.Rest);
        int changed = off.Indices.Zip(on.Indices).Count(p => p.First != p.Second);
        Assert.True(changed <= 12, $"{changed} pixels changed at rest");   // only tiny gaps closed at joints
    }

    [Theory]
    [InlineData("forearm_l", 100)]
    [InlineData("shin_r", -95)]
    [InlineData("forearm_r", -120)]
    public void A_bent_joint_has_no_gaps_and_no_seam_line(string part, double degrees)
    {
        var c = Body();
        c.JointBlend.Radius = 5;
        var pose = Bent(part, degrees);
        var joint = c.ComputeTransforms(Direction.Front, pose)[c.Find(part)!].Pivot;

        var hard = new Compositor().Compose(c, Direction.Front, pose);
        c.JointBlend.Enabled = true;
        var smooth = new Compositor().Compose(c, Direction.Front, pose);

        Assert.Equal(0, Holes(c, smooth, part, joint, 6));
        Assert.Equal(0, InnerLines(c, smooth, joint, 4));
        Assert.True(InnerLines(c, hard, joint, 4) > 0, "the hard joint shows a seam line to remove");
        // palette colours only: no blended colours appear
        Assert.All(smooth.Indices, i => Assert.InRange(i, 0, c.Palette.Count - 1));
    }

    [Fact]
    public void Far_from_the_joint_the_part_is_drawn_as_before()
    {
        var c = Body();
        var pose = Bent("forearm_l", 70);
        var t = c.ComputeTransforms(Direction.Front, pose)[c.Find("hand_l")!];
        var hard = new Compositor().Compose(c, Direction.Front, pose);
        c.JointBlend.Enabled = true;
        c.JointBlend.Radius = 3;
        var smooth = new Compositor().Compose(c, Direction.Front, pose);
        // the hand hangs well beyond the elbow's radius: its own pixels do not move
        short hand = (short)c.IndexOf(c.Find("hand_l")!);
        var a = Enumerable.Range(0, hard.Owners.Length).Where(i => hard.Owners[i] == hand && Vector2.Distance(
            new(i % hard.Width + 0.5f, i / hard.Width + 0.5f), t.Pivot) > 4).ToHashSet();
        var b = Enumerable.Range(0, smooth.Owners.Length).Where(i => smooth.Owners[i] == hand && Vector2.Distance(
            new(i % smooth.Width + 0.5f, i / smooth.Width + 0.5f), t.Pivot) > 4).ToHashSet();
        Assert.True(a.SetEquals(b));
    }

    [Fact]
    public void The_setting_is_saved_with_the_project_only_while_on()
    {
        var c = Body();
        c.JointBlend.Radius = 7;
        c.JointBlend.Enabled = true;
        using var ms = new MemoryStream();
        ProjectFile.Save(new ProjectData(c, []), ms, new RawCodec());
        ms.Position = 0;
        var loaded = ProjectFile.Load(ms, new RawCodec()).Character;
        Assert.True(loaded.JointBlend.Enabled);
        Assert.Equal(7, loaded.JointBlend.Radius);

        c.JointBlend.Enabled = false;
        using var off = new MemoryStream();
        ProjectFile.Save(new ProjectData(c, []), off, new RawCodec());
        off.Position = 0;
        using var zip = new System.IO.Compression.ZipArchive(off);
        using var reader = new StreamReader(zip.GetEntry("project.json")!.Open());
        Assert.DoesNotContain("jointBlend", reader.ReadToEnd());
    }
}
