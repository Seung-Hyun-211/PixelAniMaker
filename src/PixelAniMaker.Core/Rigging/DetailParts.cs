using PixelAniMaker.Core.History;

namespace PixelAniMaker.Core.Rigging;

/// <summary>Adding and removing optional leaf parts (eyes, bust) as single undo steps.</summary>
public static class DetailParts
{
    /// <summary>Adds the parts under their parents as one undo step.</summary>
    public static void Add(Character character, string undoName, IReadOnlyList<(Part Part, Part Parent)> parts)
    {
        var change = new PartsChange(character, undoName, parts, adding: true);
        change.Redo();
        character.History.Push(change);
    }

    /// <summary>Removes the named leaf parts that exist, as one undo step (nothing when none exists).</summary>
    public static void Remove(Character character, string undoName, params string[] names)
    {
        var parts = names.Select(character.Find)
            .Where(p => p is { Parent: not null, Children.Count: 0 })
            .Select(p => (p!, p!.Parent!)).ToList();
        if (parts.Count == 0)
            return;
        var change = new PartsChange(character, undoName, parts, adding: false);
        change.Redo();
        character.History.Push(change);
    }
}

/// <summary>Undoable addition or removal of leaf parts (e.g. the eyes).</summary>
public sealed class PartsChange(Character character, string name, IReadOnlyList<(Part Part, Part Parent)> parts, bool adding)
    : IUndoableAction
{
    public string Name => name;

    public void Undo() => Apply(!adding);

    public void Redo() => Apply(adding);

    private void Apply(bool add)
    {
        if (add)
            foreach (var (part, parent) in parts)
                character.AddPart(part, parent);
        else
            foreach (var (part, _) in parts.Reverse())
                character.RemovePart(part);
    }
}
