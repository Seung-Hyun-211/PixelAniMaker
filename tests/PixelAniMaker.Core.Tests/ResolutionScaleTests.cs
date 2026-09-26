using System.Numerics;
using PixelAniMaker.Core.Animation;
using PixelAniMaker.Core.Editing;
using PixelAniMaker.Core.Imaging;
using PixelAniMaker.Core.Project;
using PixelAniMaker.Core.Rigging;
using PixelAniMaker.Core.Rigging.Body;

namespace PixelAniMaker.Core.Tests;

public class ResolutionScaleTests
{
    /// <summary>A small generated mannequin (all 5 stored directions) with a bust, so there is real detail to compare.</summary>
    private static Character Body()
    {
        var c = MannequinBuilder.Build(BodyProportions.For(5, 12)).Build();
        c.Outline.OutlineIndex = c.Palette.GetOrAdd(new Rgba(40, 30, 30));
        c.Outline.InnerIndex = c.Palette.GetOrAdd(new Rgba(120, 90, 80));
        BustPart.Add(c, BustSize.Medium);
        c.History.Clear();
        return c;
    }

    private static ushort[] Composite(Character c, Direction d, PoseData? pose = null) =>
        new Compositor().Compose(c, d, pose).Indices.ToArray();

    private static ushort[] Doubled(ushort[] pixels, int width, int height)
    {
        var big = new ushort[pixels.Length * 4];
        for (int y = 0; y < height * 2; y++)
            for (int x = 0; x < width * 2; x++)
                big[y * width * 2 + x] = pixels[y / 2 * width + x / 2];
        return big;
    }

    [Fact]
    public void Nearest_doubles_every_drawing_and_joint_so_the_frame_is_the_same_picture_twice_as_big()
    {
        var c = Body();
        var (w, h) = (c.Width, c.Height);
        var before = c.StoredDirections.ToDictionary(d => d, d => Composite(c, d));
        var head = c.Find("head")!.View(Direction.Left);
        var (pos, pivot, image) = (head.RestPosition, head.RestPivot, head.Image);

        Assert.True(ResolutionScale.Apply(c, [], UpscaleMethod.Nearest, c.History));

        Assert.Equal((w * 2, h * 2), (c.Width, c.Height));
        Assert.Equal((pos * 2, pivot * 2), (head.RestPosition, head.RestPivot));
        Assert.Equal((image.Width * 2, image.Height * 2), (head.Image.Width, head.Image.Height));
        Assert.Equal(image[5, 7], head.Image[11, 14]);
        foreach (var d in c.StoredDirections)
            Assert.Equal(Doubled(before[d], w, h), Composite(c, d));
    }

    [Fact]
    public void Smooth_uses_Scale2x()
    {
        var c = Body();
        var chest = c.Find("chest")!.View(Direction.Front);
        var expected = Scale2x.Upscale(chest.Layers[0].Image).Pixels.ToArray();
        ResolutionScale.Apply(c, [], UpscaleMethod.Smooth, c.History);
        Assert.Equal(expected, chest.Layers[0].Image.Pixels.ToArray());
        Assert.NotEqual(ResolutionScale.Upscale(Scale2xSource(), UpscaleMethod.Nearest).Pixels.ToArray(),
            ResolutionScale.Upscale(Scale2xSource(), UpscaleMethod.Smooth).Pixels.ToArray());

        static IndexedImage Scale2xSource()
        {
            var diagonal = new IndexedImage(3, 3);
            for (int i = 0; i < 3; i++)
                diagonal.Set(i, i, 1);
            return diagonal;
        }
    }

    [Fact]
    public void Keys_poses_touchups_angle_images_attachments_and_pixel_settings_grow_too()
    {
        var c = Body();
        var clip = new AnimationClip("jump", 4);
        clip.SetKey(Direction.FrontLeft, new Keyframe(1, new PoseData(new Dictionary<string, double> { ["head"] = 20 }, new(1.5f, -6)), Easing.Linear));
        clip.Touchups.Set(Direction.Right, 2, new PixelOverrides([KeyValuePair.Create((3, 4), (ushort)2)]));
        c.PoseFor(Direction.Back).Offset = new(2, -3);
        var arm = c.Find("upper_arm_l")!.View(Direction.Front);
        arm.Attachments.Set("grip", new Vector2(2.5f, 9));
        arm.Variants.Set(45, new PartVariant(new IndexedImage(4, 6), new Vector2(1, 2)));
        c.Shading.Width = 1;
        var bust = c.Find("bust")!;
        float reach = bust.Secondary!.Max;

        ResolutionScale.Apply(c, [clip], UpscaleMethod.Nearest, c.History);

        var key = clip.KeyAt(Direction.FrontLeft, 1)!.Pose;
        Assert.Equal((new Vector2(3, -12), 20.0), (key.Offset, key.Get("head")));
        Assert.Equal([(6, 8), (6, 9), (7, 8), (7, 9)], clip.Touchups.Get(Direction.Right, 2).Keys.Order());
        Assert.Equal(new Vector2(4, -6), c.PoseFor(Direction.Back).Offset);
        Assert.Equal(new Vector2(5, 18), arm.Attachments.All["grip"]);
        Assert.Equal((8, 12, new Vector2(2, 4)), (arm.Variants.Get(45)!.Image.Width, arm.Variants.Get(45)!.Image.Height, arm.Variants.Get(45)!.LocalPivot));
        Assert.Equal(2, c.Shading.Width);
        Assert.Equal(Math.Min(reach * 2, 6), bust.Secondary!.Max);

        c.History.Undo();
        Assert.Equal(new Vector2(1.5f, -6), clip.KeyAt(Direction.FrontLeft, 1)!.Pose.Offset);
        Assert.Equal([(3, 4)], clip.Touchups.Get(Direction.Right, 2).Keys);
        Assert.Equal(new Vector2(2, -3), c.PoseFor(Direction.Back).Offset);
        Assert.Equal(new Vector2(2.5f, 9), arm.Attachments.All["grip"]);
        Assert.Equal(4, arm.Variants.Get(45)!.Image.Width);
        Assert.Equal((1, reach), (c.Shading.Width, bust.Secondary!.Max));
    }

    [Fact]
    public void Undo_puts_the_same_images_back_and_earlier_drawing_still_undoes()
    {
        var c = Body();
        var chest = c.Find("chest")!.View(Direction.Front);
        var layer = chest.Layers[0];
        var edit = new PixelEdit("점", layer.Image, () => { });
        edit.Set(1, 1, 3);
        c.History.Push(edit);
        var (w, h) = (c.Width, c.Height);
        var before = Composite(c, Direction.Front);
        int shifts = 0;
        c.CanvasSizeChanged += (_, e) => shifts += e.Scale == 2 ? 1 : e.Scale == 0.5f ? -1 : 100;

        ResolutionScale.Apply(c, [], UpscaleMethod.Smooth, c.History);
        Assert.NotSame(layer, chest.Layers[0]);
        var scaled = chest.Layers[0];
        c.History.Undo();
        Assert.Same(layer, chest.Layers[0]);
        Assert.Equal((w, h), (c.Width, c.Height));
        Assert.Equal(before, Composite(c, Direction.Front));
        c.History.Redo();
        Assert.Same(scaled, chest.Layers[0]);
        Assert.Equal(1, shifts);

        c.History.Undo();
        c.History.Undo();
        Assert.NotEqual(3, layer.Image[1, 1]);   // the dot drawn before the scale is undone on the original image
    }

    [Fact]
    public void A_canvas_that_would_get_too_large_is_refused_and_a_scaled_project_saves()
    {
        var c = Body();
        CanvasResize.ApplyUnrecorded(c, new CanvasMargins(0, 0, CanvasResize.MaxSize - c.Width, 0));
        Assert.False(ResolutionScale.Apply(c, [], UpscaleMethod.Nearest, c.History));
        Assert.False(c.History.CanUndo);

        var small = Body();
        ResolutionScale.Apply(small, [], UpscaleMethod.Nearest, small.History);
        using var ms = new MemoryStream();
        ProjectFile.Save(new ProjectData(small, []), ms, new RawCodec());
        ms.Position = 0;
        var loaded = ProjectFile.Load(ms, new RawCodec()).Character;
        Assert.Equal((small.Width, small.Height), (loaded.Width, loaded.Height));
        Assert.Equal(Composite(small, Direction.BackLeft), Composite(loaded, Direction.BackLeft));
    }
}
