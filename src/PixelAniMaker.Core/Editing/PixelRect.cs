namespace PixelAniMaker.Core.Editing;

/// <summary>A rectangle of pixels with inclusive corners.</summary>
public readonly record struct PixelRect(int X0, int Y0, int X1, int Y1)
{
    public static PixelRect FromCorners(int ax, int ay, int bx, int by) =>
        new(Math.Min(ax, bx), Math.Min(ay, by), Math.Max(ax, bx), Math.Max(ay, by));

    public int Width => X1 - X0 + 1;
    public int Height => Y1 - Y0 + 1;

    public bool Contains(int x, int y) => x >= X0 && x <= X1 && y >= Y0 && y <= Y1;

    public PixelRect Offset(int dx, int dy) => new(X0 + dx, Y0 + dy, X1 + dx, Y1 + dy);

    /// <summary>The part inside a width×height image, or null when nothing is left.</summary>
    public PixelRect? ClipTo(int width, int height)
    {
        var r = new PixelRect(Math.Max(0, X0), Math.Max(0, Y0), Math.Min(width - 1, X1), Math.Min(height - 1, Y1));
        return r.X0 <= r.X1 && r.Y0 <= r.Y1 ? r : null;
    }

    public IEnumerable<(int X, int Y)> Pixels()
    {
        for (int y = Y0; y <= Y1; y++)
            for (int x = X0; x <= X1; x++)
                yield return (x, y);
    }
}
