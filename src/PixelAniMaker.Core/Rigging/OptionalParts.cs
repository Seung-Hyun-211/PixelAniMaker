using PixelAniMaker.Core.History;

namespace PixelAniMaker.Core.Rigging;

/// <summary>Adding and removing optional parts (eyes, bust, added parts) as single undo steps.</summary>
public static class OptionalParts
{
    /// <summary>Adds the parts under their parents (listed parent first) as one undo step.</summary>
    public static void Add(Character character, string undoName, IReadOnlyList<(Part Part, Part Parent)> parts) =>
        character.History.Do(new PartsChange(character, undoName, parts, adding: true));

    /// <summary>Removes the parts (listed parent first, each with its parent) as one undo step.</summary>
    public static void Remove(Character character, string undoName, IReadOnlyList<Part> parts) =>
        character.History.Do(new PartsChange(character, undoName, parts.Select(p => (p, p.Parent!)).ToList(), adding: false));

    /// <summary>Removes the named leaf parts that exist, as one undo step (nothing when none exists).</summary>
    public static void Remove(Character character, string undoName, params string[] names)
    {
        var parts = names.Select(character.Find).OfType<Part>().Where(p => p is { Parent: not null, Children.Count: 0 }).ToList();
        if (parts.Count > 0)
            Remove(character, undoName, parts);
    }
}

/// <summary>
/// Undoable addition or removal of parts (e.g. the eyes, or an added tail's links), listed parent first. Parts removed and put
/// back return to their places in the part list.
/// </summary>
public sealed class PartsChange(Character character, string name, IReadOnlyList<(Part Part, Part Parent)> parts, bool adding)
    : IUndoableAction
{
    private readonly Dictionary<Part, int> _indices = [];

    public string Name => name;

    public void Undo() => Apply(!adding);

    public void Redo() => Apply(adding);

    private void Apply(bool add)
    {
        if (add)
        {
            foreach (var (part, parent) in parts.OrderBy(p => _indices.GetValueOrDefault(p.Part, int.MaxValue)))
                character.AddPart(part, parent, _indices.TryGetValue(part, out var i) ? i : null);
        }
        else
        {
            foreach (var (part, _) in parts)
                _indices[part] = character.IndexOf(part);
            foreach (var (part, _) in parts.Reverse())
                character.RemovePart(part);
        }
    }
}
