using System.Numerics;
using System.Text.Encodings.Web;
using System.Text.Json;
using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.Core.Animation;

/// <summary>Reads and writes animation clips as JSON (template animations.json and project files).</summary>
public static class AnimationJson
{
    private sealed record KeySpec(int Frame, string? Easing, float[]? Offset, Dictionary<string, double>? Rotations);

    /// <summary>One touched-up frame: pixels as [x, y, paletteIndex] (index 0 erases).</summary>
    private sealed record TouchupSpec(string Direction, int Frame, List<int[]> Pixels);

    private sealed record ClipSpec(string Name, int Frames, int Fps, bool Loop, Dictionary<string, List<KeySpec>> Tracks,
        List<TouchupSpec>? Touchups = null);

    private sealed record FileSpec(List<ClipSpec> Animations);

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static IReadOnlyList<AnimationClip> Parse(string json)
    {
        var file = JsonSerializer.Deserialize<FileSpec>(json, Options) ?? throw new FormatException("Empty animation file.");
        return file.Animations.Select(ToClip).ToList();
    }

    public static string Serialize(IEnumerable<AnimationClip> clips) =>
        JsonSerializer.Serialize(new FileSpec(clips.Select(ToSpec).ToList()), Options);

    private static AnimationClip ToClip(ClipSpec spec)
    {
        var clip = new AnimationClip(spec.Name, spec.Frames, spec.Fps, spec.Loop);
        foreach (var (directionName, keys) in spec.Tracks)
        {
            if (!Enum.TryParse<Direction>(directionName, ignoreCase: true, out var direction) || direction.IsMirrored())
                throw new FormatException($"Unknown or derived direction '{directionName}' in '{spec.Name}'.");
            foreach (var k in keys)
                clip.SetKey(direction, new Keyframe(k.Frame, ToPose(k), ParseEasing(k.Easing)));
        }
        foreach (var t in spec.Touchups ?? [])
        {
            if (!Enum.TryParse<Direction>(t.Direction, ignoreCase: true, out var direction))
                throw new FormatException($"Unknown direction '{t.Direction}' in touch-ups of '{spec.Name}'.");
            clip.Touchups.Set(direction, t.Frame, new PixelOverrides(
                t.Pixels.Select(p => KeyValuePair.Create((p[0], p[1]), (ushort)p[2]))));
        }
        return clip;
    }

    private static ClipSpec ToSpec(AnimationClip clip) => new(clip.Name, clip.FrameCount, clip.Fps, clip.Loop,
        DirectionExtensions.Stored.ToDictionary(
            d => d.ToString().ToLowerInvariant(),
            d => clip.Keys(d).Select(k => new KeySpec(
                k.Frame,
                k.Easing.ToString(),
                k.Pose.Offset == Vector2.Zero ? null : [k.Pose.Offset.X, k.Pose.Offset.Y],
                k.Pose.Rotations.Where(r => r.Value != 0).ToDictionary(r => r.Key, r => r.Value))).ToList()),
        clip.Touchups.Frames.Any()
            ? clip.Touchups.Frames.OrderBy(f => f.Direction).ThenBy(f => f.Frame)
                .Select(f => new TouchupSpec(f.Direction.ToString().ToLowerInvariant(), f.Frame,
                    clip.Touchups.Get(f.Direction, f.Frame).Select(p => new[] { p.Key.X, p.Key.Y, (int)p.Value }).ToList()))
                .ToList()
            : null);

    private static PoseData ToPose(KeySpec k) =>
        new(k.Rotations ?? [], k.Offset is [var x, var y] ? new Vector2(x, y) : Vector2.Zero);

    private static Easing ParseEasing(string? name) =>
        name is null ? Easing.EaseInOut
        : Enum.TryParse<Easing>(name, ignoreCase: true, out var e) ? e
        : throw new FormatException($"Unknown easing '{name}'.");
}
