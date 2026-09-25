using PixelAniMaker.Core.Imaging;
using PixelAniMaker.Core.Project;
using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.Core.Tests;

public class BustPartTests
{
    private static readonly Rgba Skin = new(250, 220, 200), Shade = new(190, 165, 150);

    /// <summary>96x128: pelvis (root), a 36x44 chest at (30,40) and a head above, in all five stored views.</summary>
    private static Character Body()
    {
        PartViewSpec View(int x, int y, float jx, float jy, int order, string image) => new(x, y, jx, jy, order, image);
        Dictionary<string, PartViewSpec> All(PartViewSpec v) =>
            new[] { "front", "left", "back", "frontleft", "backleft" }.ToDictionary(k => k, _ => v);
        var c = new CharacterSpec(96, 128,
        [
            new PartSpec("pelvis", "pelvis", null, All(View(34, 84, 48, 86, 0, "pelvis"))),
            new PartSpec("chest", "chest", "pelvis", All(View(30, 40, 48, 84, 1, "chest"))),
            new PartSpec("head", "head", "chest", All(View(32, 8, 48, 40, 2, "head"))),
        ]).Build(file => file switch
        {
            "chest" => new RgbaImage(36, 44, Enumerable.Repeat(Skin, 36 * 44).ToArray()),
            "head" => new RgbaImage(32, 32, Enumerable.Repeat(Skin, 32 * 32).ToArray()),
            _ => new RgbaImage(28, 20, Enumerable.Repeat(Skin, 28 * 20).ToArray()),
        });
        c.Palette.GetOrAdd(Shade);
        c.Outline.OutlineIndex = c.Palette.GetOrAdd(new Rgba(40, 30, 30));
        c.Outline.InnerIndex = c.Palette.GetOrAdd(new Rgba(120, 90, 80));
        c.Outline.Enabled = true;
        return c;
    }

    /// <summary>Per row of the left view: how far the bust reaches in front of the chest (screen-left of x = 30).</summary>
    private static Dictionary<int, int> SideDepth(Character c)
    {
        var v = c.Find(BustPart.Name)!.View(Direction.Left);
        var depth = new Dictionary<int, int>();
        for (int y = 0; y < v.Image.Height; y++)
            for (int x = 0; x < v.Image.Width; x++)
                if (v.Image[x, y] != Palette.TransparentIndex && v.RestPosition.X + x < 30)
                    depth[(int)v.RestPosition.Y + y] = Math.Max(depth.GetValueOrDefault((int)v.RestPosition.Y + y), 30 - (int)v.RestPosition.X - x);
        return depth;
    }

    [Fact]
    public void Adding_the_bust_creates_a_detail_child_of_the_chest_in_every_direction_and_is_undoable()
    {
        var c = Body();
        Assert.True(BustPart.CanAdd(c));
        BustPart.Add(c, BustSize.Medium);

        var bust = c.Find(BustPart.Name)!;
        Assert.True(bust.IsDetail);
        Assert.Same(c.Find("chest"), bust.Parent);
        Assert.False(BustPart.CanAdd(c));
        foreach (var d in c.StoredDirections)
            Assert.True(bust.HasOwnView(d));
        Assert.All(bust.View(Direction.Back).Image.Pixels.ToArray(), p => Assert.Equal(Palette.TransparentIndex, p));
        // drawn right after the chest, before the head
        var order = c.DrawOrder(Direction.Front).Select(p => p.Name).ToList();
        Assert.Equal(order.IndexOf("chest") + 1, order.IndexOf(BustPart.Name));

        c.History.Undo();
        Assert.Null(c.Find(BustPart.Name));
        c.History.Redo();
        Assert.Same(bust, c.Find(BustPart.Name));
        BustPart.Remove(c);
        Assert.Null(c.Find(BustPart.Name));
    }

    [Fact]
    public void The_side_view_is_a_teardrop_from_the_collarbone_to_the_ribs_that_grows_with_the_size()
    {
        int Max(BustSize size)
        {
            var c = Body();
            BustPart.Add(c, size);
            return SideDepth(c).Values.Max();
        }
        Assert.True(Max(BustSize.Small) < Max(BustSize.Medium) && Max(BustSize.Medium) < Max(BustSize.Large));

        var body = Body();
        BustPart.Add(body, BustSize.Medium);
        var depth = SideDepth(body);
        int top = depth.Keys.Min(), bottom = depth.Keys.Max(), span = bottom - top;
        Assert.InRange(top, 40 + 0.10 * 44 - 1, 40 + 0.10 * 44 + 2);                   // collarbone
        Assert.InRange(bottom, 40 + 0.86 * 44 - 2, 40 + 0.86 * 44 + 1);                // lower ribs
        int fullest = depth.MaxBy(kv => kv.Value).Key;
        Assert.InRange((fullest - top) / (double)span, 0.55, 0.8);
        int upper = depth.Where(kv => kv.Key < top + span / 2).Sum(kv => kv.Value);
        int lower = depth.Where(kv => kv.Key >= top + span / 2).Sum(kv => kv.Value);
        Assert.True(upper < lower, $"upper {upper} lower {lower}");
        Assert.True(depth[top] <= 2 && depth[bottom] <= 3);                            // thin at both ends
    }

    [Fact]
    public void The_bust_widens_the_side_silhouette_without_a_line_against_the_chest()
    {
        var plain = Body();
        var with = Body();
        BustPart.Add(with, BustSize.Medium);
        var compositor = new Compositor();

        int Drawn(CompositeResult r) => r.Owners.Count(o => o != CompositeResult.NoPart);
        Assert.True(Drawn(compositor.Compose(with, Direction.Left)) > Drawn(compositor.Compose(plain, Direction.Left)));
        Assert.Equal(compositor.Compose(plain, Direction.Back).Indices, compositor.Compose(with, Direction.Back).Indices);

        // no inner (overlap) line where the bust lies on the chest: it is a detail of the chest
        var front = compositor.Compose(with, Direction.Front);
        var bust = with.IndexOf(with.Find(BustPart.Name)!);
        for (int i = 0; i < front.Indices.Length; i++)
            if (front.Owners[i] == bust)
                Assert.NotEqual(with.Outline.InnerIndex, front.Indices[i]);
    }

    [Fact]
    public void The_bust_is_saved_and_loaded_as_a_detail_part()
    {
        var c = Body();
        BustPart.Add(c, BustSize.Large);
        using var ms = new MemoryStream();
        ProjectFile.Save(new ProjectData(c, []), ms, new RawCodec());
        ms.Position = 0;
        var loaded = ProjectFile.Load(ms, new RawCodec()).Character;
        var bust = loaded.Find(BustPart.Name)!;
        Assert.True(bust.IsDetail);
        Assert.Equal("chest", bust.Parent!.Name);
    }
}
