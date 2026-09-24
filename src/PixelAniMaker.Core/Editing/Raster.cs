using PixelAniMaker.Core.Imaging;

namespace PixelAniMaker.Core.Editing;

/// <summary>Pixel rasterisation helpers shared by the tools.</summary>
public static class Raster
{
    /// <summary>Bresenham line, both end points included.</summary>
    public static IEnumerable<(int X, int Y)> Line(int x0, int y0, int x1, int y1)
    {
        int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;
        while (true)
        {
            yield return (x0, y0);
            if (x0 == x1 && y0 == y1)
                yield break;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }

    /// <summary>4-connected region that shares the colour at (x, y).</summary>
    public static IEnumerable<(int X, int Y)> FloodRegion(IndexedImage image, int x, int y)
    {
        if (!image.InBounds(x, y))
            yield break;
        int target = image[x, y];
        var visited = new bool[image.Width * image.Height];
        var stack = new Stack<(int X, int Y)>();
        stack.Push((x, y));
        while (stack.Count > 0)
        {
            var (cx, cy) = stack.Pop();
            if (!image.InBounds(cx, cy) || visited[cy * image.Width + cx] || image[cx, cy] != target)
                continue;
            visited[cy * image.Width + cx] = true;
            yield return (cx, cy);
            stack.Push((cx + 1, cy));
            stack.Push((cx - 1, cy));
            stack.Push((cx, cy + 1));
            stack.Push((cx, cy - 1));
        }
    }
}
