using System.Numerics;
using PixelAniMaker.Core.Editing;

namespace PixelAniMaker.Core.Rigging;

/// <summary>Left/right counterparts for symmetric editing in the front and back views.</summary>
public static class Symmetry
{
    /// <summary>
    /// The part mirrored across the body centre: *_r ↔ *_l, centre parts (head, chest …) map to
    /// themselves.
    /// </summary>
    public static Part Counterpart(Character character, Part part)
    {
        string name = part.Name.EndsWith("_r", StringComparison.Ordinal) ? part.Name[..^2] + "_l"
            : part.Name.EndsWith("_l", StringComparison.Ordinal) ? part.Name[..^2] + "_r"
            : part.Name;
        return character.Find(name) ?? part;
    }

    /// <summary>
    /// Mirror for drawing on <paramref name="part"/>: an image pixel is taken to the canvas, reflected
    /// across the canvas centre line and mapped into the counterpart's image. Null for side views.
    /// </summary>
    public static PixelMirror? For(Character character, Part part, Direction direction, int layer = 0)
    {
        if (!Applies(direction))
            return null;
        var transforms = character.ComputeTransforms(direction);
        var from = transforms[part];
        var to = transforms[Counterpart(character, part)];
        int width = character.Width;
        return new PixelMirror(to.EditImage(layer), (x, y) =>
        {
            var c = from.ToCanvas(new Vector2(x + 0.5f, y + 0.5f));
            var p = to.ToLocal(new Vector2(width - c.X, c.Y));
            return ((int)MathF.Floor(p.X), (int)MathF.Floor(p.Y));
        });
    }

    /// <summary>Symmetric editing only makes sense when the body faces the viewer or away.</summary>
    public static bool Applies(Direction direction) => direction is Direction.Front or Direction.Back;
}
