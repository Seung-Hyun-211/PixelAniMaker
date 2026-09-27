using System.Numerics;
using PixelAniMaker.Core.Imaging;

namespace PixelAniMaker.Core.Rigging;

public enum GunKind
{
    Rifle,
    Pistol,
}

/// <summary>
/// Optional gun held in the right hand: <c>gun</c>, an added part under <c>hand_r</c> (so it can be redrawn, moved and
/// removed like other added parts). Every view shows the gun's side profile, grip on the hand's grip point, muzzle
/// pointing down at rest (carried at the side while the arms hang). Clips turn it from there: in the side view +90°
/// points the muzzle forward. It is drawn over the chest and under the head and near arm from the side, over the right
/// forearm and under the right hand from the front, and behind the body from the back. Sizes follow the body height.
/// </summary>
public static class GunPart
{
    public const string Name = "gun";
    public const string HandName = "hand_r";

    public static bool Has(Character character) => character.Find(Name) is not null;

    public static bool CanAdd(Character character) => !Has(character) && character.Find(HandName) is not null;

    /// <summary>
    /// Adds the gun and widens the canvas on both sides by <see cref="Room"/> (an aimed gun reaches well past the arms),
    /// then fits the gun poses of <paramref name="clips"/> (the project's clips) to the body and grows the canvas further
    /// where one of their frames still reaches an edge — all one undo step.
    /// </summary>
    public static void Add(Character character, GunKind kind, IEnumerable<Animation.AnimationClip>? clips = null)
    {
        if (!CanAdd(character))
            return;
        var hand = character.Find(HandName)!;
        int room = Room(character, kind);
        var history = character.History;
        var list = clips?.ToList() ?? [];
        history.BeginGroup("총 추가");
        CanvasResize.Apply(character, list, new CanvasMargins(room, 0, room, 0), history);
        OptionalParts.Add(character, "총 추가", [(Create(character, hand, kind), hand)]);
        WeaponHold.Apply(character, list, history);                      // clips that already aim a gun
        Export.CanvasFit.GrowToFit(character, list, history);            // every clip, the gun included, stays on the canvas
        history.EndGroup();
    }

    /// <summary>Canvas pixels added on the left and on the right when the gun is added.</summary>
    public static int Room(Character character, GunKind kind) =>
        2 * (int)MathF.Ceiling((kind == GunKind.Rifle ? 0.18f : 0.05f) * BodyHeight(character) / 2);

    public static void Remove(Character character) => OptionalParts.Remove(character, "총 삭제", Name);

    /// <summary>The body's height at rest (front view), which sets the gun's size.</summary>
    public static int BodyHeight(Character character) => HeldItem.BodyHeight(character);

    private static Part Create(Character character, Part hand, GunKind kind)
    {
        var palette = character.Palette;
        int dark = palette.GetOrAdd(new Rgba(46, 48, 56)), mid = palette.GetOrAdd(new Rgba(82, 86, 96)),
            light = palette.GetOrAdd(new Rgba(132, 138, 150)), furniture = palette.GetOrAdd(new Rgba(62, 58, 52));
        var (profile, points) = Profile(kind, BodyHeight(character), dark, mid, light, furniture);
        // where the gun meets the body (see GunHold): the stock's butt, the support hand, the magazine
        return HeldItem.Create(character, hand, Name, kind == GunKind.Rifle ? "소총" : "권총", profile, points[0],
            [(GunHold.Butt, points[1]), (GunHold.Guard, points[2]), (GunHold.Magazine, points[3])]);
    }

    /// <summary>
    /// The gun seen from its left side, muzzle to the left (-x), and its points (pixel centres): grip, butt, handguard
    /// (under it, for the support hand) and magazine. A pistol has no stock: its butt is the grip; the support hand wraps the grip.
    /// </summary>
    private static (IndexedImage Image, Vector2[] Points) Profile(GunKind kind, int bodyHeight, int dark, int mid, int light, int furniture)
    {
        float h = bodyHeight;
        if (kind == GunKind.Pistol)
        {
            int len = Math.Max(8, (int)MathF.Round(0.14f * h)), slide = Math.Max(2, (int)MathF.Round(0.03f * h));
            int gripLen = Math.Max(3, (int)MathF.Round(0.07f * h));
            var img = new IndexedImage(len + 2, slide + gripLen + 2);
            for (int x = 1; x <= len; x++)
                for (int y = 1; y <= slide; y++)
                    img.Set(x, y, y == 1 ? light : mid);                                         // slide
            for (int y = slide + 1; y <= slide + gripLen; y++)
                for (int x = len - (int)(0.35f * len) - (y - slide) / 3; x <= len - (y - slide) / 3; x++)
                    img.Set(x, y, x == len - (y - slide) / 3 ? dark : furniture);               // grip, raked back
            img.Set(len - (int)(0.45f * len), slide + 1, dark);                                 // trigger guard
            var g = new Vector2(len - (int)(0.2f * len) + 0.5f, slide + gripLen / 2 + 0.5f);
            return (img, [g, g, g + new Vector2(-0.5f, 1), g]);
        }
        int length = Math.Max(20, (int)MathF.Round(0.42f * h));   // a carbine: longer rifles leave a new project's canvas when aimed
        int body = Math.Max(3, (int)MathF.Round(0.045f * h));                                   // receiver height
        int stock = Math.Max(4, (int)MathF.Round(0.07f * h));
        int drop = Math.Max(4, (int)MathF.Round(0.09f * h));                                    // grip and magazine below the body
        var rifle = new IndexedImage(length + 2, body + drop + 3);
        int top = 1 + Math.Max(1, body / 3);                                                     // leave room for the sight rail
        void Box(int x0, int x1, int y0, int y1, int colour)
        {
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                    rifle.Set(x, y, colour);
        }
        int barrelEnd = (int)(0.24f * length), guardEnd = (int)(0.5f * length), receiverEnd = (int)(0.72f * length);
        Box(1, barrelEnd, top + body / 2, top + body / 2 + Math.Max(0, body / 4 - 1), dark);   // barrel
        Box(1, 2, top + body / 2 - 1, top + body / 2 + 1, dark);                               // muzzle
        Box(barrelEnd + 1, guardEnd, top + 1, top + body - 1, furniture);                      // handguard
        Box(guardEnd + 1, receiverEnd, top, top + body, mid);                                  // receiver
        Box(guardEnd + 1, receiverEnd, top, top, light);                                       // receiver highlight
        Box(guardEnd + 3, receiverEnd - 2, top - Math.Max(1, body / 3), top - 1, dark);        // sight rail
        for (int x = receiverEnd + 1; x <= length; x++)                                        // stock: deepens towards the butt
        {
            float t = (x - receiverEnd) / (float)(length - receiverEnd);
            Box(x, x, top + 1, top + body - 1 + (int)MathF.Round(t * (stock - body + 2)), furniture);
        }
        Box(length, length, top, top + stock + 1, dark);                                       // butt pad
        int magX = guardEnd + 2, gripX = receiverEnd - Math.Max(3, body);
        for (int y = 1; y <= drop; y++)
        {
            Box(magX + y / 3, magX + y / 3 + Math.Max(2, body - 1), top + body + y, top + body + y, dark);   // magazine, curved forward
            Box(gripX + y / 2, gripX + y / 2 + Math.Max(1, body / 2), top + body + y, top + body + y, furniture);   // pistol grip, raked back
        }
        var grip = new Vector2(gripX + drop / 4 + 1 + 0.5f, top + body + drop / 2 + 0.5f);
        return (rifle,
        [
            grip,
            new Vector2(length + 0.5f, top + (body + stock) / 2f + 0.5f),                       // butt pad
            new Vector2((barrelEnd + guardEnd) / 2f + 0.5f, top + body + 0.5f),                   // under the handguard
            new Vector2(magX + drop / 3f + body / 2f + 0.5f, top + body + 0.6f * drop + 0.5f),    // magazine
        ]);
    }
}
