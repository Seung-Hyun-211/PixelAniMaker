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
/// plays the Left track mirrored). Frames between keys are interpolated.
/// </summary>
public sealed class AnimationClip : INotifyPropertyChanged
{
    private readonly Dictionary<Direction, SortedList<int, Keyframe>> _tracks =
        DirectionExtensions.Stored.ToDictionary(d => d, _ => new SortedList<int, Keyframe>());

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

    /// <summary>The direction's keys in frame order (a copy).</summary>
    /// <summary>Hand-painted pixel fixes on top of the generated frames.</summary>
    public FrameTouchups Touchups { get; } = new();

    public IReadOnlyList<Keyframe> Keys(Direction direction) => [.. _tracks[direction.Source()].Values];

    public Keyframe? KeyAt(Direction direction, int frame) => _tracks[direction.Source()].GetValueOrDefault(frame);

    public void SetKey(Direction direction, Keyframe key)
    {
        _tracks[direction.Source()][key.Frame] = key;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>A copy with the same settings and keys (touch-ups are not copied).</summary>
    public AnimationClip CopyAs(string name)
    {
        var copy = new AnimationClip(name, FrameCount, Fps, Loop);
        foreach (var (direction, track) in _tracks)
            foreach (var key in track.Values)
                copy._tracks[direction][key.Frame] = key;
        return copy;
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
        var keys = _tracks[direction.Source()].Values.Where(k => k.Frame < FrameCount).ToList();
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

    /// <summary>Interpolates rotations along the shorter way round and the offset linearly.</summary>
    public static PoseData Blend(PoseData a, PoseData b, double t)
    {
        var rotations = new Dictionary<string, double>();
        foreach (var part in a.Rotations.Keys.Union(b.Rotations.Keys))
        {
            double from = a.Get(part), delta = Pose.Normalize(b.Get(part) - from);
            rotations[part] = Pose.Normalize(from + delta * t);
        }
        return new PoseData(rotations, Vector2.Lerp(a.Offset, b.Offset, (float)t));
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
        change.Redo();
        history.Push(change);
    }
}
