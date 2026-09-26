using PixelAniMaker.Core.Animation;
using PixelAniMaker.Core.Project;
using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.Core.Tests;

public class CanvasResizeTests
{
    private static readonly CanvasMargins Uneven = new(5, 7, 11, 3);

    /// <summary>Checks that <paramref name="after"/> is <paramref name="before"/> moved by (dx, dy), nothing else drawn.</summary>
    private static void AssertMoved(CompositeResult before, CompositeResult after, int dx, int dy)
    {
        int drawn = 0;
        for (int y = 0; y < after.Height; y++)
            for (int x = 0; x < after.Width; x++)
            {
                int sx = x - dx, sy = y - dy;
                ushort expected = sx >= 0 && sy >= 0 && sx < before.Width && sy < before.Height ? before.Indices[sy * before.Width + sx] : (ushort)0;
                Assert.Equal(expected, after.Indices[y * after.Width + x]);
                if (expected != 0)
                    drawn++;
            }
        Assert.True(drawn > 0);
    }

    [Fact]
    public void Adding_margins_grows_the_canvas_and_moves_the_drawing_by_the_left_or_mirrored_right_margin()
    {
        var c = BustPartTests.Body();
        var compositor = new Compositor();
        var before = DirectionExtensions.Every.ToDictionary(d => d, d => compositor.Compose(c, d));

        Assert.True(CanvasResize.Apply(c, [], Uneven, c.History));
        Assert.Equal((96 + 16, 128 + 10), (c.Width, c.Height));
        foreach (var d in DirectionExtensions.Every)
            AssertMoved(before[d], compositor.Compose(c, d), d.IsMirrored() ? Uneven.Right : Uneven.Left, Uneven.Top);

        c.History.Undo();
        Assert.Equal((96, 128), (c.Width, c.Height));
        foreach (var d in DirectionExtensions.Every)
            Assert.Equal(before[d].Indices, compositor.Compose(c, d).Indices);
    }

    [Fact]
    public void Removing_the_same_margins_brings_everything_back_and_nothing_is_cropped()
    {
        var c = BustPartTests.Body();
        var compositor = new Compositor();
        var before = compositor.Compose(c, Direction.Front);
        CanvasResize.Apply(c, [], new CanvasMargins(-20, 0, 0, 0), c.History);    // cuts into the drawing on the canvas
        CanvasResize.Apply(c, [], new CanvasMargins(20, 0, 0, 0), c.History);     // … but the part images kept every pixel
        Assert.Equal(before.Indices, compositor.Compose(c, Direction.Front).Indices);
    }

    [Fact]
    public void Touchups_move_with_the_frames()
    {
        var c = BustPartTests.Body();
        var clip = new AnimationClip("idle", 2);
        clip.Touchups.Set(Direction.Front, 0, new PixelOverrides { [(10, 20)] = 3 });
        clip.Touchups.Set(Direction.Right, 1, new PixelOverrides { [(10, 20)] = 4 });

        CanvasResize.Apply(c, [clip], Uneven, c.History);
        Assert.Equal(3, clip.Touchups.Get(Direction.Front, 0)[(15, 27)]);
        Assert.Equal(4, clip.Touchups.Get(Direction.Right, 1)[(21, 27)]);
        c.History.Undo();
        Assert.Equal(3, clip.Touchups.Get(Direction.Front, 0)[(10, 20)]);
    }

    [Fact]
    public void Sizes_out_of_range_or_no_change_are_refused()
    {
        var c = BustPartTests.Body();
        Assert.False(CanvasResize.Apply(c, [], new CanvasMargins(0, 0, 0, 0), c.History));
        Assert.False(CanvasResize.Apply(c, [], new CanvasMargins(-50, 0, -50, 0), c.History));
        Assert.False(CanvasResize.Apply(c, [], new CanvasMargins(0, 1000, 0, 0), c.History));
        Assert.False(c.History.CanUndo);
    }

    [Fact]
    public void The_new_size_is_saved_and_an_unrecorded_resize_leaves_no_undo_step()
    {
        var c = BustPartTests.Body();
        CanvasResize.ApplyUnrecorded(c, CanvasMargins.Default);
        Assert.False(c.History.CanUndo);
        Assert.Equal((128, 160), (c.Width, c.Height));

        using var ms = new MemoryStream();
        ProjectFile.Save(new ProjectData(c, []), ms, new RawCodec());
        ms.Position = 0;
        var loaded = ProjectFile.Load(ms, new RawCodec()).Character;
        Assert.Equal((128, 160), (loaded.Width, loaded.Height));
        var compositor = new Compositor();
        Assert.Equal(compositor.Compose(c, Direction.Left).Indices, compositor.Compose(loaded, Direction.Left).Indices);
    }
}
