using System.Numerics;
using PixelAniMaker.Core.Animation;
using PixelAniMaker.Core.Imaging;
using PixelAniMaker.Core.Project;
using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.Core.Tests;

public class CustomPartsTests
{
    private static IReadOnlyList<Part> AddTail(Character c, int segments = 3, CustomPlacement placement = CustomPlacement.BackSide) =>
        CustomParts.Add(c, c.Find("pelvis")!, new CustomPartOptions(CustomPartKind.Tail, placement, segments));

    private static bool Drawn(PartView view) => view.Image.Pixels.ToArray().Any(p => p != Palette.TransparentIndex);

    [Fact]
    public void A_tail_is_a_chain_of_links_drawn_in_every_stored_direction_with_the_cloth_sway()
    {
        var c = BustPartTests.Body();
        var links = AddTail(c);

        Assert.Equal(["tail1", "tail1_2", "tail1_3"], links.Select(p => p.Name));
        Assert.Equal(["꼬리 1", "꼬리 1-2", "꼬리 1-3"], links.Select(p => p.Label));
        Assert.Same(c.Find("pelvis"), links[0].Parent);
        Assert.Same(links[0], links[1].Parent);
        Assert.Same(links[1], links[2].Parent);
        Assert.All(links, p =>
        {
            Assert.True(p.IsCustom);
            Assert.False(p.IsDetail);
            Assert.Equal(SecondarySettings.Cloth, p.Secondary);
            Assert.All(c.StoredDirections, d => Assert.True(p.HasOwnView(d) && Drawn(p.View(d))));
        });
        // each link starts where the one before it points to: the joints go down the chain
        foreach (var d in c.StoredDirections)
            Assert.True(links[0].View(d).RestPivot.Y < links[1].View(d).RestPivot.Y && links[1].View(d).RestPivot.Y < links[2].View(d).RestPivot.Y);
        // leaning back in the side view (the back is towards screen-right when facing left)
        Assert.True(links[2].View(Direction.Left).RestPivot.X > links[0].View(Direction.Left).RestPivot.X);
        Assert.Equal(links[0].View(Direction.Front).RestPivot.X, links[2].View(Direction.Front).RestPivot.X);
    }

    [Fact]
    public void Adding_is_one_undo_step_and_names_do_not_repeat()
    {
        var c = BustPartTests.Body();
        int before = c.Parts.Count;
        AddTail(c);
        Assert.Equal(before + 3, c.Parts.Count);
        c.History.Undo();
        Assert.Equal(before, c.Parts.Count);
        c.History.Redo();
        Assert.Equal(before + 3, c.Parts.Count);

        var second = AddTail(c, 1);
        Assert.Equal("tail2", second[0].Name);
        var hair = CustomParts.Add(c, c.Find("head")!, new CustomPartOptions(CustomPartKind.Hair, CustomPlacement.Front, 9));
        Assert.Equal(CustomParts.MaxSegments, hair.Count);
        var acc = CustomParts.Add(c, c.Find("head")!, new CustomPartOptions(CustomPartKind.Accessory, CustomPlacement.Front, 3));
        Assert.Single(acc);
        Assert.Null(acc[0].Secondary);
    }

    [Fact]
    public void Placement_sets_the_draw_order_back_parts_go_behind_everything_but_in_front_from_behind()
    {
        var c = BustPartTests.Body();
        AddTail(c, 1);
        var front = CustomParts.Add(c, c.Find("chest")!, new CustomPartOptions(CustomPartKind.Accessory, CustomPlacement.Front, 1))[0];
        var behind = CustomParts.Add(c, c.Find("head")!, new CustomPartOptions(CustomPartKind.Hair, CustomPlacement.Behind, 1))[0];

        List<string> Order(Direction d) => c.DrawOrder(d).Select(p => p.Name).ToList();
        Assert.Equal("tail1", Order(Direction.Front)[0]);
        Assert.Equal("tail1", Order(Direction.Left)[0]);
        Assert.Equal("tail1", Order(Direction.Back)[^1]);
        Assert.Equal("tail1", Order(Direction.BackLeft)[^1]);
        foreach (var d in c.StoredDirections)
        {
            var order = Order(d);
            Assert.Equal(order.IndexOf("chest") + 1, order.IndexOf(front.Name));
            Assert.Equal(order.IndexOf("head") - 1, order.IndexOf(behind.Name));
        }
    }

    [Fact]
    public void Removing_takes_the_links_below_and_template_parts_cannot_be_removed()
    {
        var c = BustPartTests.Body();
        var links = AddTail(c);
        var names = c.Parts.Select(p => p.Name).ToList();

        Assert.False(CustomParts.Remove(c, c.Find("head")!));
        Assert.True(CustomParts.Remove(c, links[1]));
        Assert.NotNull(c.Find("tail1"));
        Assert.Null(c.Find("tail1_2"));
        Assert.Null(c.Find("tail1_3"));
        c.History.Undo();
        Assert.Equal(names, c.Parts.Select(p => p.Name));            // back in their places
        Assert.Same(links[1], c.Find("tail1_3")!.Parent);
    }

    [Fact]
    public void Moving_the_first_link_moves_the_chain_in_that_direction_only()
    {
        var c = BustPartTests.Body();
        var links = AddTail(c);
        var frontBefore = links.Select(p => p.View(Direction.Front).RestPivot).ToList();
        var leftBefore = links.Select(p => p.View(Direction.Left).RestPivot).ToList();

        DetailMove.Apply(links[0], Direction.Front, new Vector2(3, -2), c.History);
        Assert.Equal(frontBefore.Select(p => p + new Vector2(3, -2)), links.Select(p => p.View(Direction.Front).RestPivot));
        Assert.Equal(leftBefore, links.Select(p => p.View(Direction.Left).RestPivot));
        c.History.Undo();
        Assert.Equal(frontBefore, links.Select(p => p.View(Direction.Front).RestPivot));
        Assert.Throws<InvalidOperationException>(() => DetailMove.Apply(c.Find("head")!, Direction.Front, Vector2.One, c.History));
    }

    [Fact]
    public void Added_parts_are_saved_and_loaded_and_projects_without_them_keep_their_file()
    {
        var plain = BustPartTests.Body();
        Assert.DoesNotContain("custom", CharacterSpec.From(plain).ToJson());

        var c = BustPartTests.Body();
        AddTail(c, 2);
        var json = CharacterSpec.From(c).ToJson();
        Assert.Contains("\"custom\": true", json);

        using var ms = new MemoryStream();
        ProjectFile.Save(new ProjectData(c, []), ms, new RawCodec());
        ms.Position = 0;
        var loaded = ProjectFile.Load(ms, new RawCodec()).Character;
        var tail = loaded.Find("tail1")!;
        Assert.True(tail.IsCustom);
        Assert.Equal(SecondarySettings.Cloth, tail.Secondary);
        Assert.Same(tail, loaded.Find("tail1_2")!.Parent);
        Assert.Equal(c.Find("tail1_2")!.View(Direction.Left).Image.Pixels.ToArray(), loaded.Find("tail1_2")!.View(Direction.Left).Image.Pixels.ToArray());
    }

    [Fact]
    public void A_tail_swings_when_the_body_sways()
    {
        var c = BustPartTests.Body();
        var links = AddTail(c);
        var clip = new AnimationClip("sway", 6, 10);
        for (int f = 0; f < 6; f += 3)
            foreach (var d in DirectionExtensions.Stored)
                clip.SetKey(d, new Keyframe(f, new PoseData(new Dictionary<string, double>(), new Vector2(f == 0 ? -8 : 8, 0)), Easing.Linear));

        var frames = SecondaryMotion.Solve(c, clip, Direction.Front);
        Assert.All(links, link => Assert.Contains(frames, f => f.Angles.GetValueOrDefault(link) != 0));
        var composed = new Compositor().Compose(c, Direction.Front, clip.Evaluate(Direction.Front, 1), secondary: frames[1]);
        Assert.Contains(composed.Owners.ToArray(), o => o == c.IndexOf(links[2]));
    }
}
