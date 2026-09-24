using System.Numerics;
using System.Text.Json;
using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.Core.Animation;

/// <summary>Reads animation clips from JSON (the template's animations.json).</summary>
public static class AnimationJson
{
    private sealed record KeySpec(int Frame, string? Easing, float[]? Offset, Dictionary<string, double>? Rotations);

    private sealed record ClipSpec(string Name, int Frames, int Fps, bool Loop, Dictionary<string, List<KeySpec>> Tracks);

    private sealed record FileSpec(List<ClipSpec> Animations);

    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static IReadOnlyList<AnimationClip> Parse(string json)
    {
        var file = JsonSerializer.Deserialize<FileSpec>(json, Options) ?? throw new FormatException("Empty animation file.");
        return file.Animations.Select(ToClip).ToList();
    }

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
        return clip;
    }

    private static PoseData ToPose(KeySpec k) =>
        new(k.Rotations ?? [], k.Offset is [var x, var y] ? new Vector2(x, y) : Vector2.Zero);

    private static Easing ParseEasing(string? name) =>
        name is null ? Easing.EaseInOut
        : Enum.TryParse<Easing>(name, ignoreCase: true, out var e) ? e
        : throw new FormatException($"Unknown easing '{name}'.");
}
