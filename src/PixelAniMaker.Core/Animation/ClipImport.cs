using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.Core.Animation;

/// <summary>Clips copied from another project, and what did not carry over.</summary>
/// <param name="UnknownParts">Part names the keys rotate or reorder that the target character does not have (ignored when played).</param>
/// <param name="DroppedThreeQuarter">3/4 keys were left out because the target character has no 3/4 views.</param>
public sealed record ClipImportResult(IReadOnlyList<AnimationClip> Clips, IReadOnlyList<string> UnknownParts,
    bool DroppedThreeQuarter = false);

/// <summary>
/// Reuses animations between characters. Keys refer to parts by name, so clips move between any
/// characters built from the same skeleton; hand-painted touch-ups belong to the other character's
/// pixels and are not copied.
/// </summary>
public static class ClipImport
{
    public static ClipImportResult Import(IEnumerable<AnimationClip> clips, IEnumerable<string> takenNames, Character target)
    {
        var taken = takenNames.ToHashSet();
        var copies = new List<AnimationClip>();
        foreach (var clip in clips)
        {
            string name = UniqueName(clip.Name, taken);
            taken.Add(name);
            copies.Add(clip.CopyAs(name));
        }
        bool dropped = false;
        if (!target.HasThreeQuarter)
            foreach (var copy in copies)
                dropped |= ThreeQuarterViews.StripFrom(copy);
        var unknown = copies
            .SelectMany(c => target.StoredDirections.SelectMany(c.Keys))
            .SelectMany(k => k.Pose.Rotations.Where(r => r.Value != 0).Select(r => r.Key)
                .Concat(k.Pose.Order?.SelectMany(o => new[] { o.Key, o.Value.Anchor }) ?? []))
            .Distinct()
            .Where(name => target.Find(name) is null)
            .Order(StringComparer.Ordinal)
            .ToList();
        return new ClipImportResult(copies, unknown, dropped);
    }

    /// <summary><paramref name="name"/>, or "name (2)", "name (3)" … when it is taken.</summary>
    public static string UniqueName(string name, IReadOnlySet<string> taken)
    {
        if (!taken.Contains(name))
            return name;
        int i = 2;
        while (taken.Contains($"{name} ({i})"))
            i++;
        return $"{name} ({i})";
    }
}
