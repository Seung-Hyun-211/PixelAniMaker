using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.Core.Animation;

/// <summary>Clips copied from another project, and what did not carry over.</summary>
/// <param name="UnknownParts">Part names the keys rotate that the target character does not have (ignored when played).</param>
public sealed record ClipImportResult(IReadOnlyList<AnimationClip> Clips, IReadOnlyList<string> UnknownParts);

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
        var unknown = copies
            .SelectMany(c => DirectionExtensions.Stored.SelectMany(c.Keys))
            .SelectMany(k => k.Pose.Rotations.Where(r => r.Value != 0).Select(r => r.Key))
            .Distinct()
            .Where(name => target.Find(name) is null)
            .Order(StringComparer.Ordinal)
            .ToList();
        return new ClipImportResult(copies, unknown);
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
