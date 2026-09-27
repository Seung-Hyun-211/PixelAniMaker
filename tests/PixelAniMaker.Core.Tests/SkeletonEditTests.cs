using System.Numerics;
using PixelAniMaker.Core.Rigging;
using PixelAniMaker.Core.Rigging.Body;

namespace PixelAniMaker.Core.Tests;

public class SkeletonEditTests
{
    private static Character Body() => MannequinBuilder.Build(BodyProportions.For(6), jointDiscs: false).Build();

    private static (Vector2 Position, Vector2 Pivot) Rest(Character c, string part, Direction d = Direction.Front)
    {
        var v = c.Find(part)!.View(d);
        return (v.RestPosition, v.RestPivot);
    }

    [Fact]
    public void Moving_a_template_part_takes_the_parts_under_it_along_and_undoes_in_one_step()
    {
        var c = Body();
        var arm = Rest(c, "upper_arm_l");
        var hand = Rest(c, "hand_l");
        var chest = Rest(c, "chest");

        Assert.True(SkeletonEdit.Apply(c, c.Find("upper_arm_l")!, Direction.Front, new Vector2(-3.4f, 1), SkeletonHandle.Part));

        Assert.Equal((arm.Position + new Vector2(-3, 1), arm.Pivot + new Vector2(-3, 1)), Rest(c, "upper_arm_l"));
        Assert.Equal((hand.Position + new Vector2(-3, 1), hand.Pivot + new Vector2(-3, 1)), Rest(c, "hand_l"));
        Assert.Equal(chest, Rest(c, "chest"));
        Assert.Equal(Rest(c, "upper_arm_l", Direction.Left), Rest(Body(), "upper_arm_l", Direction.Left));   // other views untouched

        c.History.Undo();
        Assert.Equal(arm, Rest(c, "upper_arm_l"));
        Assert.Equal(hand, Rest(c, "hand_l"));
    }

    [Fact]
    public void Mirror_moves_the_counterpart_across_the_centre_in_front_and_the_same_way_from_the_side()
    {
        var c = Body();
        var right = Rest(c, "upper_arm_r");
        SkeletonEdit.Apply(c, c.Find("upper_arm_l")!, Direction.Front, new Vector2(-2, 0), SkeletonHandle.Part, mirror: true);
        Assert.Equal(right.Pivot + new Vector2(2, 0), Rest(c, "upper_arm_r").Pivot);   // both shoulders move in

        var side = Rest(c, "thigh_r", Direction.Left);
        SkeletonEdit.Apply(c, c.Find("thigh_l")!, Direction.Left, new Vector2(1, -1), SkeletonHandle.Part, mirror: true);
        Assert.Equal(side.Pivot + new Vector2(1, -1), Rest(c, "thigh_r", Direction.Left).Pivot);

        var head = Rest(c, "head");   // a centre part has no counterpart: moved once
        SkeletonEdit.Apply(c, c.Find("head")!, Direction.Front, new Vector2(0, 2), SkeletonHandle.Part, mirror: true);
        Assert.Equal(head.Pivot + new Vector2(0, 2), Rest(c, "head").Pivot);
    }

    [Fact]
    public void Moving_a_joint_keeps_the_picture_and_changes_where_it_turns()
    {
        var c = Body();
        var forearm = Rest(c, "forearm_l");
        var hand = Rest(c, "hand_l");
        SkeletonEdit.Apply(c, c.Find("forearm_l")!, Direction.Front, new Vector2(0, 2), SkeletonHandle.Joint);
        Assert.Equal((forearm.Position, forearm.Pivot + new Vector2(0, 2)), Rest(c, "forearm_l"));
        Assert.Equal(hand, Rest(c, "hand_l"));

        c.PoseFor(Direction.Front).Set("forearm_l", 90);
        Assert.Equal(forearm.Pivot + new Vector2(0, 2), c.ComputeTransforms(Direction.Front)[c.Find("forearm_l")!].Pivot);
    }

    [Fact]
    public void A_drag_on_a_turned_limb_is_turned_back_into_rest_space()
    {
        var c = Body();
        var pose = new PoseData(new Dictionary<string, double> { ["upper_arm_l"] = 90 }, Vector2.Zero);
        // the forearm hangs from an upper arm turned 90°: dragging it right on screen moves it "up" at rest
        var rest = SkeletonEdit.ToRest(c, c.Find("forearm_l")!, Direction.Front, pose, new Vector2(4, 0));
        Assert.Equal(0, rest.X, 3);
        Assert.Equal(-4, rest.Y, 3);
        Assert.Equal(new Vector2(4, 0), SkeletonEdit.ToRest(c, c.Root, Direction.Front, pose, new Vector2(4, 0)));
    }

    [Fact]
    public void A_mirrored_view_that_shares_its_source_moves_that_source()
    {
        var c = Body();
        var left = Rest(c, "hand_l", Direction.Left);
        Assert.False(c.Find("hand_l")!.HasOwnRight);
        SkeletonEdit.Apply(c, c.Find("hand_l")!, Direction.Right, new Vector2(1, 0), SkeletonHandle.Part);
        Assert.Equal(left.Pivot + new Vector2(1, 0), Rest(c, "hand_l", Direction.Left).Pivot);
        Assert.False(SkeletonEdit.Apply(c, c.Find("hand_l")!, Direction.Right, new Vector2(0.2f, -0.3f), SkeletonHandle.Part));
    }
}
