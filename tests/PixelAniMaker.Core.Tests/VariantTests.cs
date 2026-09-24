using System.Numerics;
using PixelAniMaker.Core.History;
using PixelAniMaker.Core.Imaging;
using PixelAniMaker.Core.Project;
using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.Core.Tests;

public class VariantTests
{
    private static readonly Rgba Red = new(255, 0, 0);
    private static readonly Rgba Green = new(0, 200, 0);

    /// <summary>16x16 canvas, one 2x4 red part at (7,4) hanging from its joint (8,4).</summary>
    private static Character OnePart()
    {
        var v = new PartViewSpec(7, 4, 8, 4, 0, "a");
        return new CharacterSpec(16, 16, [new PartSpec("a", "a", null,
                new Dictionary<string, PartViewSpec> { ["front"] = v, ["left"] = v, ["back"] = v })])
            .Build(_ => new RgbaImage(2, 4, Enumerable.Repeat(Red, 8).ToArray()));
    }

    [Theory]
    [InlineData(80, 90)]
    [InlineData(100, 90)]
    [InlineData(60, null)]      // too far from 90 -> base image
    [InlineData(10, null)]      // closer to the base (0°)
    [InlineData(-170, 180)]     // wraps around
    public void Picks_the_nearest_variant_within_reach(double degrees, int? expected)
    {
        var variants = new AngleVariants();
        var dummy = new PartVariant(new IndexedImage(1, 1), Vector2.Zero);
        variants.Set(90, dummy);
        variants.Set(180, dummy);
        Assert.Equal(expected, variants.Pick(degrees));
    }

    [Fact]
    public void Transform_uses_variant_with_remaining_angle()
    {
        var c = OnePart();
        var view = c.Find("a")!.View(Direction.Front);
        var variant = VariantChange.RenderFromBase(view, 90);
        view.Variants.Set(90, variant);

        c.PoseFor(Direction.Front).Set("a", 100);
        var t = c.ComputeTransforms(Direction.Front)[c.Find("a")!];
        Assert.Same(variant.Image, t.Image);
        Assert.Equal(10 * MathF.PI / 180, t.ImageAngle, 4);
        Assert.Equal(100 * MathF.PI / 180, t.Angle, 4);
    }

    [Fact]
    public void Rotated_render_turns_the_image_and_moves_the_pivot()
    {
        var image = new IndexedImage(2, 4);
        for (int y = 0; y < 4; y++) { image.Set(0, y, 1); image.Set(1, y, 1); }
        var (rotated, pivot) = RotSprite.Rotate(image, new Vector2(1, 0), MathF.PI / 2);
        Assert.Equal((4, 2), (rotated.Width, rotated.Height));
        Assert.Equal(new Vector2(4, 1), pivot);           // clockwise: the part now points left of the joint
        Assert.All(rotated.Pixels.ToArray(), p => Assert.Equal(1, p));
    }

    [Fact]
    public void Painted_variant_shows_at_its_angle()
    {
        var c = OnePart();
        var view = c.Find("a")!.View(Direction.Front);
        var variant = VariantChange.RenderFromBase(view, 90);
        int green = c.Palette.GetOrAdd(Green);
        for (int y = 0; y < variant.Image.Height; y++)
            for (int x = 0; x < variant.Image.Width; x++)
                variant.Image.Set(x, y, green);
        view.Variants.Set(90, variant);

        c.PoseFor(Direction.Front).Set("a", 90);
        var at90 = new Compositor().Compose(c, Direction.Front);
        Assert.Contains(at90.Indices, i => i == green);
        Assert.DoesNotContain(at90.Indices, i => i == c.Palette.IndexOf(Red));

        c.PoseFor(Direction.Front).Set("a", 0);
        Assert.DoesNotContain(new Compositor().Compose(c, Direction.Front).Indices, i => i == green);
    }

    [Fact]
    public void Variant_changes_are_undoable()
    {
        var c = OnePart();
        var view = c.Find("a")!.View(Direction.Front);
        var history = new UndoHistory();
        VariantChange.Apply(view.Variants, history, 45, VariantChange.RenderFromBase(view, 45));
        Assert.NotNull(view.Variants.Get(45));
        history.Undo();
        Assert.Null(view.Variants.Get(45));
        history.Redo();
        Assert.NotNull(view.Variants.Get(45));
    }

    [Fact]
    public void Variants_round_trip_through_project_files()
    {
        var c = OnePart();
        var view = c.Find("a")!.View(Direction.Left);
        var variant = VariantChange.RenderFromBase(view, -90);
        view.Variants.Set(-90, variant);

        using var ms = new MemoryStream();
        ProjectFile.Save(new ProjectData(c, []), ms, new RawCodec());
        ms.Position = 0;
        var loaded = ProjectFile.Load(ms, new RawCodec()).Character.Find("a")!.View(Direction.Left).Variants.Get(-90);

        Assert.NotNull(loaded);
        Assert.Equal(variant.LocalPivot, loaded!.LocalPivot);
        Assert.Equal(variant.Image.Pixels.ToArray(), loaded.Image.Pixels.ToArray());
    }
}
