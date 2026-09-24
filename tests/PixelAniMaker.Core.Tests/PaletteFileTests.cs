using System.Text;
using PixelAniMaker.Core.Editing;
using PixelAniMaker.Core.History;
using PixelAniMaker.Core.Imaging;

namespace PixelAniMaker.Core.Tests;

public class PaletteFileTests
{
    private static readonly Rgba Red = new(255, 0, 0);
    private static readonly Rgba Green = new(0, 128, 0);
    private static readonly Rgba Blue = new(0, 0, 255);

    private static Palette Pal(params Rgba[] colors)
    {
        var p = new Palette();
        foreach (var c in colors)
            p.GetOrAdd(c);
        return p;
    }

    [Theory]
    [InlineData("a.gpl")]
    [InlineData("a.hex")]
    [InlineData("a.png")]
    public void Round_trips(string file)
    {
        using var ms = new MemoryStream();
        PaletteFile.Write(file, ms, [Red, Green, Blue], new RawCodec());
        ms.Position = 0;
        Assert.Equal([Red, Green, Blue], PaletteFile.Read(file, ms, new RawCodec()));
    }

    [Fact]
    public void Reads_gimp_palettes_with_names_and_comments()
    {
        const string gpl = "GIMP Palette\nName: Test\nColumns: 4\n# comment\n255   0   0\tRed\n  0 128   0 Green\n";
        Assert.Equal([Red, Green], PaletteFile.ParseGpl(gpl));
    }

    [Fact]
    public void Rejects_unknown_formats_and_bad_files()
    {
        Assert.Throws<FormatException>(() => PaletteFile.Read("a.act", new MemoryStream(), new RawCodec()));
        Assert.Throws<FormatException>(() => PaletteFile.ParseGpl("not a palette"));
        Assert.Throws<FormatException>(() => PaletteFile.ParseHex("ff0000\nzz"));
    }

    [Fact]
    public void Swap_replaces_by_index_and_keeps_extra_colours()
    {
        var p = Pal(Red, Green, Blue);
        var history = new UndoHistory();
        PaletteSwap.Swap(p, history, [new Rgba(1, 1, 1), new Rgba(2, 2, 2)]);
        Assert.Equal([Rgba.Transparent, new Rgba(1, 1, 1), new Rgba(2, 2, 2), Blue], p.Colors);
        history.Undo();
        Assert.Equal([Rgba.Transparent, Red, Green, Blue], p.Colors);
    }

    [Fact]
    public void Merge_adds_only_missing_colours_and_undo_shrinks_back()
    {
        var p = Pal(Red);
        var colors = new ColorSelection(p);
        var history = new UndoHistory();
        PaletteSwap.Merge(p, history, [Red, Green, Green, Blue]);
        Assert.Equal([Rgba.Transparent, Red, Green, Blue], p.Colors);

        colors.Primary = 3;
        history.Undo();
        Assert.Equal(2, p.Count);
        Assert.Equal(1, colors.Primary);               // selection moved back into range
    }

    [Fact]
    public void Preview_does_not_touch_the_palette()
    {
        var p = Pal(Red, Green);
        var preview = PaletteSwap.Preview(p, [Blue]);
        Assert.Equal(Blue, preview[1]);
        Assert.Equal(Red, p[1]);
    }
}
