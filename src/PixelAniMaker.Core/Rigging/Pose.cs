using System.Numerics;

namespace PixelAniMaker.Core.Rigging;

/// <summary>An immutable pose: joint rotations (degrees, clockwise on screen) plus a whole-body offset in pixels.</summary>
public sealed record PoseData(IReadOnlyDictionary<string, double> Rotations, Vector2 Offset)
{
    public static PoseData Rest { get; } = new(new Dictionary<string, double>(), Vector2.Zero);

    public double Get(string part) => Rotations.GetValueOrDefault(part);

    /// <summary>Content equality (records compare dictionaries by reference).</summary>
    public bool SameAs(PoseData other) =>
        Offset == other.Offset
        && Rotations.Count(kv => kv.Value != 0) == other.Rotations.Count(kv => kv.Value != 0)
        && Rotations.All(kv => other.Get(kv.Key) == kv.Value);
}

/// <summary>The editable pose of one direction. Missing parts are at 0°.</summary>
public sealed class Pose
{
    private readonly Dictionary<string, double> _rotations = [];
    private Vector2 _offset;

    public event EventHandler? Changed;

    public double Get(string part) => _rotations.GetValueOrDefault(part);

    /// <summary>Whole-body offset in pixels, applied to the root joint.</summary>
    public Vector2 Offset
    {
        get => _offset;
        set
        {
            if (_offset == value)
                return;
            _offset = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Set(string part, double degrees)
    {
        degrees = Normalize(degrees);
        if (Get(part) == degrees)
            return;
        if (degrees == 0)
            _rotations.Remove(part);
        else
            _rotations[part] = degrees;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public PoseData Snapshot() => new(new Dictionary<string, double>(_rotations), _offset);

    /// <summary>Replaces the whole pose (raises <see cref="Changed"/> once, only if something differs).</summary>
    public void Restore(PoseData data)
    {
        if (Snapshot().SameAs(data))
            return;
        _rotations.Clear();
        foreach (var (part, degrees) in data.Rotations)
            if (degrees != 0)
                _rotations[part] = Normalize(degrees);
        _offset = data.Offset;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Maps any angle into (-180, 180].</summary>
    public static double Normalize(double degrees)
    {
        degrees %= 360;
        if (degrees > 180) degrees -= 360;
        if (degrees <= -180) degrees += 360;
        return degrees;
    }
}
