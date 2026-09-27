namespace PixelAniMaker.Core.Rigging.Body;

/// <summary>Build of a generated mannequin: how wide and thick it is at a given head count.</summary>
public enum BodyShape
{
    /// <summary>The original mannequin (every factor 1).</summary>
    Standard,
    Feminine,
    Masculine,
    Slim,
    Chubby,
    Muscular,
}

/// <summary>
/// Factors a <see cref="BodyShape"/> applies to <see cref="BodyProportions"/>' widths and thicknesses; lengths along
/// the body (and so the default clips) stay the same. Add a row to <see cref="Table"/> for a new shape, or pass
/// factors of your own to <see cref="BodyProportions.For"/>. <see cref="Head"/> scales the head's width.
/// </summary>
public sealed record BodyShapeFactors(double Shoulder, double Waist, double Hip, double Depth, double Leg, double Arm, double Neck,
    double Head = 1)
{
    public static BodyShapeFactors One { get; } = new(1, 1, 1, 1, 1, 1, 1);

    public static IReadOnlyDictionary<BodyShape, BodyShapeFactors> Table { get; } = new Dictionary<BodyShape, BodyShapeFactors>
    {
        [BodyShape.Standard] = One,
        // narrow shoulders, small waist, wide hips, slender arms and neck
        [BodyShape.Feminine] = new(0.78, 0.78, 1.3, 0.94, 1.06, 0.8, 0.82),
        // broad shoulders, straight waist, narrow hips
        [BodyShape.Masculine] = new(1.12, 1.06, 0.94, 1.08, 1.06, 1.12, 1.15),
        [BodyShape.Slim] = new(0.9, 0.84, 0.9, 0.84, 0.78, 0.78, 0.88),
        // round belly and thick limbs
        [BodyShape.Chubby] = new(1.05, 1.65, 1.2, 1.55, 1.3, 1.25, 1.2),
        // wide shoulders, deep chest, heavy arms and legs
        [BodyShape.Muscular] = new(1.25, 1.06, 1.0, 1.2, 1.25, 1.42, 1.3),
    };

    public static BodyShapeFactors For(BodyShape shape) => Table.TryGetValue(shape, out var f) ? f : One;
}
