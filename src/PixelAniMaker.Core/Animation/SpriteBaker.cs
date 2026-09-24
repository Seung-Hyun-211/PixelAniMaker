using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.Core.Animation;

/// <summary>Renders every frame of a clip in every direction (the sprite sheet contents).</summary>
public static class SpriteBaker
{
    public static IReadOnlyDictionary<Direction, CompositeResult[]> Bake(Character character, AnimationClip clip, Compositor compositor) =>
        DirectionExtensions.All.ToDictionary(d => d, d =>
            Enumerable.Range(0, clip.FrameCount)
                .Select(f => compositor.Compose(character, d, clip.Evaluate(d, f)))
                .ToArray());
}
