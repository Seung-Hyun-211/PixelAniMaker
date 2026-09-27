using System.Numerics;
using PixelAniMaker.Core.Imaging;

namespace PixelAniMaker.Core.Rigging;

public enum SwordKind
{
    /// <summary>One-handed arming sword.</summary>
    Sword,
    /// <summary>Two-handed greatsword.</summary>
    Greatsword,
    /// <summary>Two-handed katana.</summary>
    Katana,
    /// <summary>One-handed dagger.</summary>
    Dagger,
}

/// <summary>
/// Optional sword held in the right hand: <c>sword</c>, an added part under <c>hand_r</c> (see <see cref="HeldItem"/>).
/// Blade down at rest; clips turn it (side view: +90° points it forward, 180° straight up). A two-handed sword is gripped
/// by the right hand near the guard and by the left near the pommel (<see cref="SwordHold"/>). Sizes follow the body height.
/// </summary>
public static class SwordPart
{
    public const string Name = "sword";

    public static bool Has(Character character) => character.Find(Name) is not null;

    public static bool CanAdd(Character character) => !Has(character) && character.Find(HeldItem.HandName) is not null;

    public static bool TwoHanded(SwordKind kind) => kind is SwordKind.Greatsword or SwordKind.Katana;

    /// <summary>
    /// Adds the sword and makes room on the canvas for swings (sides and top), fits two-handed sword poses of
    /// <paramref name="clips"/> (the project's clips) and grows the canvas where one of their frames still reaches an
    /// edge — for example a blade held up in a jump — all one undo step.
    /// </summary>
    public static void Add(Character character, SwordKind kind, IEnumerable<Animation.AnimationClip>? clips = null)
    {
        if (!CanAdd(character))
            return;
        var hand = character.Find(HeldItem.HandName)!;
        var (side, top) = Room(character, kind);
        var history = character.History;
        var list = clips?.ToList() ?? [];
        history.BeginGroup("검 추가");
        CanvasResize.Apply(character, list, new CanvasMargins(side, top, side, 0), history);
        OptionalParts.Add(character, "검 추가", [(Create(character, hand, kind), hand)]);
        WeaponHold.Apply(character, list, history);                      // clips that already swing a two-handed sword
        Export.CanvasFit.GrowToFit(character, list, history);            // every clip, the blade included, stays on the canvas
        history.EndGroup();
    }

    public static void Remove(Character character) => OptionalParts.Remove(character, "검 삭제", Name);

    /// <summary>Canvas pixels added on each side and on top when the sword is added.</summary>
    public static (int Side, int Top) Room(Character character, SwordKind kind)
    {
        float h = HeldItem.BodyHeight(character);
        float k = kind switch { SwordKind.Greatsword => 0.45f, SwordKind.Katana => 0.38f, SwordKind.Dagger => 0.12f, _ => 0.3f };   // arm's reach plus the blade
        int side = 2 * (int)MathF.Ceiling(k * h / 2);
        return (side, 2 * (int)MathF.Ceiling(0.35f * side));   // top: a blade raised by the base clips (arms up in a jump) points up past the head
    }

    private static string Label(SwordKind kind) => kind switch
    {
        SwordKind.Greatsword => "양손검", SwordKind.Katana => "도", SwordKind.Dagger => "단검", _ => "한손검",
    };

    private static Part Create(Character character, Part hand, SwordKind kind)
    {
        var p = character.Palette;
        int bladeLight = p.GetOrAdd(new Rgba(226, 232, 240)), bladeMid = p.GetOrAdd(new Rgba(172, 180, 194)),
            bladeDark = p.GetOrAdd(new Rgba(112, 120, 136));
        int guard = kind == SwordKind.Katana ? p.GetOrAdd(new Rgba(52, 50, 56)) : kind == SwordKind.Dagger ? bladeDark : p.GetOrAdd(new Rgba(204, 170, 82));
        int guardDark = kind == SwordKind.Katana ? p.GetOrAdd(new Rgba(30, 28, 34)) : p.GetOrAdd(new Rgba(150, 118, 50));
        int grip = kind == SwordKind.Katana ? p.GetOrAdd(new Rgba(36, 34, 40)) : p.GetOrAdd(new Rgba(96, 64, 40));
        int wrap = kind == SwordKind.Katana ? p.GetOrAdd(new Rgba(214, 208, 196)) : p.GetOrAdd(new Rgba(66, 44, 28));

        float h = HeldItem.BodyHeight(character);
        int blade = Math.Max(8, (int)MathF.Round(h * kind switch { SwordKind.Greatsword => 0.44f, SwordKind.Katana => 0.38f, SwordKind.Dagger => 0.13f, _ => 0.32f }));
        int handle = Math.Max(3, (int)MathF.Round(h * (TwoHanded(kind) ? 0.13f : kind == SwordKind.Dagger ? 0.05f : 0.07f)));
        int width = Math.Max(2, (int)MathF.Round(h * (kind == SwordKind.Greatsword ? 0.032f : 0.022f)));
        int guardSpan = Math.Max(3, (int)MathF.Round(h * kind switch { SwordKind.Greatsword => 0.1f, SwordKind.Katana => 0.045f, SwordKind.Dagger => 0.04f, _ => 0.07f }));
        int imgH = Math.Max(guardSpan, width) + 2, cy = imgH / 2;
        int length = blade + 2 + handle + 2 + 1;
        var img = new IndexedImage(length + 1, imgH);

        // blade: tip at the left, tapering to a point (the katana's edge sweeps up to its tip)
        int tip = Math.Max(2, blade / (kind == SwordKind.Katana ? 7 : 5));
        for (int x = 1; x <= blade; x++)
        {
            float half = width / 2f * Math.Min(1f, x / (float)tip);
            int y0 = (int)MathF.Round(cy - half), y1 = Math.Max(y0, (int)MathF.Round(cy + half) - (width % 2 == 0 ? 1 : 0));
            if (kind == SwordKind.Katana && x < tip) y1 = y0;   // the kissaki: only the back of the blade reaches the point
            for (int y = y0; y <= y1; y++)
                img.Set(x, y, y == y0 ? bladeLight : y == y1 && y1 > y0 ? bladeDark : bladeMid);
            if (kind is SwordKind.Sword or SwordKind.Greatsword && width >= 3 && x > tip && x < blade - 1)
                img.Set(x, cy, bladeDark);                                                     // fuller
        }
        // guard
        for (int x = blade + 1; x <= blade + 2; x++)
            for (int y = cy - guardSpan / 2; y <= cy + guardSpan / 2; y++)
                img.Set(x, y, x == blade + 2 || y == cy + guardSpan / 2 ? guardDark : guard);
        // grip, wrapped
        int gripStart = blade + 3, gripEnd = blade + 2 + handle, gh = Math.Max(1, width / 2);
        for (int x = gripStart; x <= gripEnd; x++)
            for (int y = cy - gh + (width % 2 == 0 ? 1 : 0); y <= cy + gh - 1 + (width < 3 ? 1 : 0); y++)
                img.Set(x, y, (x + y) % (kind == SwordKind.Katana ? 3 : 2) == 0 ? wrap : grip);
        // pommel
        for (int y = cy - gh; y <= cy + gh; y++)
            img.Set(gripEnd + 1, y, guard);

        float rightAt = TwoHanded(kind) ? gripStart + 0.25f * handle : gripStart + 0.5f * handle;
        float leftAt = TwoHanded(kind) ? gripStart + 0.78f * handle : rightAt;
        return HeldItem.Create(character, hand, Name, Label(kind), img, new Vector2(rightAt, cy + 0.5f),
            [(SwordHold.Tip, new Vector2(1.5f, cy + 0.5f)), (SwordHold.Pommel, new Vector2(leftAt, cy + 0.5f))]);
    }
}

/// <summary>
/// For two-handed swords: puts the left hand on the grip near the pommel (two-bone inverse kinematics), keeping the
/// right arm and where the blade points as the clip has them. One-handed swords leave the left arm free.
/// </summary>
public static class SwordHold
{
    public const string Tip = "칼끝", Pommel = "손잡이 끝";

    /// <summary>True when the character holds a two-handed sword (its pommel grip is apart from the right hand's).</summary>
    public static bool NeedsFitting(Character character) =>
        character.Find(SwordPart.Name) is { } sword
        && HeldItem.Offset(sword.View(Direction.Front), Pommel) is { } o && o.Length() > 1.5f;

    public static PoseData Fit(Character character, Direction direction, PoseData pose)
    {
        var sword = character.Find(SwordPart.Name) ?? throw new InvalidOperationException("The character has no sword.");
        if (HeldItem.Offset(sword.View(direction), Pommel) is not { } pommel || pommel.Length() <= 1.5f)
            return pose;
        var t = character.ComputeTransforms(direction, pose);
        float aim = t[sword].Angle;
        var target = t[sword].Pivot + PartTransform.Rotate(pommel, aim);
        var rotations = pose.Rotations.ToDictionary(kv => kv.Key, kv => kv.Value);
        var handL = character.Find("hand_l")!;
        var palm = HeldItem.Palm(handL.View(direction));

        // out of the left arm's reach: bring the sword (the right hand) towards the left shoulder, blade still pointing the same way
        var shoulderL = t[character.Find("upper_arm_l")!].Pivot;
        float reach = 0.96f * ArmIk.Length(character, direction, "l", palm);
        float excess = Vector2.Distance(target, shoulderL) - reach;
        if (excess > 0)
        {
            var grip = t[sword].Pivot + Vector2.Normalize(shoulderL - target) * excess;
            ArmIk.Reach(character, direction, t, "r", grip, sword.View(direction).RestPivot, rotations);
            t = character.ComputeTransforms(direction, pose with { Rotations = rotations });
            rotations[SwordPart.Name] = ArmIk.Degrees(aim - t[character.Find(HeldItem.HandName)!].Angle);
            t = character.ComputeTransforms(direction, pose with { Rotations = rotations });
            target = t[sword].Pivot + PartTransform.Rotate(pommel, aim);
        }
        ArmIk.Reach(character, direction, t, "l", target, palm, rotations);
        return pose with { Rotations = rotations };
    }
}
