namespace PixelAniMaker.Core.Rigging.Body;

/// <summary>
/// One row of the proportion table: how a body of <see cref="Heads"/> heads splits below the head,
/// in head heights (neck + torso + legs = heads − 1), and the head size in pixels a new project gets.
/// </summary>
public sealed record ProportionRow(double Heads, double Neck, double Torso, double Legs, int HeadPixels);

/// <summary>
/// Body proportions of a mannequin <see cref="Heads"/> heads tall with a head <see cref="HeadPixels"/> high.
/// Lengths along the body come from <see cref="Table"/> (interpolated between rows, so any value in its range
/// works and a new row extends it); widths and thicknesses are linear in the head count.
/// Every length is in head heights unless it says pixels.
/// </summary>
public sealed record BodyProportions
{
    /// <summary>Rows in increasing head count; add one to support a new body type.</summary>
    public static IReadOnlyList<ProportionRow> Table { get; } =
    [
        new(4, 0.10, 1.30, 1.60, 30),
        new(5, 0.15, 1.70, 2.15, 26),
        new(6, 0.20, 2.00, 2.80, 24),
        new(7, 0.25, 2.35, 3.40, 22),
        new(8, 0.30, 2.70, 4.00, 20),
    ];

    public static double MinHeads => Table[0].Heads;
    public static double MaxHeads => Table[^1].Heads;

    private BodyProportions(double heads, int headPixels, ProportionRow row, BodyShape shape, BodyShapeFactors factors)
    {
        Heads = heads;
        HeadPixels = headPixels;
        Neck = row.Neck;
        Torso = row.Torso;
        Legs = row.Legs;
        Shape = shape;
        _f = factors;
    }

    private readonly BodyShapeFactors _f;

    /// <param name="headPixels">Head height in pixels; null uses the table's size for that head count.</param>
    /// <param name="shape">Build: scales widths and thicknesses only, so lengths (and the default clips) stay put.</param>
    /// <param name="factors">Factors of your own instead of the <paramref name="shape"/>'s (a custom build).</param>
    public static BodyProportions For(double heads, int? headPixels = null, BodyShape shape = BodyShape.Standard,
        BodyShapeFactors? factors = null)
    {
        if (heads < MinHeads || heads > MaxHeads)
            throw new ArgumentOutOfRangeException(nameof(heads), heads, $"Supported: {MinHeads}–{MaxHeads} heads.");
        var row = Interpolate(heads);
        int px = headPixels ?? row.HeadPixels;
        if (px < 8)
            throw new ArgumentOutOfRangeException(nameof(headPixels), px, "A head needs at least 8 pixels.");
        return new BodyProportions(heads, px, row, shape, factors ?? BodyShapeFactors.For(shape));
    }

    public BodyShape Shape { get; }

    private static ProportionRow Interpolate(double heads)
    {
        int i = 0;
        while (i < Table.Count - 2 && heads > Table[i + 1].Heads)
            i++;
        var (a, b) = (Table[i], Table[i + 1]);
        double t = (heads - a.Heads) / (b.Heads - a.Heads);
        double L(double x, double y) => x + (y - x) * t;
        return new(heads, L(a.Neck, b.Neck), L(a.Torso, b.Torso), L(a.Legs, b.Legs),
            (int)Math.Round(L(a.HeadPixels, b.HeadPixels)));
    }

    public double Heads { get; }
    public int HeadPixels { get; }

    /// <summary>Chin to shoulder line.</summary>
    public double Neck { get; }

    /// <summary>Shoulder line to crotch: chest, waist and pelvis.</summary>
    public double Torso { get; }

    /// <summary>Crotch to sole.</summary>
    public double Legs { get; }

    public double BodyHeightPixels => Heads * HeadPixels;

    // along the body
    public double Chest => Torso * 0.45;
    public double Waist => Torso * 0.20;
    public double Pelvis => Torso * 0.35;
    public double Arm => 0.44 * Heads - 0.2;
    public double UpperArm => Arm * 0.43;
    public double Forearm => Arm * 0.37;
    public double Hand => Arm * 0.20;
    public double FootHeight => 0.12 + 0.015 * Heads;
    public double FootLength => 0.02 + 0.12 * Heads;
    public double FootWidth => 0.34;

    // across (half widths from the centre line) and depth
    public double HeadHalfWidth => (0.54 - 0.02 * Heads) * _f.Head;
    public double HeadDepthScale => 1 - 0.015 * (Heads - 4);
    public double ShoulderHalf => (0.2 + 0.08 * Heads) * _f.Shoulder;
    public double WaistHalf => (0.55 * (0.2 + 0.08 * Heads) + 0.05) * _f.Waist;
    public double HipHalf => (0.3 + 0.055 * Heads) * _f.Hip;
    public double ChestDepth => (0.3 + 0.025 * Heads) * _f.Depth;

    // thicknesses (radii)
    public double NeckRadius => (0.07 + 0.016 * Heads) * _f.Neck;
    public double ThighRadius => (0.12 + 0.028 * Heads) * _f.Leg;
    public double UpperArmRadius => (0.08 + 0.018 * Heads) * _f.Arm;
}
