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

    /// <summary>Outline of the rectangle spanned by two corners (inclusive).</summary>
    public static IEnumerable<(int X, int Y)> Rectangle(int x0, int y0, int x1, int y1)
    {
        (x0, x1) = (Math.Min(x0, x1), Math.Max(x0, x1));
        (y0, y1) = (Math.Min(y0, y1), Math.Max(y0, y1));
        for (int x = x0; x <= x1; x++)
        {
            yield return (x, y0);
            if (y1 != y0) yield return (x, y1);
        }
        for (int y = y0 + 1; y < y1; y++)
        {
            yield return (x0, y);
            if (x1 != x0) yield return (x1, y);
        }
    }

    /// <summary>
    /// Outline of the ellipse inscribed in the rectangle spanned by two corners (inclusive), drawn with
    /// the integer midpoint algorithm for rectangles (A. Zingl), so it is closed and symmetric.
    /// </summary>
    public static IEnumerable<(int X, int Y)> Ellipse(int x0, int y0, int x1, int y1)
    {
        var points = new HashSet<(int, int)>();
        long a = Math.Abs(x1 - x0), b = Math.Abs(y1 - y0), b1 = b & 1;
        double dx = 4 * (1 - a) * b * b, dy = 4 * (b1 + 1) * a * a;
        double err = dx + dy + b1 * a * a, e2;

        if (x0 > x1) { x0 = x1; x1 += (int)a; }
        if (y0 > y1) y0 = y1;
        y0 += (int)((b + 1) / 2);
        y1 = y0 - (int)b1;
        long aa = 8 * a * a, bb = 8 * b * b;

        do
        {
            points.Add((x1, y0));
            points.Add((x0, y0));
            points.Add((x0, y1));
            points.Add((x1, y1));
            e2 = 2 * err;
            if (e2 <= dy) { y0++; y1--; err += dy += aa; }
            if (e2 >= dx || 2 * err > dy) { x0++; x1--; err += dx += bb; }
        } while (x0 <= x1);

        while (y0 - y1 <= b)
        {
            points.Add((x0 - 1, y0));
            points.Add((x1 + 1, y0++));
            points.Add((x0 - 1, y1));
            points.Add((x1 + 1, y1--));
        }
        return points;
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
