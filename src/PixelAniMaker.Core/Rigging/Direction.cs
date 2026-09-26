namespace PixelAniMaker.Core.Rigging;

/// <summary>
/// The views a character is drawn in. Left/Right name the way the character faces. The 3/4 views
/// (<see cref="FrontLeft"/> … <see cref="BackRight"/>) are optional per character; new values are only
/// ever added at the end so the order of the existing ones (sheet rows, saved touch-ups) never changes.
/// </summary>
public enum Direction
{
    Front,
    Left,
    Right,
    Back,

    /// <summary>3/4 view from the front, facing screen-left.</summary>
    FrontLeft,

    /// <summary>3/4 view from the front, facing screen-right (mirrors <see cref="FrontLeft"/>).</summary>
    FrontRight,

    /// <summary>3/4 view from the back, facing screen-left.</summary>
    BackLeft,

    /// <summary>3/4 view from the back, facing screen-right (mirrors <see cref="BackLeft"/>).</summary>
    BackRight,
}

public static class DirectionExtensions
{
    /// <summary>The directions that store their own images and pose (Right mirrors Left).</summary>
    public static IReadOnlyList<Direction> Stored { get; } = [Direction.Front, Direction.Left, Direction.Back];

    /// <summary>The four classic directions in sheet order.</summary>
    public static IReadOnlyList<Direction> All { get; } = [Direction.Front, Direction.Left, Direction.Right, Direction.Back];

    /// <summary>The stored 3/4 directions (their right-facing twins are mirrored).</summary>
    public static IReadOnlyList<Direction> ThreeQuarterStored { get; } = [Direction.FrontLeft, Direction.BackLeft];

    /// <summary>The 3/4 directions in sheet order, after the classic four.</summary>
    public static IReadOnlyList<Direction> ThreeQuarter { get; } =
        [Direction.FrontLeft, Direction.FrontRight, Direction.BackLeft, Direction.BackRight];

    /// <summary>Every direction a character can have, in sheet order.</summary>
    public static IReadOnlyList<Direction> Every { get; } = [.. All, .. ThreeQuarter];

    /// <summary>The direction whose data is shown, possibly mirrored.</summary>
    public static Direction Source(this Direction d) => d switch
    {
        Direction.Right => Direction.Left,
        Direction.FrontRight => Direction.FrontLeft,
        Direction.BackRight => Direction.BackLeft,
        _ => d,
    };

    /// <summary>True when the view is the horizontal mirror of <see cref="Source"/>.</summary>
    public static bool IsMirrored(this Direction d) => d is Direction.Right or Direction.FrontRight or Direction.BackRight;

    public static bool IsThreeQuarter(this Direction d) => d >= Direction.FrontLeft;

    /// <summary>
    /// What a 3/4 view falls back to when the character has no picture or keys for it: the front
    /// or back view (the right-facing ones are still mirrored when drawn). Classic directions return themselves.
    /// </summary>
    public static Direction Fallback(this Direction d) => d switch
    {
        Direction.FrontLeft or Direction.FrontRight => Direction.Front,
        Direction.BackLeft or Direction.BackRight => Direction.Back,
        _ => d,
    };

    /// <summary>Lower-case name used as the key in project files and sheet metadata.</summary>
    public static string Key(this Direction d) => d.ToString().ToLowerInvariant();

    public static string Label(this Direction d) => d switch
    {
        Direction.Front => "정면",
        Direction.Left => "좌측면",
        Direction.Right => "우측면",
        Direction.Back => "후면",
        Direction.FrontLeft => "앞 반측면 (좌)",
        Direction.FrontRight => "앞 반측면 (우)",
        Direction.BackLeft => "뒤 반측면 (좌)",
        _ => "뒤 반측면 (우)",
    };
}
