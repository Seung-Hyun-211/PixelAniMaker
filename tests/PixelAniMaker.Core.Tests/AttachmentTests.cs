using System.Numerics;
using PixelAniMaker.Core.Animation;
using PixelAniMaker.Core.Export;
using PixelAniMaker.Core.History;
using PixelAniMaker.Core.Imaging;
using PixelAniMaker.Core.Project;
using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.Core.Tests;

public class AttachmentTests
{
    private static readonly Rgba Red = new(255, 0, 0);

    /// <summary>16x16 canvas, one 2x4 part at (4,4) hanging from its joint (5,4).</summary>
    private static Character OnePart()
    {
        var v = new PartViewSpec(4, 4, 5, 4, 0, "a");
        return new CharacterSpec(16, 16, [new PartSpec("hand", "hand", null,
                new Dictionary<string, PartViewSpec> { ["front"] = v, ["left"] = v, ["back"] = v })])
            .Build(_ => new RgbaImage(2, 4, Enumerable.Repeat(Red, 8).ToArray()));
    }

    private static AttachmentPoints PointsOf(Character c, Direction d) => c.Find("hand")!.View(d).Attachments;

    [Fact]
    public void Point_follows_the_part_rotation()
    {
        var c = OnePart();
        PointsOf(c, Direction.Front).Set("grip", new Vector2(1, 4));   // 4 px below the joint (local pivot (1,0))
        c.PoseFor(Direction.Front).Set("hand", 90);

        var a = Attachments.Place(c, Direction.Front).Single();
        Assert.Equal("grip", a.Name);
        Assert.Equal(1, a.Position.X, 3);           // rotated clockwise: now left of the joint (5,4)
        Assert.Equal(4, a.Position.Y, 3);
        Assert.Equal(90, a.Degrees, 3);
    }

    [Fact]
    public void Right_view_is_mirrored()
    {
        var c = OnePart();
        PointsOf(c, Direction.Left).Set("grip", new Vector2(0, 2));
        c.PoseFor(Direction.Left).Set("hand", 30);
        var left = Attachments.Place(c, Direction.Left).Single();
        var right = Attachments.Place(c, Direction.Right).Single();
        Assert.Equal(16 - left.Position.X, right.Position.X, 3);
        Assert.Equal(left.Position.Y, right.Position.Y, 3);
        Assert.Equal(-left.Degrees, right.Degrees, 3);
    }

    [Fact]
    public void Edits_are_undoable_as_one_step()
    {
        var points = new AttachmentPoints();
        var history = new UndoHistory();
        AttachmentChange.Apply(points, history, p => p.Set("a", new Vector2(1, 1)));
        AttachmentChange.Apply(points, history, p => { p.Remove("a"); p.Set("b", new Vector2(1, 1)); });   // rename
        Assert.Equal(["b"], points.All.Keys);
        history.Undo();
        Assert.Equal(["a"], points.All.Keys);
        AttachmentChange.Apply(points, history, _ => { });                                               // no-op not recorded
        history.Undo();
        Assert.Empty(points.All);
        points.Set("point", default);
        Assert.Equal("point2", AttachmentChange.FreeName(points));
    }

    [Fact]
    public void Points_survive_save_and_load_and_reach_the_sheet_json()
    {
        var c = OnePart();
        PointsOf(c, Direction.Front).Set("grip", new Vector2(1, 3));
        using var ms = new MemoryStream();
        ProjectFile.Save(new ProjectData(c, []), ms, new RawCodec());
        ms.Position = 0;
        var loaded = ProjectFile.Load(ms, new RawCodec()).Character;
        Assert.Equal(new Vector2(1, 3), PointsOf(loaded, Direction.Front).All["grip"]);
        Assert.Empty(PointsOf(loaded, Direction.Back).All);

        var sheet = SpriteSheet.Build(loaded, [new AnimationClip("idle", 1, 6, true)], new Compositor());
        var front = sheet.Clips[0].Directions["front"][0];
        Assert.Equal(new SheetAttachment("hand", "grip", 5, 7, 0), front.Attachments!.Single());
        Assert.Null(sheet.Clips[0].Directions["back"][0].Attachments);
        Assert.Contains("\"grip\"", sheet.MetadataJson());
    }
}
