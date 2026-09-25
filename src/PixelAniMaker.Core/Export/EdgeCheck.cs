using PixelAniMaker.Core.Animation;
using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.Core.Export;

[Flags]
public enum CanvasSides
{
    None = 0,
    Left = 1,
    Top = 2,
    Right = 4,
    Bottom = 8,
}

/// <summary>A frame whose drawing reaches the canvas edge (and so may be cut off there).</summary>
public sealed record EdgeHit(string Clip, Direction Direction, int Frame, CanvasSides Sides);

/// <summary>Finds frames that touch the canvas edge, to warn before a cut-off drawing ends up in a sheet.</summary>
public static class EdgeCheck
{
    /// <summary>The canvas sides the frame's drawn pixels reach.</summary>
    public static CanvasSides Touching(CompositeResult frame)
    {
        int w = frame.Width, h = frame.Height;
        bool Drawn(int x, int y) => frame.Indices[y * w + x] != 0;
        var sides = CanvasSides.None;
        for (int x = 0; x < w; x++)
        {
            if (Drawn(x, 0)) sides |= CanvasSides.Top;
            if (Drawn(x, h - 1)) sides |= CanvasSides.Bottom;
        }
        for (int y = 0; y < h; y++)
        {
            if (Drawn(0, y)) sides |= CanvasSides.Left;
            if (Drawn(w - 1, y)) sides |= CanvasSides.Right;
        }
        return sides;
    }

    /// <summary>Every exported frame (touch-ups and export sway included) that touches the canvas edge.</summary>
    public static IReadOnlyList<EdgeHit> Find(Character character, IEnumerable<AnimationClip> clips, Compositor compositor,
        IReadOnlyList<Direction> directions)
    {
        var hits = new List<EdgeHit>();
        foreach (var clip in clips)
            foreach (var (direction, frames) in SpriteBaker.Bake(character, clip, compositor, directions: directions))
                for (int f = 0; f < frames.Length; f++)
                    if (Touching(frames[f]) is var sides and not CanvasSides.None)
                        hits.Add(new EdgeHit(clip.Name, direction, f, sides));
        return hits;
    }

    /// <summary>"위·왼쪽" style names of the sides.</summary>
    public static string SideNames(CanvasSides sides) => string.Join("·", new[]
    {
        (CanvasSides.Top, "위"), (CanvasSides.Bottom, "아래"), (CanvasSides.Left, "왼쪽"), (CanvasSides.Right, "오른쪽"),
    }.Where(s => sides.HasFlag(s.Item1)).Select(s => s.Item2));

    /// <summary>A short warning listing the clips and frame counts, or null when nothing touches the edge.</summary>
    public static string? Summary(IReadOnlyList<EdgeHit> hits)
    {
        if (hits.Count == 0)
            return null;
        var perClip = hits.GroupBy(h => h.Clip).Select(g =>
            $"{g.Key} {g.Count()}프레임 ({SideNames(g.Aggregate(CanvasSides.None, (s, h) => s | h.Sides))})");
        return $"캔버스 끝에 닿는 프레임이 {hits.Count}개 있습니다 (잘렸을 수 있음): {string.Join(", ", perClip)}. " +
               "편집 → 캔버스 크기로 여백을 늘릴 수 있습니다.";
    }
}
