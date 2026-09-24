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

    private static PartSpec Spec(string name, string? parent, int x, int y, float jx, float jy, int order,
        (int X, int Y, int Order)? left = null) =>
        new(name, name, parent, new Dictionary<string, PartViewSpec>
        {
            ["front"] = new(x, y, jx, jy, order, name),
            ["back"] = new(x, y, jx, jy, order, name),
            ["left"] = left is { } l ? new(l.X, l.Y, jx + l.X - x, jy + l.Y - y, l.Order, name) : new(x, y, jx, jy, order, name),
        });

    /// <summary>
    /// 16x16 canvas. "body" 4x4 red at (6,6), joint at its centre (8,8).
    /// "arm" 2x4 blue hanging from the body's bottom edge: image at (7,10), joint (8,10).
    /// In the left view the arm sits one pixel further left (6,10) and is drawn behind the body.
    /// </summary>
    private static Character TwoParts()
    {
        var spec = new CharacterSpec(16, 16,
        [
            Spec("body", null, 6, 6, 8, 8, 0, left: (6, 6, 1)),
            Spec("arm", "body", 7, 10, 8, 10, 1, left: (6, 10, 0)),
        ]);
        return spec.Build(name => name == "body" ? Solid(4, 4, Red) : Solid(2, 4, Blue));
    }

    private static Direction F => Direction.Front;

    [Fact]
    public void Rest_pose_places_images_at_rest_positions()
    {
        var c = TwoParts();
        var result = new Compositor().Compose(c, F);
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
        var result = new Compositor().Compose(c, F);
        Assert.Equal(0, result.OwnerAt(6, 6));
        Assert.Equal(1, result.OwnerAt(7, 12));
        Assert.Equal(CompositeResult.NoPart, result.OwnerAt(0, 0));
        Assert.Equal(CompositeResult.NoPart, result.OwnerAt(-1, 99));
    }

    [Fact]
    public void Rotating_parent_carries_child()
    {
        var c = TwoParts();
        c.PoseFor(F).Set("body", 90);
        var arm = c.ComputeTransforms(F)[c.Find("arm")!];

        // arm joint was 2 px below the body joint; after +90° (clockwise on screen) it is 2 px to the left
        Assert.Equal(6f, arm.Pivot.X, 3);
        Assert.Equal(8f, arm.Pivot.Y, 3);
        Assert.Equal(MathF.PI / 2, arm.Angle, 4);
    }

    [Fact]
    public void Quarter_turn_keeps_pixel_count()
    {
        var c = TwoParts();
        c.PoseFor(F).Set("arm", 90);
        var result = new Compositor().Compose(c, F);
        Assert.Equal(8, result.Indices.Count(i => i == c.Palette.IndexOf(Blue)));
    }

    [Fact]
    public void Free_rotation_uses_only_palette_colours()
    {
        var c = TwoParts();
        c.PoseFor(F).Set("arm", 37);
        var result = new Compositor().Compose(c, F);
        Assert.All(result.Indices, i => Assert.InRange(i, 0, c.Palette.Count - 1));
        Assert.Contains(result.Indices, i => i == c.Palette.IndexOf(Blue));
    }

    [Fact]
    public void Compositor_picks_up_part_edits_while_rotated()
    {
        var c = TwoParts();
        c.PoseFor(F).Set("arm", 30);
        var compositor = new Compositor();
        compositor.Compose(c, F);

        var doc = c.CreateDocument(c.Find("arm")!, F);
        doc.BeginStroke(PencilTool.Eraser, 0, 0);
        doc.ContinueStroke(1, 3);
        doc.EndStroke();
        doc.BeginStroke(PencilTool.Eraser, 1, 0);
        doc.ContinueStroke(0, 3);
        doc.EndStroke();

        var result = compositor.Compose(c, F);
        Assert.True(result.Indices.Count(i => i == c.Palette.IndexOf(Blue)) < 8);
    }

    [Fact]
    public void Canvas_pixel_maps_back_to_part_pixel()
    {
        var c = TwoParts();
        var arm = c.ComputeTransforms(F)[c.Find("arm")!];
        Assert.Equal((0, 0), arm.ToLocalPixel(7, 10));
        Assert.Equal((1, 3), arm.ToLocalPixel(8, 13));
    }

    [Fact]
    public void Parts_share_history_and_colours()
    {
        var c = TwoParts();
        var a = c.CreateDocument(c.Find("body")!, F);
        var b = c.CreateDocument(c.Find("arm")!, F);
        a.Colors.Primary = 2;
        Assert.Equal(2, b.Colors.Primary);

        b.BeginStroke(PencilTool.Eraser, 0, 0);
        b.EndStroke();
        Assert.True(a.History.CanUndo);
        a.Undo();
        Assert.NotEqual(0, c.Find("arm")!.View(F).Image[0, 0]);
    }

    [Fact]
    public void Pose_changes_are_undoable()
    {
        var c = TwoParts();
        PoseChange.Apply(c.PoseFor(F), c.History, "arm", 30);
        PoseChange.Apply(c.PoseFor(F), c.History, "body", -10);
        PoseChange.ResetAll(c.PoseFor(F), c.History);
        Assert.Equal(0, c.PoseFor(F).Get("arm"));

        c.History.Undo();
        Assert.Equal(30, c.PoseFor(F).Get("arm"));
        Assert.Equal(-10, c.PoseFor(F).Get("body"));
        c.History.Undo();
        Assert.Equal(0, c.PoseFor(F).Get("body"));
        c.History.Redo();
        Assert.Equal(-10, c.PoseFor(F).Get("body"));
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
        var spec = new CharacterSpec(4, 4, [Spec("a", "missing", 0, 0, 0, 0, 0)]);
        Assert.Throws<FormatException>(() => spec.Build(_ => Solid(1, 1, Red)));
    }

    [Fact]
    public void Spec_parses_json()
    {
        var spec = CharacterSpec.Parse("""
            {"width":96,"height":128,"parts":[
              {"name":"pelvis","label":"골반","parent":null,"views":{
                "front":{"x":19,"y":60,"jointX":48.0,"jointY":65.0,"order":6,"image":"front/pelvis.png"}}}]}
            """);
        Assert.Equal(96, spec.Width);
        var v = spec.Parts[0].Views["front"];
        Assert.Equal(new Vector2(48, 65), new Vector2(v.JointX, v.JointY));
    }

    [Fact]
    public void Missing_view_is_rejected()
    {
        var spec = new CharacterSpec(4, 4,
            [new PartSpec("a", "a", null, new Dictionary<string, PartViewSpec> { ["front"] = new(0, 0, 0, 0, 0, "a") })]);
        Assert.Throws<FormatException>(() => spec.Build(_ => Solid(1, 1, Red)));
    }

    [Fact]
    public void Each_direction_uses_its_own_layout_and_draw_order()
    {
        var c = TwoParts();
        var compositor = new Compositor();
        var front = compositor.Compose(c, Direction.Front);
        var left = compositor.Compose(c, Direction.Left);

        Assert.Equal(1, front.OwnerAt(7, 12));          // arm below the body
        Assert.Equal(1, left.OwnerAt(6, 12));           // arm one pixel further left
        Assert.Equal(0, left.OwnerAt(7, 9));            // body drawn over the arm where they meet
        Assert.Equal(CompositeResult.NoPart, left.OwnerAt(8, 12));
    }

    [Fact]
    public void Right_view_mirrors_left_view()
    {
        var c = TwoParts();
        c.PoseFor(Direction.Left).Set("arm", 25);
        var compositor = new Compositor();
        var left = compositor.Compose(c, Direction.Left);
        var right = compositor.Compose(c, Direction.Right);

        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
                Assert.Equal(left.Indices[y * 16 + x], right.Indices[y * 16 + 15 - x]);
    }

    [Fact]
    public void Poses_are_per_direction_and_right_shares_left()
    {
        var c = TwoParts();
        c.PoseFor(Direction.Front).Set("arm", 30);
        Assert.Equal(0, c.PoseFor(Direction.Left).Get("arm"));
        Assert.Equal(0, c.PoseFor(Direction.Back).Get("arm"));
        Assert.Same(c.PoseFor(Direction.Left), c.PoseFor(Direction.Right));
    }

    [Fact]
    public void Drawing_in_right_view_edits_the_left_image()
    {
        var c = TwoParts();
        var arm = c.Find("arm")!;
        var doc = c.CreateDocument(arm, Direction.Right);
        doc.BeginStroke(PencilTool.Eraser, 0, 0);
        doc.EndStroke();
        Assert.Equal(Palette.TransparentIndex, arm.View(Direction.Left).Image[0, 0]);
        Assert.NotEqual(Palette.TransparentIndex, arm.View(Direction.Front).Image[0, 0]);
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
