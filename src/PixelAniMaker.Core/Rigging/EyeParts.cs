using System.Numerics;
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
        OptionalParts.Add(character, "눈 파츠 추가", [(Create(character, head, Right), head), (Create(character, head, Left), head)]);
    }

    /// <summary>Removes the eye parts (undoable).</summary>
    public static void Remove(Character character) => OptionalParts.Remove(character, "눈 파츠 삭제", Right, Left);

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
            // screen side, visibility and width: front shows *_r on the left; back and back-3/4 hide both;
            // the left-facing side view shows the near (left) eye towards the face; the front-3/4 view
            // (facing screen-left) moves both towards the face side, the far (right) eye narrower
            (float x, float width) = direction switch
            {
                Direction.Front => (joint.X + (right ? -1 : 1) * 0.24f * h, 0.13f),
                Direction.FrontLeft => (joint.X - 0.14f * h + (right ? -0.20f : 0.20f) * h, right ? 0.08f : 0.13f),
                Direction.Left => (joint.X - (right ? 0.20f : 0.28f) * h, right ? 0 : 0.09f),
                Direction.Back => (joint.X + (right ? 1 : -1) * 0.24f * h, 0),
                _ => (joint.X + 0.14f * h + (right ? 0.20f : -0.20f) * h, 0),   // back-3/4
            };
            views[direction] = View(character, new Vector2(x, y), h, headView.DrawOrder, width);
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
    private static float SkullHeight(PartView head) =>
        head.DrawnPixels().Select(p => (int?)p.Y).Min() is { } top ? Math.Max(6, head.RestPivot.Y - top) : 30;
}
