using System.Numerics;
using PixelAniMaker.Core.Editing;
using PixelAniMaker.Core.History;
using PixelAniMaker.Core.Imaging;
using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.Core.Tests;

public class PartViewBoundsTests
{
    private static readonly Rgba Skin = new(230, 200, 170);

    /// <summary>32x32 canvas, one 4x4 part at (10,10) with its joint at (12,10).</summary>
    private static Character OnePart()
    {
        var v = new PartViewSpec(10, 10, 12, 10, 0, "a");
        return new CharacterSpec(32, 32, [new PartSpec("head", "head", null,
                new Dictionary<string, PartViewSpec> { ["front"] = v, ["left"] = v, ["back"] = v })])
            .Build(_ => new RgbaImage(4, 4, Enumerable.Repeat(Skin, 16).ToArray()));
    }

    private static PartView Front(Character c) => c.Find("head")!.View(Direction.Front);

    private static PartTransform Transform(Character c) => c.ComputeTransforms(Direction.Front)[c.Find("head")!];

    private static HashSet<(int X, int Y, int Index)> OnCanvas(Character c) => [.. Front(c).DrawnPixels()];

    /// <summary>Grows to the canvas, draws a pixel at a canvas point outside the part, trims — like the app's stroke.</summary>
    private static void DrawOutside(Character c, int canvasX, int canvasY)
    {
        var view = Front(c);
        var history = c.History;
        history.BeginGroup("연필");
        var original = PartViewBounds.Current(view);
        var grow = PartViewBounds.CoveringCanvas(Transform(c), c.Width, c.Height);
        PartViewBounds.Resize(view, grow, history);

        var (lx, ly) = Transform(c).ToLocalPixel(canvasX, canvasY);
        var doc = c.CreateDocument(view.Layers[0].Image);
        doc.Colors.Primary = 1;
        doc.BeginStroke(PencilTool.Pencil, lx, ly, false);
        doc.EndStroke();

        var keep = original.Offset(-grow.X0, -grow.Y0);
        PartViewBounds.Resize(view, PartViewBounds.Trimmed(view, keep), history);
        history.EndGroup();
    }

    [Fact]
    public void Drawing_outside_the_part_grows_it_just_enough_and_keeps_it_in_place()
    {
        var c = OnePart();
        var before = OnCanvas(c);
        DrawOutside(c, 20, 25);

        var view = Front(c);
        Assert.Contains((20, 25, 1), OnCanvas(c));
        Assert.True(before.IsSubsetOf(OnCanvas(c)));                     // the old drawing did not move
        Assert.Equal(new Vector2(10, 10), view.RestPosition);             // grown only right and down
        Assert.Equal((20 + 1 + PartViewBounds.Padding - 10, 25 + 1 + PartViewBounds.Padding - 10),
            (view.Layers[0].Image.Width, view.Layers[0].Image.Height));
        Assert.Equal(new Vector2(12, 10), view.RestPivot);                // the joint stays
    }

    [Fact]
    public void Growing_up_and_left_moves_the_image_origin_and_attachment_points_with_it()
    {
        var c = OnePart();
        Front(c).Attachments.Set("grip", new Vector2(1, 1));              // canvas (11, 11)
        DrawOutside(c, 3, 2);

        var view = Front(c);
        Assert.Contains((3, 2, 1), OnCanvas(c));
        Assert.Equal(new Vector2(3 - PartViewBounds.Padding, 2 - PartViewBounds.Padding), view.RestPosition);
        Assert.Equal(new Vector2(11, 11), view.RestPosition + view.Attachments.All["grip"]);
    }

    [Fact]
    public void The_whole_stroke_is_one_undo_step()
    {
        var c = OnePart();
        var before = OnCanvas(c);
        DrawOutside(c, 20, 25);

        Assert.Single(c.History.Done);
        c.History.Undo();
        Assert.Equal(before, OnCanvas(c));
        Assert.Equal((4, 4), (Front(c).Layers[0].Image.Width, Front(c).Layers[0].Image.Height));
        c.History.Redo();
        Assert.Contains((20, 25, 1), OnCanvas(c));
    }

    [Fact]
    public void Drawing_inside_leaves_the_size_unchanged()
    {
        var c = OnePart();
        DrawOutside(c, 11, 11);
        Assert.Equal(new PixelRect(0, 0, 3, 3), PartViewBounds.Current(Front(c)));
        Assert.Equal(new Vector2(10, 10), Front(c).RestPosition);
    }

    [Fact]
    public void Every_layer_is_resized_together()
    {
        var c = OnePart();
        var layers = Front(c).Layers;
        LayerChange.Apply(layers, c.History, "추가", list => list.Add(layers.CreateLayer("옷")));
        DrawOutside(c, 20, 25);
        Assert.Equal(layers[0].Image.Width, layers[1].Image.Width);
        Assert.Equal(layers[0].Image.Height, layers[1].Image.Height);
    }

    [Fact]
    public void A_cancelled_group_leaves_nothing_behind()
    {
        var c = OnePart();
        c.History.BeginGroup("연필");
        PartViewBounds.Resize(Front(c), PartViewBounds.CoveringCanvas(Transform(c), c.Width, c.Height), c.History);
        c.History.CancelGroup();
        Assert.Empty(c.History.Done);
        Assert.Equal(new PixelRect(0, 0, 3, 3), PartViewBounds.Current(Front(c)));
    }

    [Fact]
    public void A_rotated_part_covers_the_turned_canvas()
    {
        var c = OnePart();
        c.PoseFor(Direction.Front).Set("head", 45);
        var cover = PartViewBounds.CoveringCanvas(Transform(c), c.Width, c.Height);
        foreach (var (x, y) in new[] { (0, 0), (31, 0), (0, 31), (31, 31) })
        {
            var (lx, ly) = Transform(c).ToLocalPixel(x, y);
            Assert.True(cover.Contains(lx, ly), $"canvas ({x},{y}) -> ({lx},{ly}) not in {cover}");
        }
    }
}
