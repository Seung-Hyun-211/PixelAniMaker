using PixelAniMaker.Core.Animation;
using PixelAniMaker.Core.History;
using PixelAniMaker.Core.Imaging;
using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.Core.Tests;

public class TouchupTests
{
    private static Character Solid()
    {
        var v = new PartViewSpec(2, 2, 4, 4, 0, "a");
        return new CharacterSpec(8, 8, [new PartSpec("a", "a", null,
                new Dictionary<string, PartViewSpec> { ["front"] = v, ["left"] = v, ["back"] = v })])
            .Build(_ => new RgbaImage(4, 4, Enumerable.Repeat(new Rgba(200, 0, 0), 16).ToArray()));
    }

    private static PixelOverrides Pixels(params (int X, int Y, ushort V)[] p) =>
        new(p.Select(x => KeyValuePair.Create((x.X, x.Y), x.V)));

    [Fact]
    public void Baked_frames_include_touchups_only_for_their_frame_and_direction()
    {
        var c = Solid();
        var clip = new AnimationClip("a", 2);
        int blue = c.Palette.GetOrAdd(new Rgba(0, 0, 255));
        clip.Touchups.Set(Direction.Right, 1, Pixels((0, 0, (ushort)blue), (3, 3, 0)));

        var baked = SpriteBaker.Bake(c, clip, new Compositor());
        Assert.Equal(blue, baked[Direction.Right][1].Indices[0]);
        Assert.Equal(0, baked[Direction.Right][1].Indices[3 * 8 + 3]);     // erased
        Assert.Equal(0, baked[Direction.Right][0].Indices[0]);              // other frame untouched
        Assert.NotEqual(0, baked[Direction.Left][1].Indices[3 * 8 + 3]);    // other direction untouched

        var raw = SpriteBaker.Bake(c, clip, new Compositor(), touchups: false);
        Assert.Equal(0, raw[Direction.Right][1].Indices[0]);
    }

    [Fact]
    public void Diff_records_only_changed_pixels()
    {
        var c = Solid();
        var generated = new Compositor().Compose(c, Direction.Front);
        var edited = new IndexedImage(8, 8);
        for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
                edited.Set(x, y, generated.Indices[y * 8 + x]);
        edited.Set(0, 0, 5);
        edited.Set(2, 2, 0);

        var diff = FrameTouchups.Diff(generated, edited);
        Assert.Equal(2, diff.Count);
        Assert.Equal(5, diff[(0, 0)]);
        Assert.Equal(0, diff[(2, 2)]);
    }

    [Fact]
    public void Touchup_changes_are_undoable()
    {
        var touchups = new FrameTouchups();
        var history = new UndoHistory();
        TouchupChange.Apply(touchups, history, Direction.Front, 0, Pixels((1, 1, 3)));
        TouchupChange.Apply(touchups, history, Direction.Front, 0, []);
        Assert.False(touchups.Has(Direction.Front, 0));

        history.Undo();
        Assert.Equal(3, touchups.Get(Direction.Front, 0)[(1, 1)]);
        history.Undo();
        Assert.False(touchups.Has(Direction.Front, 0));
    }

    [Fact]
    public void Touchups_round_trip_through_json()
    {
        var clip = new AnimationClip("a", 3);
        clip.Touchups.Set(Direction.Back, 2, Pixels((4, 5, 7), (6, 1, 0)));
        var loaded = Assert.Single(AnimationJson.Parse(AnimationJson.Serialize([clip])));
        Assert.True(loaded.Touchups.Get(Direction.Back, 2).SameAs(clip.Touchups.Get(Direction.Back, 2)));
        Assert.Single(loaded.Touchups.Frames);
    }
}
