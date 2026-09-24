namespace PixelAniMaker.Core.Rigging;

/// <summary>The four views a character is drawn in. Left/Right name the way the character faces.</summary>
public enum Direction
{
    Front,
    Left,
    Right,
    Back,
}

public static class DirectionExtensions
{
    /// <summary>The directions that store their own images and pose (Right mirrors Left).</summary>
    public static IReadOnlyList<Direction> Stored { get; } = [Direction.Front, Direction.Left, Direction.Back];

    public static IReadOnlyList<Direction> All { get; } = [Direction.Front, Direction.Left, Direction.Right, Direction.Back];

    /// <summary>The direction whose data is shown, possibly mirrored.</summary>
    public static Direction Source(this Direction d) => d == Direction.Right ? Direction.Left : d;

    /// <summary>True when the view is the horizontal mirror of <see cref="Source"/>.</summary>
    public static bool IsMirrored(this Direction d) => d == Direction.Right;

    public static string Label(this Direction d) => d switch
    {
        Direction.Front => "정면",
        Direction.Left => "좌측면",
        Direction.Right => "우측면",
        _ => "후면",
    };
}
