using PixelAniMaker.Core.Animation;
using PixelAniMaker.Core.Project;
using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.Core.Tests;

public class DrawOrderTests
{
    /// <summary>pelvis, chest (+bust), head (+eyes) back to front in every direction.</summary>
    private static Character Body()
    {
        var c = BustPartTests.Body();
        EyeParts.Add(c);
        BustPart.Add(c, BustSize.Medium);
        c.History.Clear();
        return c;
    }

    private static List<string> Order(Character c, Direction d) => c.DrawOrder(d).Select(p => p.Name).ToList();

    [Fact]
    public void A_part_moves_past_its_neighbour_with_its_detail_parts()
    {
        var c = Body();
        Assert.Equal(["pelvis", "chest", "bust", "head", "eye_r", "eye_l"], Order(c, Direction.Front));

        Assert.True(DrawOrderEdit.Move(c, c.Find("head")!, Direction.Front, DrawOrderMove.Backward, c.History));
        Assert.Equal(["pelvis", "head", "eye_r", "eye_l", "chest", "bust"], Order(c, Direction.Front));
        Assert.Equal(["pelvis", "chest", "bust", "head", "eye_r", "eye_l"], Order(c, Direction.Left));   // other directions keep theirs

        Assert.True(DrawOrderEdit.Move(c, c.Find("pelvis")!, Direction.Front, DrawOrderMove.ToFront, c.History));
        Assert.Equal(["head", "eye_r", "eye_l", "chest", "bust", "pelvis"], Order(c, Direction.Front));
        Assert.True(DrawOrderEdit.Move(c, c.Find("chest")!, Direction.Front, DrawOrderMove.Forward, c.History));
        Assert.Equal(["head", "eye_r", "eye_l", "pelvis", "chest", "bust"], Order(c, Direction.Front));

        c.History.Undo();
        c.History.Undo();
        c.History.Undo();
        Assert.Equal(["pelvis", "chest", "bust", "head", "eye_r", "eye_l"], Order(c, Direction.Front));
    }

    [Fact]
    public void With_children_the_parts_below_move_too()
    {
        var c = Body();
        Assert.True(DrawOrderEdit.Move(c, c.Find("chest")!, Direction.Front, DrawOrderMove.ToBack, c.History));
        Assert.Equal(["chest", "bust", "pelvis", "head", "eye_r", "eye_l"], Order(c, Direction.Front));
        c.History.Undo();
        Assert.True(DrawOrderEdit.Move(c, c.Find("chest")!, Direction.Front, DrawOrderMove.ToBack, c.History, withChildren: true));
        Assert.Equal(["chest", "bust", "head", "eye_r", "eye_l", "pelvis"], Order(c, Direction.Front));
        // nothing but its own children in front of it: cannot go further forward with them
        Assert.False(DrawOrderEdit.CanMove(c, c.Find("pelvis")!, Direction.Front, DrawOrderMove.Forward, withChildren: true));
    }

    [Fact]
    public void Ends_and_detail_parts_cannot_move()
    {
        var c = Body();
        Assert.False(DrawOrderEdit.CanMove(c, c.Find("pelvis")!, Direction.Back, DrawOrderMove.Backward));
        Assert.False(DrawOrderEdit.CanMove(c, c.Find("pelvis")!, Direction.Back, DrawOrderMove.ToBack));
        Assert.False(DrawOrderEdit.CanMove(c, c.Find("head")!, Direction.Back, DrawOrderMove.Forward));
        Assert.False(DrawOrderEdit.CanMove(c, c.Find("eye_r")!, Direction.Back, DrawOrderMove.Backward));
        Assert.False(DrawOrderEdit.Move(c, c.Find("head")!, Direction.Back, DrawOrderMove.ToFront, c.History));
        Assert.False(c.History.CanUndo);
    }

    [Fact]
    public void A_mirrored_direction_edits_its_source_order_and_its_own_drawings_follow()
    {
        var c = Body();
        RightViewChange.Apply(c.Find("head")!, c.History, separate: true);
        DrawOrderEdit.Move(c, c.Find("head")!, Direction.Right, DrawOrderMove.ToBack, c.History);
        Assert.Equal("head", Order(c, Direction.Left)[0]);
        Assert.Equal("head", Order(c, Direction.Right)[0]);
        Assert.Equal(c.Find("head")!.View(Direction.Left).DrawOrder, c.Find("head")!.View(Direction.Right).DrawOrder);
    }

    [Fact]
    public void The_order_is_saved()
    {
        var c = Body();
        DrawOrderEdit.Move(c, c.Find("chest")!, Direction.FrontLeft, DrawOrderMove.ToFront, c.History);
        using var ms = new MemoryStream();
        ProjectFile.Save(new ProjectData(c, []), ms, new RawCodec());
        ms.Position = 0;
        var loaded = ProjectFile.Load(ms, new RawCodec()).Character;
        Assert.Equal(Order(c, Direction.FrontLeft), Order(loaded, Direction.FrontLeft));
        Assert.Equal(["pelvis", "head", "eye_r", "eye_l", "chest", "bust"], Order(loaded, Direction.FrontLeft));
    }
}

public class PoseDrawOrderTests
{
    private static Character Body() => BustPartTests.Body();   // pelvis, chest, head back to front

    private static List<string> Drawn(Character c, PoseData pose) =>
        DrawOrderEdit.WithOverrides(c, c.DrawOrder(Direction.Front).ToList(), pose).Select(p => p.Name).ToList();

    private static PoseData With(params (string Part, string Anchor, bool Front)[] orders) =>
        PoseData.Rest with { Order = orders.ToDictionary(o => o.Part, o => new OrderOverride(o.Anchor, o.Front)) };

    [Fact]
    public void A_pose_can_put_a_part_in_front_of_or_behind_another()
    {
        var c = Body();
        Assert.Equal(["chest", "head", "pelvis"], Drawn(c, With(("pelvis", "head", true))));
        Assert.Equal(["head", "pelvis", "chest"], Drawn(c, With(("head", "pelvis", false))));
        Assert.Equal(["pelvis", "chest", "head"], Drawn(c, With(("head", "nothing", false))));   // unknown anchor: unchanged
        Assert.Equal(["pelvis", "chest", "head"], Drawn(c, PoseData.Rest));
    }

    [Fact]
    public void The_composite_uses_the_pose_order_and_the_pose_keeps_it_through_undo()
    {
        var c = Body();
        var compositor = new Compositor();
        var pose = c.PoseFor(Direction.Front);
        pose.Set("chest", 180);                                             // swing the chest down over the pelvis
        var plain = compositor.Compose(c, Direction.Front);
        short pelvis = (short)c.IndexOf(c.Find("pelvis")!);
        PoseChange.Apply(pose, c.History, p => p.SetOrder("pelvis", new OrderOverride("head", true)));
        Assert.Equal(new OrderOverride("head", true), pose.Snapshot().OrderOf("pelvis"));
        var composed = compositor.Compose(c, Direction.Front);
        Assert.True(composed.Owners.ToArray().Count(o => o == pelvis) > plain.Owners.ToArray().Count(o => o == pelvis));   // pelvis now over the chest
        c.History.Undo();
        Assert.Null(pose.GetOrder("pelvis"));
        Assert.Equal(plain.Owners.ToArray(), compositor.Compose(c, Direction.Front).Owners.ToArray());
    }

    [Fact]
    public void A_pose_order_can_take_the_parts_below_along_and_saves_that_only_when_used()
    {
        var c = Body();
        var pose = PoseData.Rest with { Order = new Dictionary<string, OrderOverride> { ["chest"] = OrderOverride.Create("pelvis", false, true) } };
        Assert.Equal(["chest", "head", "pelvis"], Drawn(c, pose));
        pose = PoseData.Rest with { Order = new Dictionary<string, OrderOverride> { ["chest"] = OrderOverride.Create("head", true, true) } };
        Assert.Equal(["pelvis", "chest", "head"], Drawn(c, pose));   // the anchor is one of its own children: ignored

        var clip = new AnimationClip("c", 1);
        clip.SetKey(Direction.Front, new Keyframe(0, PoseData.Rest with { Order = new Dictionary<string, OrderOverride>
        {
            ["chest"] = OrderOverride.Create("pelvis", false, true),
            ["head"] = OrderOverride.Create("pelvis", true, false),
        } }, Easing.Linear));
        var json = AnimationJson.Serialize([clip]);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(json, "\"children\": true"));
        Assert.DoesNotContain("withChildren", json);
        Assert.True(AnimationJson.Parse(json)[0].Evaluate(Direction.Front, 0).OrderOf("chest")!.WithChildren);
    }

    [Fact]
    public void Between_keys_the_earlier_key_order_holds_and_keys_save_it()
    {
        var clip = new AnimationClip("swing", 4);
        clip.SetKey(Direction.Front, new Keyframe(0, With(("pelvis", "head", true)), Easing.Linear));
        clip.SetKey(Direction.Front, new Keyframe(2, PoseData.Rest, Easing.Linear));
        Assert.NotNull(clip.Evaluate(Direction.Front, 1).OrderOf("pelvis"));
        Assert.Null(clip.Evaluate(Direction.Front, 2).OrderOf("pelvis"));

        var json = AnimationJson.Serialize([clip]);
        Assert.Contains("\"order\"", json);
        var loaded = AnimationJson.Parse(json)[0];
        Assert.Equal(new OrderOverride("head", true), loaded.Evaluate(Direction.Front, 0).OrderOf("pelvis"));
        Assert.DoesNotContain("\"order\"", AnimationJson.Serialize([new AnimationClip("plain", 2)]));
    }
}

public class KeysPastEndTests
{
    [Fact]
    public void Keys_past_the_end_are_listed_and_can_be_deleted_with_undo()
    {
        var clip = new AnimationClip("walk", 8);
        clip.SetKey(Direction.Front, new Keyframe(6, PoseData.Rest, Easing.Linear));
        clip.SetKey(Direction.Left, new Keyframe(7, PoseData.Rest, Easing.Linear));
        clip.SetKey(Direction.Left, new Keyframe(2, PoseData.Rest, Easing.Linear));
        clip.FrameCount = 4;
        Assert.Equal(2, clip.KeysPastEnd.Count);

        var history = new PixelAniMaker.Core.History.UndoHistory();
        Assert.True(KeysPastEndChange.Apply(clip, history));
        Assert.Empty(clip.KeysPastEnd);
        Assert.Single(clip.Keys(Direction.Left));
        Assert.False(KeysPastEndChange.Apply(clip, history));
        history.Undo();
        Assert.Equal(2, clip.KeysPastEnd.Count);
    }
}
