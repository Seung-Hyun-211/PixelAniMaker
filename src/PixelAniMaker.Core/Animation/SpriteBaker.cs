using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.Core.Animation;

/// <summary>Renders every frame of a clip in every direction (the sprite sheet contents).</summary>
public static class SpriteBaker
{
    /// <param name="touchups">False gives the frames as generated, before hand-painted fixes.</param>
    public static IReadOnlyDictionary<Direction, CompositeResult[]> Bake(
        Character character, AnimationClip clip, Compositor compositor, bool touchups = true) =>
        DirectionExtensions.All.ToDictionary(d => d, d =>
            Enumerable.Range(0, clip.FrameCount)
                .Select(f =>
                {
                    var frame = compositor.Compose(character, d, clip.Evaluate(d, f));
                    if (touchups)
                        clip.Touchups.ApplyTo(frame, d, f);
                    return frame;
                })
                .ToArray());
}
