using System.Numerics;
using PixelAniMaker.Core.Editing;
using PixelAniMaker.Core.Imaging;
using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.Core.Tests;

public class RiggingTests
{
    private static readonly Rgba Red = new(255, 0, 0);
    private static readonly Rgba Blue = new(0, 0, 255);

    private static RgbaImage Solid(int w, int h, Rgba c) => new(w, h, Enumerable.Repeat(c, w * h).ToArray());

    /// <summary>
    /// 16x16 canvas. "body" 4x4 red at (6,6), joint at its centre (8,8).
    /// "arm" 2x4 blue hanging from the body's bottom edge: image at (7,10), joint (8,10).
    /// </summary>
    private static Character TwoParts()
    {
        var spec = new CharacterSpec(16, 16,
        [
            new PartSpec("body", "몸", null, 0, 6, 6, 8, 8, "body"),
            new PartSpec("arm", "팔", "body", 1, 7, 10, 8, 10, "arm"),
        ]);
        return spec.Build(name => name == "body" ? Solid(4, 4, Red) : Solid(2, 4, Blue));
    }

    [Fact]
    public void Rest_pose_places_images_at_rest_positions()
    {
        var c = TwoParts();
        var result = new Compositor().Compose(c);
        int red = c.Palette.IndexOf(Red), blue = c.Palette.IndexOf(Blue);

        Assert.Equal(red, result.Indices[6 * 16 + 6]);
        Assert.Equal(red, result.Indices[9 * 16 + 9]);
        Assert.Equal(blue, result.Indices[10 * 16 + 7]);
        Assert.Equal(blue, result.Indices[13 * 16 + 8]);
        Assert.Equal(0, result.Indices[14 * 16 + 8]);
        Assert.Equal(16, result.Indices.Count(i => i == red));
        Assert.Equal(8, result.Indices.Count(i => i == blue));
    }

    [Fact]
    public void Owners_report_the_topmost_part()
    {
        var c = TwoParts();
        var result = new Compositor().Compose(c);
        Assert.Equal(0, result.OwnerAt(6, 6));
        Assert.Equal(1, result.OwnerAt(7, 12));
        Assert.Equal(CompositeResult.NoPart, result.OwnerAt(0, 0));
        Assert.Equal(CompositeResult.NoPart, result.OwnerAt(-1, 99));
    }

    [Fact]
    public void Rotating_parent_carries_child()
    {
        var c = TwoParts();
        c.Pose.Set("body", 90);
        var arm = c.ComputeTransforms()[c.Find("arm")!];

        // arm joint was 2 px below the body joint; after +90° (clockwise on screen) it is 2 px to the left
        Assert.Equal(6f, arm.Pivot.X, 3);
        Assert.Equal(8f, arm.Pivot.Y, 3);
        Assert.Equal(MathF.PI / 2, arm.Angle, 4);
    }

    [Fact]
    public void Quarter_turn_keeps_pixel_count()
    {
        var c = TwoParts();
        c.Pose.Set("arm", 90);
        var result = new Compositor().Compose(c);
        Assert.Equal(8, result.Indices.Count(i => i == c.Palette.IndexOf(Blue)));
    }

    [Fact]
    public void Free_rotation_uses_only_palette_colours()
    {
        var c = TwoParts();
        c.Pose.Set("arm", 37);
        var result = new Compositor().Compose(c);
        Assert.All(result.Indices, i => Assert.InRange(i, 0, c.Palette.Count - 1));
        Assert.Contains(result.Indices, i => i == c.Palette.IndexOf(Blue));
    }

    [Fact]
    public void Compositor_picks_up_part_edits_while_rotated()
    {
        var c = TwoParts();
        c.Pose.Set("arm", 30);
        var compositor = new Compositor();
        compositor.Compose(c);

        var doc = c.CreateDocument(c.Find("arm")!);
        doc.BeginStroke(PencilTool.Eraser, 0, 0);
        doc.ContinueStroke(1, 3);
        doc.EndStroke();
        doc.BeginStroke(PencilTool.Eraser, 1, 0);
        doc.ContinueStroke(0, 3);
        doc.EndStroke();

        var result = compositor.Compose(c);
        Assert.True(result.Indices.Count(i => i == c.Palette.IndexOf(Blue)) < 8);
    }

    [Fact]
    public void Canvas_pixel_maps_back_to_part_pixel()
    {
        var c = TwoParts();
        var arm = c.ComputeTransforms()[c.Find("arm")!];
        Assert.Equal((0, 0), arm.ToLocalPixel(7, 10));
        Assert.Equal((1, 3), arm.ToLocalPixel(8, 13));
    }

    [Fact]
    public void Parts_share_history_and_colours()
    {
        var c = TwoParts();
        var a = c.CreateDocument(c.Find("body")!);
        var b = c.CreateDocument(c.Find("arm")!);
        a.Colors.Primary = 2;
        Assert.Equal(2, b.Colors.Primary);

        b.BeginStroke(PencilTool.Eraser, 0, 0);
        b.EndStroke();
        Assert.True(a.History.CanUndo);
        a.Undo();
        Assert.NotEqual(0, c.Find("arm")!.Image[0, 0]);
    }

    [Fact]
    public void Pose_changes_are_undoable()
    {
        var c = TwoParts();
        PoseChange.Apply(c.Pose, c.History, "arm", 30);
        PoseChange.Apply(c.Pose, c.History, "body", -10);
        PoseChange.ResetAll(c.Pose, c.History);
        Assert.Equal(0, c.Pose.Get("arm"));

        c.History.Undo();
        Assert.Equal(30, c.Pose.Get("arm"));
        Assert.Equal(-10, c.Pose.Get("body"));
        c.History.Undo();
        Assert.Equal(0, c.Pose.Get("body"));
        c.History.Redo();
        Assert.Equal(-10, c.Pose.Get("body"));
    }

    [Fact]
    public void Hierarchy_lists_parents_first()
    {
        var c = TwoParts();
        Assert.Equal(["body", "arm"], c.Hierarchy().Select(p => p.Name));
        Assert.Equal(1, c.Find("arm")!.Depth);
    }

    [Fact]
    public void Unknown_parent_is_rejected()
    {
        var spec = new CharacterSpec(4, 4, [new PartSpec("a", "a", "missing", 0, 0, 0, 0, 0, "a")]);
        Assert.Throws<FormatException>(() => spec.Build(_ => Solid(1, 1, Red)));
    }

    [Fact]
    public void Spec_parses_json()
    {
        var spec = CharacterSpec.Parse("""
            {"width":64,"height":128,"parts":[
              {"name":"pelvis","label":"골반","parent":null,"order":6,"x":19,"y":60,"jointX":32.0,"jointY":65.0,"image":"pelvis.png"}]}
            """);
        Assert.Equal(64, spec.Width);
        Assert.Equal(new Vector2(32, 65), new Vector2(spec.Parts[0].JointX, spec.Parts[0].JointY));
    }

    [Theory]
    [InlineData(190, -170)]
    [InlineData(-180, 180)]
    [InlineData(540, 180)]
    [InlineData(45, 45)]
    public void Pose_normalizes_angles(double input, double expected) =>
        Assert.Equal(expected, Pose.Normalize(input));

    [Fact]
    public void Scale2x_doubles_and_keeps_colours()
    {
        var palette = new Palette();
        var img = IndexedImage.FromRgba(2, 2, [Red, Blue, Blue, Red], palette);
        var up = Scale2x.Upscale8(img);
        Assert.Equal((16, 16), (up.Width, up.Height));
        Assert.All(up.Pixels.ToArray(), i => Assert.InRange(i, 1, 2));
    }
}
