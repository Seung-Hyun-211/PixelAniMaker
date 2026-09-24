using PixelAniMaker.Core.Editing;
using PixelAniMaker.Core.History;
using PixelAniMaker.Core.Imaging;

namespace PixelAniMaker.Core.Tests;

public class EditToolTests
{
    private static EditorDocument Doc(int w = 16, int h = 16) => EditorDocument.CreateBlank(w, h);

    private static int Painted(EditorDocument d) => d.Image.Pixels.ToArray().Count(p => p != 0);

    [Fact]
    public void Pixel_perfect_removes_l_corners()
    {
        var doc = Doc();
        // a staircase drawn as right, down, right, down ... produces L corners
        doc.BeginStroke(PencilTool.PixelPerfectPencil, 0, 0);
        foreach (var (x, y) in new[] { (1, 0), (1, 1), (2, 1), (2, 2), (3, 2) })
            doc.ContinueStroke(x, y);
        doc.EndStroke();
        // corners (1,0) and (2,1) removed: a clean diagonal remains
        Assert.Equal(0, doc.Image[1, 0]);
        Assert.Equal(0, doc.Image[2, 1]);
        Assert.Equal(1, doc.Image[1, 1]);
        Assert.Equal(1, doc.Image[3, 2]);
    }

    [Fact]
    public void Pixel_perfect_keeps_pixels_that_were_already_there()
    {
        var doc = Doc();
        doc.Colors.Primary = 2;
        doc.BeginStroke(PencilTool.Pencil, 1, 0);   // an existing white pixel under the corner
        doc.EndStroke();
        doc.Colors.Primary = 1;
        doc.BeginStroke(PencilTool.PixelPerfectPencil, 0, 0);
        doc.ContinueStroke(1, 0);
        doc.ContinueStroke(1, 1);
        doc.EndStroke();
        Assert.Equal(2, doc.Image[1, 0]);             // restored, not erased
    }

    [Fact]
    public void Line_tool_shows_only_the_latest_preview()
    {
        var doc = Doc();
        doc.BeginStroke(ShapeTool.Line, 0, 0);
        doc.ContinueStroke(10, 0);
        doc.ContinueStroke(0, 10);
        doc.EndStroke();
        Assert.Equal(0, doc.Image[10, 0]);
        Assert.Equal(1, doc.Image[0, 10]);
        Assert.Equal(11, Painted(doc));
        doc.Undo();
        Assert.Equal(0, Painted(doc));
    }

    [Fact]
    public void Rectangle_is_an_outline()
    {
        var doc = Doc();
        doc.BeginStroke(ShapeTool.Rectangle, 2, 3);
        doc.ContinueStroke(6, 8);
        doc.EndStroke();
        Assert.Equal(2 * 5 + 2 * 4, Painted(doc));   // 5 wide, 6 tall -> 18 border pixels
        Assert.Equal(0, doc.Image[4, 5]);
    }

    [Theory]
    [InlineData(0, 0, 10, 6)]
    [InlineData(3, 2, 12, 12)]
    [InlineData(5, 5, 6, 7)]
    public void Ellipse_is_symmetric_closed_and_inside_its_box(int x0, int y0, int x1, int y1)
    {
        var pts = Raster.Ellipse(x0, y0, x1, y1).ToHashSet();
        Assert.All(pts, p => Assert.InRange(p.X, x0, x1));
        Assert.All(pts, p => Assert.InRange(p.Y, y0, y1));
        Assert.Contains(pts, p => p.X == x0);
        Assert.Contains(pts, p => p.X == x1);
        Assert.Contains(pts, p => p.Y == y0);
        Assert.Contains(pts, p => p.Y == y1);
        Assert.All(pts, p => Assert.Contains((x0 + x1 - p.X, p.Y), pts));   // mirror left/right
        Assert.All(pts, p => Assert.Contains((p.X, y0 + y1 - p.Y), pts));   // mirror up/down
        // closed: every point has an 8-neighbour on the outline
        Assert.All(pts, p => Assert.Contains(pts, q => q != p && Math.Abs(q.X - p.X) <= 1 && Math.Abs(q.Y - p.Y) <= 1));
    }

    [Fact]
    public void Palette_replacement_is_undoable()
    {
        var palette = new Palette();
        int i = palette.GetOrAdd(new Rgba(1, 2, 3));
        var history = new UndoHistory();
        PaletteChange.Apply(palette, history, i, new Rgba(9, 9, 9));
        Assert.Equal(new Rgba(9, 9, 9), palette[i]);
        history.Undo();
        Assert.Equal(new Rgba(1, 2, 3), palette[i]);
    }
}
