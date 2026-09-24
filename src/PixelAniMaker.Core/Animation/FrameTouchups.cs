using PixelAniMaker.Core.History;
using PixelAniMaker.Core.Imaging;
using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.Core.Animation;

/// <summary>Hand-painted pixel overrides for one frame: (x, y) → palette index (0 = erase to transparent).</summary>
public sealed class PixelOverrides : Dictionary<(int X, int Y), ushort>
{
    public PixelOverrides() { }

    public PixelOverrides(IEnumerable<KeyValuePair<(int X, int Y), ushort>> pixels) : base(pixels) { }

    public bool SameAs(PixelOverrides other) => Count == other.Count && this.All(kv => other.TryGetValue(kv.Key, out var v) && v == kv.Value);
}

/// <summary>
/// The touch-up layer of a clip: pixel overrides painted on top of generated frames, per direction
/// (all four, since each is a separate row of the sheet) and frame. Kept when frames are regenerated.
/// </summary>
public sealed class FrameTouchups
{
    private readonly Dictionary<(Direction, int), PixelOverrides> _frames = [];

    public event EventHandler? Changed;

    /// <summary>Frames that have overrides.</summary>
    public IEnumerable<(Direction Direction, int Frame)> Frames => _frames.Keys;

    public bool Has(Direction direction, int frame) => _frames.ContainsKey((direction, frame));

    /// <summary>A copy of the frame's overrides (empty when none).</summary>
    public PixelOverrides Get(Direction direction, int frame) =>
        _frames.TryGetValue((direction, frame), out var p) ? new PixelOverrides(p) : [];

    /// <summary>Replaces the frame's overrides; an empty set removes them.</summary>
    public void Set(Direction direction, int frame, PixelOverrides pixels)
    {
        if (Get(direction, frame).SameAs(pixels))
            return;
        if (pixels.Count == 0)
            _frames.Remove((direction, frame));
        else
            _frames[(direction, frame)] = new PixelOverrides(pixels);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Paints the frame's overrides onto a generated frame.</summary>
    public void ApplyTo(CompositeResult frame, Direction direction, int index)
    {
        if (!_frames.TryGetValue((direction, index), out var pixels))
            return;
        foreach (var ((x, y), value) in pixels)
            if ((uint)x < (uint)frame.Width && (uint)y < (uint)frame.Height)
                frame.Indices[y * frame.Width + x] = value;
    }

    /// <summary>The overrides that turn <paramref name="generated"/> into <paramref name="edited"/>.</summary>
    public static PixelOverrides Diff(CompositeResult generated, IndexedImage edited)
    {
        var result = new PixelOverrides();
        for (int y = 0; y < generated.Height; y++)
            for (int x = 0; x < generated.Width; x++)
                if (edited[x, y] != generated.Indices[y * generated.Width + x])
                    result[(x, y)] = (ushort)edited[x, y];
        return result;
    }
}

/// <summary>Undoable replacement of one frame's overrides.</summary>
public sealed class TouchupChange(FrameTouchups touchups, Direction direction, int frame, PixelOverrides before, PixelOverrides after)
    : IUndoableAction
{
    public string Name => "프레임 손보기";

    public void Undo() => touchups.Set(direction, frame, before);

    public void Redo() => touchups.Set(direction, frame, after);

    public static void Apply(FrameTouchups touchups, UndoHistory history, Direction direction, int frame, PixelOverrides after)
    {
        var before = touchups.Get(direction, frame);
        if (before.SameAs(after))
            return;
        var change = new TouchupChange(touchups, direction, frame, before, after);
        change.Redo();
        history.Push(change);
    }
}
