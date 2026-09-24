using System.Numerics;
using PixelAniMaker.Core.Imaging;

namespace PixelAniMaker.Core.Rigging;

/// <summary>
/// Where a part is on the canvas in one direction: its joint position and accumulated rotation, and
/// the image actually drawn — the base image, or an angle variant turned by the remaining angle.
/// </summary>
public readonly record struct PartTransform
{
    private const float QuarterTurn = MathF.PI / 2;

    public PartTransform(Part part, PartView view, Vector2 pivot, float angle)
    {
        Part = part;
        View = view;
        Pivot = pivot;
        Angle = angle;
        (Image, ImagePivot, ImageAngle) = view.Pick(angle);
    }

    public Part Part { get; }
    public PartView View { get; }

    /// <summary>Joint position on the canvas.</summary>
    public Vector2 Pivot { get; }

    /// <summary>Accumulated rotation (radians) — what children inherit.</summary>
    public float Angle { get; }

    /// <summary>The image drawn for this rotation (base image or angle variant).</summary>
    public IndexedImage Image { get; }

    /// <summary>Joint position inside <see cref="Image"/>.</summary>
    public Vector2 ImagePivot { get; }

    /// <summary>Rotation still applied to <see cref="Image"/> (radians).</summary>
    public float ImageAngle { get; }

    /// <summary>True when the drawn image is turned by a multiple of 90°, so pixels map 1:1.</summary>
    public bool IsGridAligned
    {
        get
        {
            float quarters = ImageAngle / QuarterTurn;
            return MathF.Abs(quarters - MathF.Round(quarters)) < 1e-4f;
        }
    }

    /// <summary>Image-local point → canvas point.</summary>
    public Vector2 ToCanvas(Vector2 local) => Pivot + Rotate(local - ImagePivot, ImageAngle);

    /// <summary>Canvas point → image-local point.</summary>
    public Vector2 ToLocal(Vector2 canvas) => ImagePivot + Rotate(canvas - Pivot, -ImageAngle);

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
