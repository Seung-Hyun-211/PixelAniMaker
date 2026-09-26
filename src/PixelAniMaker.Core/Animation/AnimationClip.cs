using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Numerics;
using PixelAniMaker.Core.History;
using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.Core.Animation;

/// <summary>How a keyframe blends into the next one.</summary>
public enum Easing
{
    EaseInOut,
    Linear,
    Step,
}

public static class EasingExtensions
{
    public static double Apply(this Easing easing, double t) => easing switch
    {
        Easing.Step => 0,
        Easing.Linear => t,
        _ => t * t * (3 - 2 * t),
    };

    public static string Label(this Easing easing) => easing switch
    {
        Easing.Step => "계단",
        Easing.Linear => "선형",
        _ => "이징",
    };
}

/// <summary>A stored pose at one frame.</summary>
public sealed record Keyframe(int Frame, PoseData Pose, Easing Easing = Easing.EaseInOut);

/// <summary>
/// A named animation: frame count, speed and one keyframe track per stored direction (the Right view
/// plays the Left track mirrored). Frames between keys are interpolated. The 3/4 tracks are optional:
/// while one is empty, that view plays the front or back track.
/// </summary>
public sealed class AnimationClip : INotifyPropertyChanged
{
    private readonly Dictionary<Direction, SortedList<int, Keyframe>> _tracks =
        DirectionExtensions.Stored.Concat(DirectionExtensions.ThreeQuarterStored)
            .ToDictionary(d => d, _ => new SortedList<int, Keyframe>());

    private readonly SortedDictionary<int, int> _holds = [];

    private string _name;
    private int _frameCount;
    private int _fps;
    private bool _loop;

    public AnimationClip(string name, int frameCount, int fps = 10, bool loop = true)
    {
        _name = name;
        _frameCount = Math.Max(1, frameCount);
        _fps = Math.Max(1, fps);
        _loop = loop;
    }

    /// <summary>Raised when keys or settings change.</summary>
    public event EventHandler? Changed;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name { get => _name; set => Set(ref _name, value); }
    public int FrameCount { get => _frameCount; set => Set(ref _frameCount, Math.Max(1, value)); }
    public int Fps { get => _fps; set => Set(ref _fps, Math.Max(1, value)); }
    public bool Loop { get => _loop; set => Set(ref _loop, value); }

    /// <summary>Longest hold a frame can have, in ticks.</summary>
    public const int MaxHold = 16;

    /// <summary>How many ticks (1/<see cref="Fps"/> s each) <paramref name="frame"/> stays on screen; 1 unless held longer.</summary>
    public int Hold(int frame) => _holds.GetValueOrDefault(frame, 1);

    /// <summary>Frames held longer than one tick (frame → ticks), inside the clip only.</summary>
    public IReadOnlyDictionary<int, int> Holds => _holds.Where(h => h.Key < FrameCount).ToDictionary(h => h.Key, h => h.Value);

    public void SetHold(int frame, int ticks)
    {
        ticks = Math.Clamp(ticks, 1, MaxHold);
        if (Hold(frame) == ticks)
            return;
        if (ticks == 1)
            _holds.Remove(frame);
        else
            _holds[frame] = ticks;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Holds)));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Display time of <paramref name="frame"/> in milliseconds (whole ms, as in sheet JSON).</summary>
    public int DurationMs(int frame) => Hold(frame) * 1000 / Fps;

    /// <summary>The direction's keys in frame order (a copy).</summary>
    /// <summary>Hand-painted pixel fixes on top of the generated frames.</summary>
    public FrameTouchups Touchups { get; } = new();

    public IReadOnlyList<Keyframe> Keys(Direction direction) => [.. _tracks[direction.Source()].Values];

    /// <summary>
    /// Keys past the last frame, left over after the clip was shortened. They are kept (lengthening the clip
    /// brings them back) but not played; see <see cref="KeysPastEndChange"/> to delete them.
    /// </summary>
    public IReadOnlyList<(Direction Direction, Keyframe Key)> KeysPastEnd =>
        _tracks.SelectMany(t => t.Value.Values.Where(k => k.Frame >= FrameCount).Select(k => (t.Key, k))).ToList();

    /// <summary>True when any 3/4 track has keys or any 3/4 frame is touched up (the file then needs format 2).</summary>
    public bool HasThreeQuarterData =>
        DirectionExtensions.ThreeQuarterStored.Any(d => _tracks[d].Count > 0) || Touchups.Frames.Any(f => f.Direction.IsThreeQuarter());

    /// <summary>The track played for a direction: its own, or for an empty 3/4 track the front/back one.</summary>
    private SortedList<int, Keyframe> PlayedTrack(Direction direction)
    {
        var track = _tracks[direction.Source()];
        return track.Count == 0 && direction.IsThreeQuarter() ? _tracks[direction.Fallback()] : track;
    }

    public Keyframe? KeyAt(Direction direction, int frame) => _tracks[direction.Source()].GetValueOrDefault(frame);

    public void SetKey(Direction direction, Keyframe key)
    {
        _tracks[direction.Source()][key.Frame] = key;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>A copy with the same settings, frame holds and keys (touch-ups are not copied).</summary>
    public AnimationClip CopyAs(string name)
    {
        var copy = new AnimationClip(name, FrameCount, Fps, Loop);
        foreach (var (direction, track) in _tracks)
            foreach (var key in track.Values)
                copy._tracks[direction][key.Frame] = key;
        foreach (var (frame, ticks) in _holds)
            copy._holds[frame] = ticks;
        return copy;
    }

    /// <summary>
    /// Multiplies every key's body offset (rounded to whole pixels), e.g. to fit a clip made for a smaller
    /// body. Not recorded: meant for clips being set up, before they are shown.
    /// </summary>
    public void ScaleOffsets(float factor)
    {
        foreach (var track in _tracks.Values)
            foreach (var key in track.Values.ToList())
                track[key.Frame] = key with { Pose = key.Pose with { Offset = new Vector2(MathF.Round(key.Pose.Offset.X * factor), MathF.Round(key.Pose.Offset.Y * factor)) } };
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void RemoveKey(Direction direction, int frame)
    {
        if (_tracks[direction.Source()].Remove(frame))
            Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The pose at <paramref name="frame"/>, blended between the surrounding keys.</summary>
    public PoseData Evaluate(Direction direction, int frame)
    {
        // keys past the end (left over after shortening the clip) are kept but not played
        var keys = PlayedTrack(direction).Values.Where(k => k.Frame < FrameCount).ToList();
        if (keys.Count == 0)
            return PoseData.Rest;

        int next = FirstIndexAfter(keys, frame);
        Keyframe from, to;
        int fromFrame, toFrame;
        if (next == 0)
        {
            if (!Loop)
                return keys[0].Pose;
            (from, fromFrame) = (keys[^1], keys[^1].Frame - FrameCount);
            (to, toFrame) = (keys[0], keys[0].Frame);
        }
        else if (next == keys.Count)
        {
            if (!Loop || keys.Count == 1)
                return keys[^1].Pose;
            (from, fromFrame) = (keys[^1], keys[^1].Frame);
            (to, toFrame) = (keys[0], keys[0].Frame + FrameCount);
        }
        else
        {
            (from, fromFrame) = (keys[next - 1], keys[next - 1].Frame);
            (to, toFrame) = (keys[next], keys[next].Frame);
        }

        if (frame == fromFrame || toFrame == fromFrame)
            return from.Pose;
        double t = from.Easing.Apply((double)(frame - fromFrame) / (toFrame - fromFrame));
        return Blend(from.Pose, to.Pose, t);
    }

    private static int FirstIndexAfter(IList<Keyframe> keys, int frame)
    {
        int i = 0;
        while (i < keys.Count && keys[i].Frame <= frame)
            i++;
        return i;
    }

    /// <summary>
    /// Interpolates rotations along the shorter way round and the offset linearly; drawing order changes
    /// cannot blend, so the earlier pose's hold until the next key.
    /// </summary>
    public static PoseData Blend(PoseData a, PoseData b, double t)
    {
        var rotations = new Dictionary<string, double>();
        foreach (var part in a.Rotations.Keys.Union(b.Rotations.Keys))
        {
            double from = a.Get(part), delta = Pose.Normalize(b.Get(part) - from);
            rotations[part] = Pose.Normalize(from + delta * t);
        }
        return new PoseData(rotations, Vector2.Lerp(a.Offset, b.Offset, (float)t), a.Order);
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? property = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public override string ToString() => Name;
}

/// <summary>Undoable add/replace/remove of one keyframe.</summary>
public sealed class KeyframeChange(AnimationClip clip, Direction direction, int frame, Keyframe? before, Keyframe? after)
    : IUndoableAction
{
    public string Name => after is null ? "키 삭제" : "키 저장";

    public void Undo() => Apply(before);

    public void Redo() => Apply(after);

    private void Apply(Keyframe? key)
    {
        if (key is null)
            clip.RemoveKey(direction, frame);
        else
            clip.SetKey(direction, key);
    }

    /// <summary>Sets (or removes, when <paramref name="key"/> is null) the key at <paramref name="frame"/> as one undo step.</summary>
    public static void Apply(AnimationClip clip, UndoHistory history, Direction direction, int frame, Keyframe? key)
    {
        var before = clip.KeyAt(direction, frame);
        if (before == key || (before is null && key is null))
            return;
        var change = new KeyframeChange(clip, direction, frame, before, key);
        history.Do(change);
    }
}

/// <summary>Undoable deletion of a clip's keys past its last frame (<see cref="AnimationClip.KeysPastEnd"/>).</summary>
public sealed class KeysPastEndChange(AnimationClip clip, IReadOnlyList<(Direction Direction, Keyframe Key)> keys) : IUndoableAction
{
    public string Name => "범위 밖 키 지우기";

    public void Undo()
    {
        foreach (var (direction, key) in keys)
            clip.SetKey(direction, key);
    }

    public void Redo()
    {
        foreach (var (direction, key) in keys)
            clip.RemoveKey(direction, key.Frame);
    }

    /// <summary>Deletes the keys past the end as one undo step; false when there are none.</summary>
    public static bool Apply(AnimationClip clip, UndoHistory history)
    {
        var keys = clip.KeysPastEnd;
        if (keys.Count == 0)
            return false;
        history.Do(new KeysPastEndChange(clip, keys));
        return true;
    }
}
