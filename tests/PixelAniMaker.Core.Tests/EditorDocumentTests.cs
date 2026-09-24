using PixelAniMaker.Core.Editing;
using PixelAniMaker.Core.Imaging;

namespace PixelAniMaker.Core.Tests;

public class EditorDocumentTests
{
    private static EditorDocument Doc(int w = 8, int h = 8) => EditorDocument.CreateBlank(w, h);

    [Fact]
    public void Pencil_drag_draws_continuous_line()
    {
        var doc = Doc();
        doc.BeginStroke(ToolKind.Pencil, 0, 0);
        doc.ContinueStroke(7, 3);
        doc.EndStroke();

        foreach (var (x, y) in EditorDocument.Line(0, 0, 7, 3))
            Assert.Equal(doc.PrimaryIndex, doc.Image[x, y]);
        Assert.Single(doc.History.Done);
    }

    [Fact]
    public void Right_button_uses_secondary_colour()
    {
        var doc = Doc();
        doc.SecondaryIndex = 2;
        doc.BeginStroke(ToolKind.Pencil, 1, 1, secondary: true);
        doc.EndStroke();
        Assert.Equal(2, doc.Image[1, 1]);
    }

    [Fact]
    public void Eraser_sets_transparent()
    {
        var doc = Doc();
        doc.BeginStroke(ToolKind.Pencil, 2, 2);
        doc.EndStroke();
        doc.BeginStroke(ToolKind.Eraser, 2, 2);
        doc.EndStroke();
        Assert.Equal(Palette.TransparentIndex, doc.Image[2, 2]);
    }

    [Fact]
    public void Fill_stops_at_boundary()
    {
        var doc = Doc();
        // vertical wall at x = 3
        doc.BeginStroke(ToolKind.Pencil, 3, 0);
        doc.ContinueStroke(3, 7);
        doc.EndStroke();

        doc.PrimaryIndex = 2;
        doc.BeginStroke(ToolKind.Fill, 0, 0);

        Assert.Equal(2, doc.Image[0, 7]);
        Assert.Equal(2, doc.Image[2, 4]);
        Assert.Equal(1, doc.Image[3, 4]);
        Assert.Equal(Palette.TransparentIndex, doc.Image[4, 4]);
    }

    [Fact]
    public void Undo_and_redo_restore_pixels()
    {
        var doc = Doc();
        doc.BeginStroke(ToolKind.Pencil, 1, 1);
        doc.ContinueStroke(4, 1);
        doc.EndStroke();

        doc.Undo();
        Assert.All(Enumerable.Range(1, 4), x => Assert.Equal(0, doc.Image[x, 1]));
        Assert.False(doc.History.CanUndo);

        doc.Redo();
        Assert.All(Enumerable.Range(1, 4), x => Assert.Equal(1, doc.Image[x, 1]));
    }

    [Fact]
    public void Stroke_that_revisits_a_pixel_undoes_to_original()
    {
        var doc = Doc();
        doc.BeginStroke(ToolKind.Pencil, 0, 0);
        doc.ContinueStroke(3, 0);
        doc.ContinueStroke(0, 0);
        doc.EndStroke();
        doc.Undo();
        Assert.Equal(0, doc.Image[0, 0]);
    }

    [Fact]
    public void Empty_stroke_is_not_recorded()
    {
        var doc = Doc();
        doc.BeginStroke(ToolKind.Eraser, 0, 0); // already transparent
        doc.EndStroke();
        Assert.False(doc.History.CanUndo);
    }

    [Fact]
    public void Eyedropper_selects_colour_under_cursor()
    {
        var doc = Doc();
        doc.PrimaryIndex = 2;
        doc.BeginStroke(ToolKind.Pencil, 5, 5);
        doc.EndStroke();
        doc.PrimaryIndex = 1;

        doc.BeginStroke(ToolKind.Eyedropper, 5, 5);
        doc.EndStroke();
        Assert.Equal(2, doc.PrimaryIndex);
    }

    [Fact]
    public void Dirty_flag_follows_save_point()
    {
        var doc = Doc();
        Assert.False(doc.History.IsDirty);
        doc.BeginStroke(ToolKind.Pencil, 0, 0);
        doc.EndStroke();
        Assert.True(doc.History.IsDirty);
        doc.History.MarkSaved();
        Assert.False(doc.History.IsDirty);
        doc.Undo();
        Assert.True(doc.History.IsDirty);
        doc.Redo();
        Assert.False(doc.History.IsDirty);
    }

    [Fact]
    public void Out_of_bounds_input_is_ignored()
    {
        var doc = Doc(4, 4);
        doc.BeginStroke(ToolKind.Pencil, -5, -5);
        doc.ContinueStroke(10, 10);
        doc.EndStroke();
        Assert.Equal(1, doc.Image[0, 0]);
        Assert.Equal(1, doc.Image[3, 3]);
    }
}

public class ImagingTests
{
    [Fact]
    public void FromRgba_builds_palette_without_duplicates()
    {
        var red = new Rgba(255, 0, 0);
        var blue = new Rgba(0, 0, 255);
        var palette = new Palette();
        var image = IndexedImage.FromRgba(2, 2, [red, blue, Rgba.Transparent, red], palette);

        Assert.Equal(3, palette.Count);
        Assert.Equal(image[0, 0], image[1, 1]);
        Assert.Equal(Palette.TransparentIndex, image[0, 1]);
    }

    [Fact]
    public void Bgra_round_trip()
    {
        var c = new Rgba(10, 20, 30, 40);
        Assert.Equal(c, Rgba.FromBgra32(c.ToBgra32()));
    }

    [Fact]
    public void Palette_transparent_entry_is_fixed()
    {
        var palette = new Palette();
        Assert.Throws<ArgumentOutOfRangeException>(() => palette.Set(0, new Rgba(1, 2, 3)));
    }
}
