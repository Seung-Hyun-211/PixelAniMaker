using System.Numerics;
using PixelAniMaker.Core.Imaging;
using PixelAniMaker.Core.Project;
using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.Core.Tests;

public class EyePartsTests
{
    private static readonly Rgba Skin = new(250, 220, 200);

    /// <summary>48x48 canvas: a 30x33 skin head at (9,3) on its neck joint (24,36), plus a body below.</summary>
    internal static Character HeadAndBody()
    {
        var head = new PartViewSpec(9, 3, 24, 36, 1, "head");
        var body = new PartViewSpec(14, 36, 24, 40, 0, "body");
        var views = (PartViewSpec v) => new Dictionary<string, PartViewSpec> { ["front"] = v, ["left"] = v, ["back"] = v };
        return new CharacterSpec(48, 48,
            [new PartSpec("body", "body", null, views(body)), new PartSpec("head", "head", "body", views(head))])
            .Build(file => file == "head"
                ? new RgbaImage(30, 33, Enumerable.Repeat(Skin, 30 * 33).ToArray())
                : new RgbaImage(20, 10, Enumerable.Repeat(Skin, 200).ToArray()));
    }

    private static bool Blank(PartView view) => view.Image.Pixels.ToArray().All(p => p == Palette.TransparentIndex);

    [Fact]
    public void Adding_eyes_creates_detail_children_of_the_head_and_is_undoable()
    {
        var c = HeadAndBody();
        int changes = 0;
        c.PartsChanged += (_, _) => changes++;

        EyeParts.Add(c);
        var right = c.Find(EyeParts.Right)!;
        var left = c.Find(EyeParts.Left)!;
        Assert.Equal(4, c.Parts.Count);
        Assert.All([right, left], eye =>
        {
            Assert.True(eye.IsDetail);
            Assert.Same(c.Find("head"), eye.Parent);
        });
        Assert.False(EyeParts.CanAdd(c));

        c.History.Undo();
        Assert.Equal(2, c.Parts.Count);
        Assert.Empty(c.Find("head")!.Children);
        Assert.True(EyeParts.CanAdd(c));

        c.History.Redo();
        Assert.Same(right, c.Find(EyeParts.Right));
        Assert.True(changes >= 3);
    }

    [Fact]
    public void Eyes_sit_on_the_face_and_are_blank_where_they_cannot_be_seen()
    {
        var c = HeadAndBody();
        EyeParts.Add(c);
        var right = c.Find(EyeParts.Right)!;
        var left = c.Find(EyeParts.Left)!;
        var joint = c.Find("head")!.View(Direction.Front).RestPivot;

        // front: the character's right eye is on the screen's left, both above the neck joint
        Assert.True(right.View(Direction.Front).RestPivot.X < joint.X);
        Assert.True(left.View(Direction.Front).RestPivot.X > joint.X);
        Assert.True(right.View(Direction.Front).RestPivot.Y < joint.Y - 5);
        Assert.False(Blank(right.View(Direction.Front)));
        Assert.False(Blank(left.View(Direction.Front)));

        // back: nothing drawn; side (facing left): only the near (left) eye, towards the face
        Assert.True(Blank(right.View(Direction.Back)) && Blank(left.View(Direction.Back)));
        Assert.False(Blank(left.View(Direction.Left)));
        Assert.True(Blank(right.View(Direction.Left)));
        Assert.True(left.View(Direction.Left).RestPivot.X < joint.X);
    }

    [Fact]
    public void No_outline_is_drawn_between_an_eye_and_the_head()
    {
        var c = HeadAndBody();
        c.Outline.OutlineIndex = c.Palette.GetOrAdd(new Rgba(0, 0, 0));
        c.Outline.InnerIndex = c.Palette.GetOrAdd(new Rgba(128, 0, 128));
        c.Outline.Enabled = true;
        EyeParts.Add(c);

        var composite = new Compositor().Compose(c, Direction.Front);
        int eye = c.IndexOf(c.Find(EyeParts.Right)!);
        var eyePixels = Enumerable.Range(0, composite.Indices.Length).Where(i => composite.Owners[i] == eye).ToList();
        Assert.NotEmpty(eyePixels);
        Assert.DoesNotContain(eyePixels, i => composite.Indices[i] == c.Outline.InnerIndex);
    }

    [Fact]
    public void Eyes_survive_save_and_load()
    {
        var c = HeadAndBody();
        EyeParts.Add(c);
        var front = c.Find(EyeParts.Left)!.View(Direction.Front);

        using var ms = new MemoryStream();
        ProjectFile.Save(new ProjectData(c, []), ms, new RawCodec());
        ms.Position = 0;
        var loaded = ProjectFile.Load(ms, new RawCodec()).Character;

        var eye = loaded.Find(EyeParts.Left)!;
        Assert.True(eye.IsDetail);
        Assert.Equal("head", eye.Parent!.Name);
        var lf = eye.View(Direction.Front);
        Assert.Equal(front.RestPivot, lf.RestPivot);
        Assert.Equal(front.RestPosition, lf.RestPosition);
        Assert.Equal(front.Image.Pixels.ToArray().Select(i => c.Palette[i]),
            lf.Image.Pixels.ToArray().Select(i => loaded.Palette[i]));
        Assert.False(loaded.Find("head")!.IsDetail);
    }

    [Fact]
    public void Moving_an_eye_shifts_its_image_and_joint_in_that_direction_only_and_is_undoable()
    {
        var c = HeadAndBody();
        EyeParts.Add(c);
        var eye = c.Find(EyeParts.Right)!;
        var front = eye.View(Direction.Front);
        var (pos, pivot) = (front.RestPosition, front.RestPivot);
        var backPivot = eye.View(Direction.Back).RestPivot;
        var before = new Compositor().Compose(c, Direction.Front);

        DetailMove.Apply(eye, Direction.Front, new Vector2(2.4f, -1f), c.History);   // rounds to whole pixels
        Assert.Equal(pos + new Vector2(2, -1), front.RestPosition);
        Assert.Equal(pivot + new Vector2(2, -1), front.RestPivot);
        Assert.Equal(backPivot, eye.View(Direction.Back).RestPivot);
        var after = new Compositor().Compose(c, Direction.Front);
        Assert.NotEqual(before.Owners, after.Owners);

        c.History.Undo();
        Assert.Equal(pos, front.RestPosition);
        Assert.Equal(before.Indices, new Compositor().Compose(c, Direction.Front).Indices);
    }

    [Fact]
    public void Moving_an_eye_in_the_mirrored_view_moves_the_view_both_sides_show()
    {
        var c = HeadAndBody();
        EyeParts.Add(c);
        var eye = c.Find(EyeParts.Left)!;
        var pivot = eye.View(Direction.Left).RestPivot;
        DetailMove.Apply(eye, Direction.Right, new Vector2(-3, 0), c.History);
        Assert.Equal(pivot + new Vector2(-3, 0), eye.View(Direction.Left).RestPivot);
        Assert.Same(eye.View(Direction.Left), eye.View(Direction.Right));
    }

    [Fact]
    public void Only_detail_parts_move_and_moved_eyes_survive_save_and_load()
    {
        var c = HeadAndBody();
        Assert.Throws<InvalidOperationException>(() => DetailMove.Apply(c.Find("head")!, Direction.Front, Vector2.One, c.History));

        EyeParts.Add(c);
        DetailMove.Apply(c.Find(EyeParts.Left)!, Direction.Front, new Vector2(1, 2), c.History);
        var moved = c.Find(EyeParts.Left)!.View(Direction.Front).RestPivot;
        using var ms = new MemoryStream();
        ProjectFile.Save(new ProjectData(c, []), ms, new RawCodec());
        ms.Position = 0;
        var loaded = ProjectFile.Load(ms, new RawCodec()).Character;
        Assert.Equal(moved, loaded.Find(EyeParts.Left)!.View(Direction.Front).RestPivot);
    }

    [Fact]
    public void Removing_eyes_is_undoable_and_parts_with_children_cannot_be_removed()
    {
        var c = HeadAndBody();
        EyeParts.Add(c);
        EyeParts.Remove(c);
        Assert.False(EyeParts.Has(c));
        c.History.Undo();
        Assert.True(EyeParts.Has(c));

        var change = new PartsChange(c, "x", [(c.Find("head")!, c.Find("body")!)], adding: false);
        Assert.Throws<InvalidOperationException>(change.Redo);
    }
}
