using System.Numerics;

namespace PixelAniMaker.Core.Rigging;

/// <summary>Where a part is on the canvas in one direction: its joint position and accumulated rotation.</summary>
public readonly record struct PartTransform(Part Part, PartView View, Vector2 Pivot, float Angle)
{
    private const float QuarterTurn = MathF.PI / 2;

    /// <summary>True when the rotation is a multiple of 90°, so pixels map 1:1 without resampling.</summary>
    public bool IsGridAligned
    {
        get
        {
            float quarters = Angle / QuarterTurn;
            return MathF.Abs(quarters - MathF.Round(quarters)) < 1e-4f;
        }
    }

    /// <summary>Image-local point → canvas point.</summary>
    public Vector2 ToCanvas(Vector2 local) => Pivot + Rotate(local - View.LocalPivot, Angle);

    /// <summary>Canvas point → image-local point.</summary>
    public Vector2 ToLocal(Vector2 canvas) => View.LocalPivot + Rotate(canvas - Pivot, -Angle);

    /// <summary>Canvas pixel → image pixel (sampled at the pixel centre).</summary>
    public (int X, int Y) ToLocalPixel(int x, int y)
    {
        var p = ToLocal(new Vector2(x + 0.5f, y + 0.5f));
        return ((int)MathF.Floor(p.X), (int)MathF.Floor(p.Y));
    }

    public static Vector2 Rotate(Vector2 v, float angle)
    {
        float c = MathF.Cos(angle), s = MathF.Sin(angle);
        return new Vector2(c * v.X - s * v.Y, s * v.X + c * v.Y);
    }
}
