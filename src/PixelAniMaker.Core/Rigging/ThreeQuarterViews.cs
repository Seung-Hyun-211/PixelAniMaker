using PixelAniMaker.Core.Animation;
using PixelAniMaker.Core.History;

namespace PixelAniMaker.Core.Rigging;

/// <summary>
/// Turns a character's 3/4 views on or off. On: every part gets a front-left view copied from its
/// front view and a back-left view copied from its back view, to be redrawn (the right-facing ones
/// mirror them). Off: those views, separately drawn right-facing 3/4 views, and the clips' 3/4 keys
/// and touch-ups are removed. Both are one undo step.
/// </summary>
public static class ThreeQuarterViews
{
    public static void Enable(Character character, UndoHistory history)
    {
        if (character.HasThreeQuarter)
            return;
        var after = character.Parts.ToDictionary(p => p, p => new Dictionary<Direction, PartView>
        {
            [Direction.FrontLeft] = p.View(Direction.Front).Copy(),
            [Direction.BackLeft] = p.View(Direction.Back).Copy(),
        });
        Apply(new ThreeQuarterChange(character, "반측면 추가", Snapshot(character), after, [], on: true), history);
    }

    public static void Disable(Character character, IEnumerable<AnimationClip> clips, UndoHistory history)
    {
        var clipData = clips.Where(c => c.HasThreeQuarterData).Select(ClipThreeQuarter.Take).ToList();
        if (!character.HasThreeQuarter && clipData.Count == 0)
            return;
        var after = character.Parts.ToDictionary(p => p, _ => new Dictionary<Direction, PartView>());
        Apply(new ThreeQuarterChange(character, "반측면 지우기", Snapshot(character), after, clipData, on: false), history);
    }

    /// <summary>Removes a clip's 3/4 keys and touch-ups (for clips moved to a character without 3/4 views).</summary>
    public static bool StripFrom(AnimationClip clip)
    {
        if (!clip.HasThreeQuarterData)
            return false;
        ClipThreeQuarter.Take(clip).Clear();
        return true;
    }

    private static void Apply(ThreeQuarterChange change, UndoHistory history)
    {
        change.Redo();
        history.Push(change);
    }

    private static Dictionary<Part, Dictionary<Direction, PartView>> Snapshot(Character character) =>
        character.Parts.ToDictionary(p => p, p => DirectionExtensions.ThreeQuarter.Where(p.HasOwnView)
            .ToDictionary(d => d, p.View));
}

/// <summary>A clip's 3/4 keys and touch-ups, so they can be removed and put back.</summary>
internal sealed record ClipThreeQuarter(AnimationClip Clip, IReadOnlyList<(Direction, Keyframe)> Keys,
    IReadOnlyList<(Direction, int, PixelOverrides)> Touchups)
{
    public static ClipThreeQuarter Take(AnimationClip clip) => new(clip,
        DirectionExtensions.ThreeQuarterStored.SelectMany(d => clip.Keys(d).Select(k => (d, k))).ToList(),
        clip.Touchups.Frames.Where(f => f.Direction.IsThreeQuarter()).ToList()
            .Select(f => (f.Direction, f.Frame, clip.Touchups.Get(f.Direction, f.Frame))).ToList());

    public void Clear()
    {
        foreach (var (d, key) in Keys)
            Clip.RemoveKey(d, key.Frame);
        foreach (var (d, frame, _) in Touchups)
            Clip.Touchups.Set(d, frame, []);
    }

    public void Restore()
    {
        foreach (var (d, key) in Keys)
            Clip.SetKey(d, key);
        foreach (var (d, frame, pixels) in Touchups)
            Clip.Touchups.Set(d, frame, pixels);
    }
}

/// <summary>Undoable switch of the 3/4 views (and, when turning off, the clips' 3/4 data).</summary>
internal sealed class ThreeQuarterChange(
    Character character, string name,
    Dictionary<Part, Dictionary<Direction, PartView>> before,
    Dictionary<Part, Dictionary<Direction, PartView>> after,
    IReadOnlyList<ClipThreeQuarter> clips, bool on) : IUndoableAction
{
    private readonly bool _wasOn = character.HasThreeQuarter;

    public string Name => name;

    public void Redo()
    {
        SetViews(after);
        foreach (var clip in clips)
            clip.Clear();
        character.HasThreeQuarter = on;
    }

    public void Undo()
    {
        SetViews(before);
        foreach (var clip in clips)
            clip.Restore();
        character.HasThreeQuarter = _wasOn;
    }

    private static void SetViews(Dictionary<Part, Dictionary<Direction, PartView>> views)
    {
        foreach (var (part, byDirection) in views)
            foreach (var d in DirectionExtensions.ThreeQuarter)
                part.SetView(d, byDirection.GetValueOrDefault(d));
    }
}
