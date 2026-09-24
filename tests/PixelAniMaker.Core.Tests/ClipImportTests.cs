using System.Numerics;
using PixelAniMaker.Core.Animation;
using PixelAniMaker.Core.Imaging;
using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.Core.Tests;

public class ClipImportTests
{
    private static Character OnePart()
    {
        var v = new PartViewSpec(0, 0, 0, 0, 0, "a");
        return new CharacterSpec(4, 4, [new PartSpec("arm", "arm", null,
                new Dictionary<string, PartViewSpec> { ["front"] = v, ["left"] = v, ["back"] = v })])
            .Build(_ => new RgbaImage(1, 1, [new Rgba(1, 1, 1)]));
    }

    private static AnimationClip Wave(string name = "wave")
    {
        var clip = new AnimationClip(name, 4, 8, loop: false);
        clip.SetKey(Direction.Front, new Keyframe(0, new PoseData(new Dictionary<string, double> { ["arm"] = 30, ["tail"] = 10 }, Vector2.Zero)));
        clip.SetKey(Direction.Left, new Keyframe(2, new PoseData(new Dictionary<string, double> { ["arm"] = -30 }, Vector2.One), Easing.Linear));
        clip.Touchups.Set(Direction.Front, 0, new PixelOverrides { [(1, 1)] = 1 });
        return clip;
    }

    [Fact]
    public void Copies_settings_and_keys_but_not_touchups()
    {
        var copy = ClipImport.Import([Wave()], [], OnePart()).Clips.Single();
        Assert.Equal(("wave", 4, 8, false), (copy.Name, copy.FrameCount, copy.Fps, copy.Loop));
        Assert.Equal(30, copy.KeyAt(Direction.Front, 0)!.Pose.Get("arm"));
        Assert.Equal(Easing.Linear, copy.KeyAt(Direction.Right, 2)!.Easing);   // Right plays the Left track
        Assert.Empty(copy.Touchups.Frames);
    }

    [Fact]
    public void Clashing_names_get_a_number()
    {
        var result = ClipImport.Import([Wave(), Wave()], ["wave", "wave (2)"], OnePart());
        Assert.Equal(["wave (3)", "wave (4)"], result.Clips.Select(c => c.Name));
    }

    [Fact]
    public void Reports_parts_the_target_does_not_have()
    {
        Assert.Equal(["tail"], ClipImport.Import([Wave()], [], OnePart()).UnknownParts);
    }
}
