using PixelAniMaker.Core.Animation;
using PixelAniMaker.Core.Export;
using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.Core.Tests;

public class FrameHoldTests
{
    [Fact]
    public void Held_frames_last_whole_ticks_and_one_tick_is_the_default()
    {
        var clip = new AnimationClip("walk", 4, fps: 8);
        clip.SetHold(1, 3);
        clip.SetHold(2, 99);

        Assert.Equal([125, 375, 16 * 125, 125], Enumerable.Range(0, 4).Select(clip.DurationMs));
        clip.SetHold(1, 1);
        Assert.Equal(new Dictionary<int, int> { [2] = 16 }, clip.Holds);
        Assert.Equal(16, clip.CopyAs("copy").Hold(2));
    }

    [Fact]
    public void Holds_are_saved_only_when_set_and_only_inside_the_clip()
    {
        var clip = new AnimationClip("walk", 4, fps: 10);
        Assert.DoesNotContain("holds", AnimationJson.Serialize([clip]));

        clip.SetHold(3, 2);
        var loaded = AnimationJson.Parse(AnimationJson.Serialize([clip]))[0];
        Assert.Equal(2, loaded.Hold(3));

        clip.FrameCount = 3;   // frame 3 is outside now: not written
        Assert.DoesNotContain("holds", AnimationJson.Serialize([clip]));
    }

    [Fact]
    public void Sheet_json_and_gif_use_each_frames_duration()
    {
        var project = GoldenTests.Build();
        var clip = project.Clips[0];                    // 4 frames at 8 fps
        clip.SetHold(2, 4);

        var sheet = SpriteSheet.Build(project.Character, [clip], new Compositor());
        Assert.Equal([125, 125, 500, 125], sheet.Clips[0].Directions["front"].Select(f => f.DurationMs));

        using var gif = new MemoryStream();
        AnimationGif.Write(gif, project.Character, clip, new Compositor());
        Assert.Equal([13, 13, 50, 13], GifDelays(gif.ToArray()));   // hundredths, rounded
    }

    /// <summary>Delays of the graphic control extensions (21 F9 04 flags delayLo delayHi).</summary>
    private static List<int> GifDelays(byte[] gif)
    {
        var delays = new List<int>();
        for (int i = 0; i + 5 < gif.Length; i++)
            if (gif[i] == 0x21 && gif[i + 1] == 0xF9 && gif[i + 2] == 0x04)
                delays.Add(gif[i + 4] | gif[i + 5] << 8);
        return delays;
    }
}
