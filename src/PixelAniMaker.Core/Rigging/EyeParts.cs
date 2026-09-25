using System.Numerics;
using PixelAniMaker.Core.History;
using PixelAniMaker.Core.Imaging;

namespace PixelAniMaker.Core.Rigging;

/// <summary>
/// Optional eye parts: <c>eye_r</c> and <c>eye_l</c>, detail children of the head, so eyes can be
/// drawn (and swapped or redrawn) apart from the face. Placement follows the head's size: skull height
/// H is measured from the top of the head image to the neck joint, eyes sit 0.36 H above the joint.
/// </summary>
public static class EyeParts
{
    public const string Right = "eye_r";
    public const string Left = "eye_l";
    public const string HeadName = "head";

    private static readonly Rgba EyeColor = new(44, 30, 36);
    private static readonly Rgba ShineColor = new(255, 255, 255);

    public static bool Has(Character character) => character.Find(Right) is not null || character.Find(Left) is not null;

    public static bool CanAdd(Character character) => !Has(character) && character.Find(HeadName) is not null;

    /// <summary>Adds both eyes with a simple starting drawing (front and the near side eye), as one undo step.</summary>
    public static void Add(Character character)
    {
        if (!CanAdd(character))
            return;
        var head = character.Find(HeadName)!;
        var change = new PartsChange(character, "눈 파츠 추가",
            [(Create(character, head, Right), head), (Create(character, head, Left), head)], adding: true);
        change.Redo();
        character.History.Push(change);
    }

    /// <summary>Removes the eye parts (undoable).</summary>
    public static void Remove(Character character)
    {
        var eyes = new[] { character.Find(Right), character.Find(Left) }
            .Where(p => p is { Parent: not null, Children.Count: 0 })
            .Select(p => (p!, p!.Parent!)).ToList();
        if (eyes.Count == 0)
            return;
        var change = new PartsChange(character, "눈 파츠 삭제", eyes, adding: false);
        change.Redo();
        character.History.Push(change);
    }

    private static Part Create(Character character, Part head, string name)
    {
        bool right = name == Right;
        var views = new Dictionary<Direction, PartView>();
        foreach (var direction in character.StoredDirections)
        {
            var headView = head.View(direction);
            float h = SkullHeight(headView);
            var joint = headView.RestPivot;
            float y = joint.Y - 0.36f * h;
            // screen side and visibility: front shows *_r on the left, back hides both,
            // the left-facing side view shows the near (left) eye towards the face
            // a 3/4 view starts like the front or back one (redraw to taste)
            (float x, bool visible) = direction.Fallback() switch
            {
                Direction.Front => (joint.X + (right ? -1 : 1) * 0.24f * h, true),
                Direction.Back => (joint.X + (right ? 1 : -1) * 0.24f * h, false),
                _ => (joint.X - (right ? 0.20f : 0.28f) * h, !right),
            };
            views[direction] = View(character, new Vector2(x, y), h, headView.DrawOrder,
                visible ? direction == Direction.Left ? 0.09f : 0.13f : 0);
        }
        return new Part(name, right ? "눈 R" : "눈 L", views) { IsDetail = true };
    }

    /// <summary>A blank canvas around the eye centre, with a dark oval and a shine when <paramref name="eyeWidth"/> is set.</summary>
    private static PartView View(Character character, Vector2 centre, float h, int drawOrder, float eyeWidth)
    {
        int w = Math.Max(3, (int)MathF.Round(0.30f * h)), hh = Math.Max(3, (int)MathF.Round(0.33f * h));
        var position = new Vector2(MathF.Round(centre.X - w / 2f), MathF.Round(centre.Y - hh / 2f));
        var image = new IndexedImage(w, hh);
        if (eyeWidth > 0)
        {
            int eye = character.Palette.GetOrAdd(EyeColor), shine = character.Palette.GetOrAdd(ShineColor);
            float rx = Math.Max(1f, eyeWidth * h / 2), ry = Math.Max(1.5f, 0.1f * h);
            var c = centre - position;
            for (int y = 0; y < hh; y++)
                for (int x = 0; x < w; x++)
                    if (Square((x + 0.5f - c.X) / rx) + Square((y + 0.5f - c.Y) / ry) <= 1)
                        image.Set(x, y, eye);
            image.Set((int)MathF.Floor(c.X - rx / 2), (int)MathF.Floor(c.Y - ry / 2), shine);
        }
        return new PartView(image, position, centre, drawOrder);
    }

    private static float Square(float v) => v * v;

    /// <summary>From the first drawn row of the head image down to the neck joint.</summary>
    private static float SkullHeight(PartView head)
    {
        var image = head.Image;
        for (int y = 0; y < image.Height; y++)
            for (int x = 0; x < image.Width; x++)
                if (image[x, y] != Palette.TransparentIndex)
                    return Math.Max(6, head.RestPivot.Y - (head.RestPosition.Y + y));
        return 30;
    }
}

/// <summary>Undoable addition or removal of leaf parts (e.g. the eyes).</summary>
public sealed class PartsChange(Character character, string name, IReadOnlyList<(Part Part, Part Parent)> parts, bool adding)
    : IUndoableAction
{
    public string Name => name;

    public void Undo() => Apply(!adding);

    public void Redo() => Apply(adding);

    private void Apply(bool add)
    {
        if (add)
            foreach (var (part, parent) in parts)
                character.AddPart(part, parent);
        else
            foreach (var (part, _) in parts.Reverse())
                character.RemovePart(part);
    }
}
