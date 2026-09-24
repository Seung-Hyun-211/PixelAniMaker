using System.Numerics;
using PixelAniMaker.Core.History;
using PixelAniMaker.Core.Imaging;

namespace PixelAniMaker.Core.Rigging;

/// <summary>A hand-drawn look of a part at one rotation, with the joint position inside it.</summary>
public sealed record PartVariant(IndexedImage Image, Vector2 LocalPivot);

/// <summary>
/// Per-angle replacement images of a part view (45° steps). When the part is rotated near one of
/// these angles, that image is drawn and turned only by the remaining angle — so small parts such as
/// hands can keep a clean, hand-drawn shape instead of being resampled a long way.
/// </summary>
public sealed class AngleVariants
{
    /// <summary>How far from a variant's angle it is still used.</summary>
    public const double Reach = 22.5;
    public const int Step = 45;

    private readonly SortedDictionary<int, PartVariant> _variants = [];

    public IReadOnlyDictionary<int, PartVariant> All => _variants;

    public PartVariant? Get(int angle) => _variants.GetValueOrDefault(angle);

    public void Set(int angle, PartVariant? variant)
    {
        if (variant is null)
            _variants.Remove(angle);
        else
            _variants[NormalizeKey(angle)] = variant;
    }

    /// <summary>The 45° step closest to an angle (degrees), in (-180, 180].</summary>
    public static int NearestStep(double degrees) => NormalizeKey((int)Math.Round(Pose.Normalize(degrees) / Step) * Step);

    /// <summary>The variant angle to use for a rotation (degrees), or null for the base image.</summary>
    public int? Pick(double degrees)
    {
        degrees = Pose.Normalize(degrees);
        int? best = null;
        double bestDistance = Math.Abs(degrees); // the base image is the 0° look
        foreach (var angle in _variants.Keys)
        {
            double distance = Math.Abs(Pose.Normalize(degrees - angle));
            if (distance <= Reach && distance < bestDistance)
                (best, bestDistance) = (angle, distance);
        }
        return best;
    }

    private static int NormalizeKey(int angle) => (int)Pose.Normalize(angle);
}

/// <summary>Undoable add/replace/remove of a part's angle variant.</summary>
public sealed class VariantChange(AngleVariants variants, int angle, PartVariant? before, PartVariant? after) : IUndoableAction
{
    public string Name => after is null ? "각도 이미지 삭제" : "각도 이미지 만들기";

    public void Undo() => variants.Set(angle, before);

    public void Redo() => variants.Set(angle, after);

    public static void Apply(AngleVariants variants, UndoHistory history, int angle, PartVariant? after)
    {
        var before = variants.Get(angle);
        if (before == after)
            return;
        var change = new VariantChange(variants, angle, before, after);
        change.Redo();
        history.Push(change);
    }

    /// <summary>Starting point for a new variant: the base image rotated to <paramref name="angle"/> degrees.</summary>
    public static PartVariant RenderFromBase(PartView view, int angle)
    {
        var (image, pivot) = RotSprite.Rotate(view.Image, view.LocalPivot, (float)(angle * Math.PI / 180));
        return new PartVariant(image, pivot);
    }
}
