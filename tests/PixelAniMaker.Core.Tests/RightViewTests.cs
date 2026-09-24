using PixelAniMaker.Core.History;
using PixelAniMaker.Core.Imaging;
using PixelAniMaker.Core.Project;
using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.Core.Tests;

public class RightViewTests
{
    private static readonly Rgba Red = new(255, 0, 0);

    /// <summary>16x16 canvas, one 2x4 red part at (4,4) hanging from its joint (5,4).</summary>
    private static Character OnePart()
    {
        var v = new PartViewSpec(4, 4, 5, 4, 0, "a");
        return new CharacterSpec(16, 16, [new PartSpec("a", "a", null,
                new Dictionary<string, PartViewSpec> { ["front"] = v, ["left"] = v, ["back"] = v })])
            .Build(_ => new RgbaImage(2, 4, Enumerable.Repeat(Red, 8).ToArray()));
    }

    [Fact]
    public void Right_mirrors_left_until_separated()
    {
        var c = OnePart();
        var part = c.Find("a")!;
        Assert.False(part.HasOwnRight);
        Assert.Same(part.View(Direction.Left), part.View(Direction.Right));

        var history = new UndoHistory();
        RightViewChange.Apply(part, history, separate: true);
        Assert.True(part.HasOwnRight);
        var right = part.View(Direction.Right);
        Assert.NotSame(part.View(Direction.Left).Image, right.Image);
        Assert.Equal(part.View(Direction.Left).RestPivot, right.RestPivot);

        history.Undo();
        Assert.False(part.HasOwnRight);
        history.Redo();
        Assert.Same(right, part.View(Direction.Right));
    }

    [Fact]
    public void Drawing_on_own_right_changes_only_the_right_composite()
    {
        var c = OnePart();
        var part = c.Find("a")!;
        RightViewChange.Apply(part, new UndoHistory(), separate: true);
        int blue = c.Palette.GetOrAdd(new Rgba(0, 0, 255));
        part.View(Direction.Right).Image.Set(0, 0, blue);

        var compositor = new Compositor();
        c.Outline.Enabled = false;
        var left = compositor.Compose(c, Direction.Left);
        var right = compositor.Compose(c, Direction.Right);
        Assert.NotEqual(blue, left.Indices[4 * 16 + 4]);
        Assert.Equal(blue, right.Indices[4 * 16 + (16 - 1 - 4)]);   // mirrored for display
    }

    [Fact]
    public void Own_right_view_survives_save_and_load()
    {
        var c = OnePart();
        var part = c.Find("a")!;
        RightViewChange.Apply(part, new UndoHistory(), separate: true);
        int blue = c.Palette.GetOrAdd(new Rgba(0, 0, 255));
        part.View(Direction.Right).Image.Set(1, 2, blue);

        using var ms = new MemoryStream();
        ProjectFile.Save(new ProjectData(c, []), ms, new RawCodec());
        ms.Position = 0;
        var loaded = ProjectFile.Load(ms, new RawCodec()).Character;

        var lp = loaded.Find("a")!;
        Assert.True(lp.HasOwnRight);
        Assert.Equal(new Rgba(0, 0, 255), loaded.Palette[lp.View(Direction.Right).Image[1, 2]]);
        Assert.Equal(Red, loaded.Palette[lp.View(Direction.Left).Image[1, 2]]);
    }

    [Fact]
    public void Mirrored_parts_save_no_right_images()
    {
        var images = CharacterSpec.Images(OnePart()).Select(i => i.Path).ToList();
        Assert.DoesNotContain(images, p => p.StartsWith("right/"));
    }
}
