using PixelAniMaker.Core.Animation;
using PixelAniMaker.Core.History;
using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.Core.Export;

/// <summary>Grows the canvas where frames of the clips still reach its edges (see <see cref="EdgeCheck"/>).</summary>
public static class CanvasFit
{
    /// <summary>
    /// Adds <paramref name="step"/> pixels on every side some frame touches, until none does (at most
    /// <paramref name="rounds"/> times). Each growth is an undo step of its own unless a group is open.
    /// </summary>
    /// <returns>True when the canvas grew.</returns>
    public static bool GrowToFit(Character character, IReadOnlyList<AnimationClip> clips, UndoHistory history, int step = 8, int rounds = 8)
    {
        bool grew = false;
        var compositor = new Compositor();
        for (int round = 0; round < rounds && clips.Count > 0; round++)
        {
            var hits = EdgeCheck.Find(character, clips, compositor, character.Directions);
            if (hits.Count == 0)
                break;
            var sides = hits.Aggregate(CanvasSides.None, (all, h) => all | h.Sides);
            int Grow(CanvasSides side) => sides.HasFlag(side) ? step : 0;
            if (!CanvasResize.Apply(character, clips, new CanvasMargins(Grow(CanvasSides.Left), Grow(CanvasSides.Top),
                    Grow(CanvasSides.Right), Grow(CanvasSides.Bottom)), history))
                break;
            grew = true;
        }
        return grew;
    }
}
