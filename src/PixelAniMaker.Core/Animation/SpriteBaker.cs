using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.Core.Animation;

/// <summary>Renders every frame of a clip in every direction (the sprite sheet contents).</summary>
public static class SpriteBaker
{
    /// <param name="touchups">False gives the frames as generated, before hand-painted fixes.</param>
    /// <param name="directions">Which directions to render (default: the classic four).</param>
    /// <param name="secondary">Add secondary motion (default: the character's export setting).</param>
    public static IReadOnlyDictionary<Direction, CompositeResult[]> Bake(
        Character character, AnimationClip clip, Compositor compositor, bool touchups = true,
        IReadOnlyList<Direction>? directions = null, bool? secondary = null)
    {
        bool sway = (secondary ?? character.SecondaryInExport) && SecondaryMotion.HasAny(character);
        return (directions ?? DirectionExtensions.All).ToDictionary(d => d, d =>
        {
            var extra = sway ? SecondaryMotion.Solve(character, clip, d) : null;
            return Enumerable.Range(0, clip.FrameCount)
                .Select(f =>
                {
                    var frame = compositor.Compose(character, d, clip.Evaluate(d, f), secondary: extra?[f]);
                    if (touchups)
                        clip.Touchups.ApplyTo(frame, d, f);
                    return frame;
                })
                .ToArray();
        });
    }

    /// <summary>The pose a frame is drawn with, secondary angles included (for attachment points).</summary>
    public static IReadOnlyList<PoseData> FramePoses(Character character, AnimationClip clip, Direction direction, bool? secondary = null)
    {
        bool sway = (secondary ?? character.SecondaryInExport) && SecondaryMotion.HasAny(character);
        var extra = sway ? SecondaryMotion.Solve(character, clip, direction) : null;
        return Enumerable.Range(0, clip.FrameCount)
            .Select(f => extra is null ? clip.Evaluate(direction, f) : extra[f].Apply(clip.Evaluate(direction, f))).ToList();
    }
}
