using System.Numerics;
using PixelAniMaker.Core.Animation;
using PixelAniMaker.Core.Export;
using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.Core.Tests;

public class EdgeCheckTests
{
    [Fact]
    public void Frames_reaching_the_canvas_edge_are_found_with_their_sides()
    {
        var c = BustPartTests.Body();                      // head top at y 8, pelvis bottom at y 104 of 128
        var clip = new AnimationClip("jump", 3);
        foreach (var d in DirectionExtensions.Stored)
        {
            clip.SetKey(d, new Keyframe(0, PoseData.Rest, Easing.Step));
            clip.SetKey(d, new Keyframe(1, new PoseData(new Dictionary<string, double>(), new Vector2(0, -20)), Easing.Step));
            clip.SetKey(d, new Keyframe(2, new PoseData(new Dictionary<string, double>(), new Vector2(0, 30)), Easing.Step));
        }
        var hits = EdgeCheck.Find(c, [clip], new Compositor(), DirectionExtensions.All);

        Assert.DoesNotContain(hits, h => h.Frame == 0);
        Assert.Contains(hits, h => h.Frame == 1 && h.Direction == Direction.Front && h.Sides == CanvasSides.Top);
        Assert.Contains(hits, h => h.Frame == 2 && h.Sides.HasFlag(CanvasSides.Bottom));
        Assert.Equal(8, hits.Count);                       // frames 1 and 2 in four directions
        Assert.Contains("jump 8프레임 (위·아래)", EdgeCheck.Summary(hits));
        Assert.Null(EdgeCheck.Summary([]));
    }
}
