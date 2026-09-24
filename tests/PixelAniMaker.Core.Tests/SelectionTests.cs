using PixelAniMaker.Core.Editing;
using PixelAniMaker.Core.History;
using PixelAniMaker.Core.Imaging;
using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.Core.Tests;

public class SelectionTests
{
    private static EditorDocument DocWithBlock()
    {
        var doc = EditorDocument.CreateBlank(10, 10);
        doc.BeginStroke(ShapeTool.Rectangle, 2, 2);
        doc.ContinueStroke(3, 3);                 // 2x2 block at (2,2)-(3,3)
        doc.EndStroke();
        return doc;
    }

    [Fact]
    public void Dragging_outside_selects_a_rectangle()
    {
        var doc = DocWithBlock();
        doc.BeginStroke(SelectMoveTool.Instance, 1, 1);
        doc.ContinueStroke(4, 4);
        doc.EndStroke();
        Assert.Equal(new PixelRect(1, 1, 4, 4), doc.Selection);
    }

    [Fact]
    public void Click_without_drag_clears_selection()
    {
        var doc = DocWithBlock();
        doc.Selection = new PixelRect(1, 1, 4, 4);
        doc.BeginStroke(SelectMoveTool.Instance, 8, 8);
        doc.EndStroke();
        Assert.Null(doc.Selection);
    }

    [Fact]
    public void Dragging_inside_moves_the_pixels_as_one_undo_step()
    {
        var doc = DocWithBlock();
        doc.Selection = new PixelRect(2, 2, 3, 3);
        doc.BeginStroke(SelectMoveTool.Instance, 2, 2);
        doc.ContinueStroke(4, 3);
        doc.ContinueStroke(6, 5);                 // net move (+4, +3)
        doc.EndStroke();

        Assert.Equal(0, doc.Image[2, 2]);
        Assert.Equal(1, doc.Image[6, 5]);
        Assert.Equal(1, doc.Image[7, 6]);
        Assert.Equal(new PixelRect(6, 5, 7, 6), doc.Selection);

        doc.Undo();
        Assert.Equal(1, doc.Image[2, 2]);
        Assert.Equal(0, doc.Image[6, 5]);
    }

    [Fact]
    public void Delete_clears_the_selection()
    {
        var doc = DocWithBlock();
        doc.Selection = new PixelRect(2, 2, 2, 3);
        doc.DeleteSelection();
        Assert.Equal(0, doc.Image[2, 2]);
        Assert.Equal(1, doc.Image[3, 2]);
    }

    [Fact]
    public void Selection_is_clipped_to_the_image()
    {
        var doc = DocWithBlock();
        doc.Selection = new PixelRect(-3, 8, 20, 30);
        Assert.Equal(new PixelRect(0, 8, 9, 9), doc.Selection);
    }

    [Fact]
    public void Mirror_into_another_image_is_one_undo_step()
    {
        var a = EditorDocument.CreateBlank(4, 4);
        var other = new IndexedImage(4, 4);
        a.Mirror = new PixelMirror(other, (x, y) => (3 - x, y));

        a.BeginStroke(PencilTool.Pencil, 0, 1);
        a.EndStroke();
        Assert.Equal(1, a.Image[0, 1]);
        Assert.Equal(1, other[3, 1]);

        a.Undo();
        Assert.Equal(0, a.Image[0, 1]);
        Assert.Equal(0, other[3, 1]);
        a.Redo();
        Assert.Equal(1, other[3, 1]);
    }

    [Fact]
    public void Mirrored_shape_preview_in_the_same_image_leaves_no_trace()
    {
        var doc = EditorDocument.CreateBlank(10, 3);
        doc.Mirror = new PixelMirror(doc.Image, (x, y) => (9 - x, y));
        doc.BeginStroke(ShapeTool.Line, 2, 1);
        doc.ContinueStroke(8, 1);                 // crosses the centre line
        doc.ContinueStroke(3, 1);                 // shrinks back
        doc.EndStroke();
        // line 2..3 and its mirror 6..7 only
        int[] expected = [0, 0, 1, 1, 0, 0, 1, 1, 0, 0];
        Assert.Equal(expected, Enumerable.Range(0, 10).Select(x => (int)doc.Image[x, 1]));
    }

    [Theory]
    [InlineData("upper_arm_r", "upper_arm_l")]
    [InlineData("foot_l", "foot_r")]
    [InlineData("head", "head")]
    public void Counterparts_swap_sides(string part, string expected)
    {
        var v = new PartViewSpec(0, 0, 0, 0, 0, "x");
        var views = new Dictionary<string, PartViewSpec> { ["front"] = v, ["left"] = v, ["back"] = v };
        var c = new CharacterSpec(4, 4,
            [new PartSpec("head", "", null, views), new PartSpec("upper_arm_r", "", "head", views), new PartSpec("upper_arm_l", "", "head", views),
             new PartSpec("foot_r", "", "head", views), new PartSpec("foot_l", "", "head", views)])
            .Build(_ => new RgbaImage(1, 1, [new Rgba(1, 1, 1)]));
        Assert.Equal(expected, Symmetry.Counterpart(c, c.Find(part)!).Name);
    }
}
