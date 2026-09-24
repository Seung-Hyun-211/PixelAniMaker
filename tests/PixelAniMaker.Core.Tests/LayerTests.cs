using PixelAniMaker.Core.History;
using PixelAniMaker.Core.Imaging;
using PixelAniMaker.Core.Project;
using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.Core.Tests;

public class LayerTests
{
    private static readonly Rgba Skin = new(230, 200, 170);

    /// <summary>16x16 canvas, one 2x4 skin-coloured part at (4,4).</summary>
    private static Character OnePart()
    {
        var v = new PartViewSpec(4, 4, 5, 4, 0, "a");
        return new CharacterSpec(16, 16, [new PartSpec("arm", "arm", null,
                new Dictionary<string, PartViewSpec> { ["front"] = v, ["left"] = v, ["back"] = v })])
            .Build(_ => new RgbaImage(2, 4, Enumerable.Repeat(Skin, 8).ToArray()));
    }

    private static PartView Front(Character c) => c.Find("arm")!.View(Direction.Front);

    /// <summary>Adds a "clothes" layer with one pixel of <paramref name="index"/> at (0, 1).</summary>
    private static PartLayer AddClothes(Character c, UndoHistory history, int index)
    {
        var layers = Front(c).Layers;
        var clothes = layers.CreateLayer("옷");
        LayerChange.Apply(layers, history, "레이어 추가", list => list.Add(clothes));
        clothes.Image.Set(0, 1, index);
        return clothes;
    }

    [Fact]
    public void Single_layer_is_drawn_directly()
    {
        var view = Front(OnePart());
        Assert.Same(view.Layers[0].Image, view.Image);
    }

    [Fact]
    public void Upper_layers_cover_lower_ones_and_hidden_layers_are_skipped()
    {
        var c = OnePart();
        int blue = c.Palette.GetOrAdd(new Rgba(0, 0, 255));
        int skin = Front(c).Layers[0].Image[0, 1];
        var clothes = AddClothes(c, new UndoHistory(), blue);

        var view = Front(c);
        Assert.Equal(blue, view.Image[0, 1]);
        Assert.Equal(skin, view.Image[1, 1]);          // transparent clothes pixel shows the skin

        clothes.Image.Set(1, 1, blue);                 // later edits reach the flattened image
        Assert.Equal(blue, view.Image[1, 1]);

        clothes.Visible = false;
        Assert.Equal(skin, view.Image[0, 1]);
    }

    [Fact]
    public void Layer_edits_are_undoable_and_keep_at_least_one_layer()
    {
        var c = OnePart();
        var history = new UndoHistory();
        var layers = Front(c).Layers;
        AddClothes(c, history, 1);
        LayerChange.Apply(layers, history, "순서", list => list.Reverse());
        Assert.Equal(["옷", "기본"], layers.All.Select(l => l.Name));

        history.Undo();
        Assert.Equal(["기본", "옷"], layers.All.Select(l => l.Name));
        history.Undo();
        Assert.Single(layers.All);

        LayerChange.Apply(layers, history, "삭제", list => list.Clear());
        Assert.Single(layers.All);                     // refused
        Assert.Equal("기본 2", layers.FreeName("기본"));
    }

    [Fact]
    public void Composite_uses_the_flattened_layers()
    {
        var c = OnePart();
        c.Outline.Enabled = false;
        int blue = c.Palette.GetOrAdd(new Rgba(0, 0, 255));
        AddClothes(c, new UndoHistory(), blue);
        var result = new Compositor().Compose(c, Direction.Front);
        Assert.Equal(blue, result.Indices[5 * 16 + 4]);
    }

    [Fact]
    public void Layers_survive_save_and_load()
    {
        var c = OnePart();
        int blue = c.Palette.GetOrAdd(new Rgba(0, 0, 255));
        var clothes = AddClothes(c, new UndoHistory(), blue);
        clothes.Visible = false;

        using var ms = new MemoryStream();
        ProjectFile.Save(new ProjectData(c, []), ms, new RawCodec());
        ms.Position = 0;
        var loaded = ProjectFile.Load(ms, new RawCodec()).Character;

        var layers = Front(loaded).Layers;
        Assert.Equal(["기본", "옷"], layers.All.Select(l => l.Name));
        Assert.False(layers[1].Visible);
        Assert.Equal(new Rgba(0, 0, 255), loaded.Palette[layers[1].Image[0, 1]]);
        Assert.Single(loaded.Find("arm")!.View(Direction.Back).Layers.All);
    }

    [Fact]
    public void Separate_right_view_copies_the_layers()
    {
        var c = OnePart();
        var left = c.Find("arm")!.View(Direction.Left).Layers;
        LayerChange.Apply(left, new UndoHistory(), "추가", list => list.Add(left.CreateLayer("머리카락")));
        RightViewChange.Apply(c.Find("arm")!, new UndoHistory(), separate: true);
        var right = c.Find("arm")!.View(Direction.Right).Layers;
        Assert.Equal(["기본", "머리카락"], right.All.Select(l => l.Name));
        Assert.NotSame(left[1].Image, right[1].Image);
    }

    [Fact]
    public void Edit_target_is_the_chosen_layer_unless_a_variant_is_drawn()
    {
        var c = OnePart();
        var clothes = AddClothes(c, new UndoHistory(), 1);
        var t = c.ComputeTransforms(Direction.Front)[c.Find("arm")!];
        Assert.Same(clothes.Image, t.EditImage(1));
        Assert.Same(clothes.Image, t.EditImage(7));    // clamped

        var view = Front(c);
        view.Variants.Set(90, VariantChange.RenderFromBase(view, 90));
        c.PoseFor(Direction.Front).Set("arm", 90);
        var turned = c.ComputeTransforms(Direction.Front)[c.Find("arm")!];
        Assert.Same(view.Variants.Get(90)!.Image, turned.EditImage(1));
    }
}
