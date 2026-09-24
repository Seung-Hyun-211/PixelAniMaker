namespace PixelAniMaker.Core.Rigging;

/// <summary>Joint rotations in degrees (clockwise on screen) keyed by part name. Missing parts are at 0.</summary>
public sealed class Pose
{
    private readonly Dictionary<string, double> _rotations = [];

    public event EventHandler? Changed;

    public double Get(string part) => _rotations.GetValueOrDefault(part);

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

    public IReadOnlyDictionary<string, double> Snapshot() => new Dictionary<string, double>(_rotations);

    /// <summary>Replaces every rotation with <paramref name="snapshot"/> (empty = rest pose).</summary>
    public void Restore(IReadOnlyDictionary<string, double> snapshot)
    {
        if (_rotations.Count == snapshot.Count && snapshot.All(kv => _rotations.GetValueOrDefault(kv.Key) == kv.Value))
            return;
        _rotations.Clear();
        foreach (var (part, degrees) in snapshot)
            if (degrees != 0)
                _rotations[part] = Normalize(degrees);
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
