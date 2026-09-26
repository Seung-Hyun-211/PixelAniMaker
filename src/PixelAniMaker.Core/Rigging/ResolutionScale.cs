using System.Numerics;
using PixelAniMaker.Core.Animation;
using PixelAniMaker.Core.History;
using PixelAniMaker.Core.Imaging;

namespace PixelAniMaker.Core.Rigging;

/// <summary>How pixels are enlarged.</summary>
public enum UpscaleMethod
{
    /// <summary>Every pixel becomes a 2×2 block: exactly the same shapes.</summary>
    Nearest,

    /// <summary>Scale2x: diagonal steps are filled in, no new colours.</summary>
    Smooth,
}

/// <summary>
/// Doubles the character's resolution: canvas, every part drawing (all directions, layers and angle images),
/// joints, attachment points, body offsets in poses and keys, frame touch-ups, and the pixel-sized settings
/// (shading width, deform reach). Rotations, drawing order and the palette stay. One undo step.
/// </summary>
public static class ResolutionScale
{
    public const int Factor = 2;

    public static bool CanApply(Character character) =>
        character.Width * Factor <= CanvasResize.MaxSize && character.Height * Factor <= CanvasResize.MaxSize;

    /// <summary>Scales up (undoable); false when the canvas would get too large.</summary>
    public static bool Apply(Character character, IEnumerable<AnimationClip> clips, UpscaleMethod method, UndoHistory history)
    {
        if (!CanApply(character))
            return false;
        history.Do(new ResolutionScaleChange(character, clips.ToList(), method));
        return true;
    }

    public static IndexedImage Upscale(IndexedImage image, UpscaleMethod method)
    {
        if (method == UpscaleMethod.Smooth)
            return Scale2x.Upscale(image);
        var big = new IndexedImage(image.Width * Factor, image.Height * Factor);
        for (int y = 0; y < big.Height; y++)
            for (int x = 0; x < big.Width; x++)
                big.Set(x, y, image[x / Factor, y / Factor]);
        return big;
    }
}

/// <summary>
/// Undoable resolution change. The new drawings are made once; redo puts the new layer objects in and undo
/// puts the old ones back, so edits recorded before (and after) still find the images they changed.
/// </summary>
public sealed class ResolutionScaleChange : IUndoableAction
{
    private sealed record ViewState(
        IReadOnlyList<(PartLayer Layer, string Name, bool Visible)> Layers, Vector2 Position, Vector2 Pivot,
        IReadOnlyDictionary<int, PartVariant> Variants, IReadOnlyDictionary<string, Vector2> Attachments);

    private readonly Character _character;
    private readonly (int Width, int Height) _before, _after;
    private readonly List<(PartView View, ViewState Before, ViewState After)> _views = [];
    private readonly List<(Part Part, SecondarySettings Before, SecondarySettings After)> _secondary = [];
    private readonly (int Before, int After) _shadingWidth;
    private readonly List<(Pose Pose, Vector2 Before)> _poses = [];
    private readonly List<(AnimationClip Clip, Direction Direction, Keyframe Before, Keyframe After)> _keys = [];
    private readonly List<(FrameTouchups Touchups, Direction Direction, int Frame, PixelOverrides Before, PixelOverrides After)> _touchups = [];

    public ResolutionScaleChange(Character character, IReadOnlyList<AnimationClip> clips, UpscaleMethod method)
    {
        const int k = ResolutionScale.Factor;
        _character = character;
        _before = (character.Width, character.Height);
        _after = (character.Width * k, character.Height * k);

        foreach (var view in character.Parts.SelectMany(p => DirectionExtensions.Every.Where(p.HasOwnView).Select(p.View)).Distinct())
        {
            var before = State(view);
            var after = new ViewState(
                before.Layers.Select(l => (new PartLayer(l.Name, ResolutionScale.Upscale(l.Layer.Image, method), l.Visible), l.Name, l.Visible)).ToList(),
                before.Position * k, before.Pivot * k,
                before.Variants.ToDictionary(v => v.Key, v => new PartVariant(ResolutionScale.Upscale(v.Value.Image, method), v.Value.LocalPivot * k)),
                before.Attachments.ToDictionary(a => a.Key, a => a.Value * k));
            _views.Add((view, before, after));
        }
        foreach (var part in character.Parts.Where(p => p.Secondary is { Mode: SecondaryMode.Deform }))
            _secondary.Add((part, part.Secondary!, (part.Secondary! with { Max = part.Secondary!.Max * k }).Clamped()));
        _shadingWidth = (character.Shading.Width, Math.Min(character.Shading.Width * k, ShadingSettings.MaxWidth));
        foreach (var d in DirectionExtensions.Every.Select(d => d.Source()).Distinct())
            _poses.Add((character.PoseFor(d), character.PoseFor(d).Offset));
        foreach (var clip in clips)
        {
            foreach (var d in DirectionExtensions.Every.Where(d => d == d.Source()))
                foreach (var key in clip.Keys(d))
                    _keys.Add((clip, d, key, key with { Pose = key.Pose with { Offset = key.Pose.Offset * k } }));
            foreach (var (d, frame) in clip.Touchups.Frames.ToList())
            {
                var before = clip.Touchups.Get(d, frame);
                var after = new PixelOverrides(before.SelectMany(kv =>
                    from dy in Enumerable.Range(0, k)
                    from dx in Enumerable.Range(0, k)
                    select KeyValuePair.Create((kv.Key.X * k + dx, kv.Key.Y * k + dy), kv.Value)));
                _touchups.Add((clip.Touchups, d, frame, before, after));
            }
        }
    }

    public string Name => "해상도 2배";

    public void Redo() => Apply(after: true);

    public void Undo() => Apply(after: false);

    private static ViewState State(PartView view) => new(view.Layers.Snapshot(), view.RestPosition, view.RestPivot,
        view.Variants.All.ToDictionary(v => v.Key, v => v.Value), view.Attachments.All.ToDictionary(a => a.Key, a => a.Value));

    private void Apply(bool after)
    {
        const int k = ResolutionScale.Factor;
        foreach (var (view, before, scaled) in _views)
        {
            var s = after ? scaled : before;
            view.Layers.Restore(s.Layers);
            view.SetRest(s.Position, s.Pivot);
            foreach (var angle in view.Variants.All.Keys.ToList())
                view.Variants.Set(angle, null);
            foreach (var (angle, variant) in s.Variants)
                view.Variants.Set(angle, variant);
            foreach (var name in view.Attachments.All.Keys.ToList())
                view.Attachments.Remove(name);
            foreach (var (name, point) in s.Attachments)
                view.Attachments.Set(name, point);
        }
        foreach (var (part, before, scaled) in _secondary)
            part.Secondary = after ? scaled : before;
        _character.Shading.Width = after ? _shadingWidth.After : _shadingWidth.Before;
        foreach (var (pose, before) in _poses)
            pose.Offset = after ? before * k : before;
        foreach (var (clip, d, before, scaled) in _keys)
            clip.SetKey(d, after ? scaled : before);
        foreach (var (touchups, d, frame, before, scaled) in _touchups)
            touchups.Set(d, frame, after ? scaled : before);
        var (w, h) = after ? _after : _before;
        _character.SetCanvasSize(w, h, new CanvasShift(0, 0, 0, after ? k : 1f / k));
    }
}
