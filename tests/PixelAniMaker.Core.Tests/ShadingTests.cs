using PixelAniMaker.Core.Imaging;
using PixelAniMaker.Core.Project;
using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.Core.Tests;

public class ShadingTests
{
    private static readonly Rgba Light = new(222, 206, 184), Mid = new(176, 150, 120), Dark = new(120, 96, 74);

    [Fact]
    public void Each_colour_gets_the_nearest_clearly_darker_palette_colour_if_there_is_one()
    {
        var palette = new Palette();
        int light = palette.GetOrAdd(Light), mid = palette.GetOrAdd(Mid), dark = palette.GetOrAdd(Dark);
        int red = palette.GetOrAdd(new Rgba(200, 30, 30));

        var darker = ShadingPass.DarkerColours(palette, new HashSet<int>());
        Assert.Equal(mid, darker[light]);
        Assert.Equal(dark, darker[mid]);
        Assert.Null(darker[red]);          // no dark red in the palette: left as it is
        Assert.Null(darker[0]);
        Assert.NotEqual(mid, ShadingPass.DarkerColours(palette, new HashSet<int> { mid })[light]);   // skipped colours are never used
    }

    [Fact]
    public void Shading_darkens_the_edges_away_from_the_light_and_only_when_on()
    {
        var c = GoldenTests.Build().Character;
        foreach (var colour in new[] { Light, Mid, Dark })
            c.Palette.GetOrAdd(colour);
        var compositor = new Compositor();
        var plain = compositor.Compose(c, Direction.Front);

        c.Shading.Enabled = true;
        var left = compositor.Compose(c, Direction.Front);
        c.Shading.Light = LightFrom.TopRight;
        var right = compositor.Compose(c, Direction.Front);
        c.Shading.Enabled = false;
        Assert.Equal(plain.Indices, compositor.Compose(c, Direction.Front).Indices);

        Assert.NotEqual(plain.Indices, left.Indices);
        Assert.NotEqual(left.Indices, right.Indices);
        Assert.Equal(plain.Owners, left.Owners);                          // colours only
        for (int i = 0; i < plain.Indices.Length; i++)
            if (left.Indices[i] != plain.Indices[i])
                Assert.NotEqual(CompositeResult.NoPart, plain.Owners[i]);   // nothing outside the parts
    }

    [Fact]
    public void Shading_is_saved_only_while_on()
    {
        var project = GoldenTests.Build();
        Assert.DoesNotContain("shading", ProjectJson(project));

        project.Character.Shading.Enabled = true;
        project.Character.Shading.Light = LightFrom.TopRight;
        project.Character.Shading.Width = 2;
        using var ms = new MemoryStream();
        ProjectFile.Save(project, ms, new RawCodec());
        ms.Position = 0;
        var loaded = ProjectFile.Load(ms, new RawCodec()).Character.Shading;
        Assert.Equal((true, LightFrom.TopRight, 2), (loaded.Enabled, loaded.Light, loaded.Width));
    }

    private static string ProjectJson(ProjectData project)
    {
        using var ms = new MemoryStream();
        ProjectFile.Save(project, ms, new RawCodec());
        using var zip = new System.IO.Compression.ZipArchive(new MemoryStream(ms.ToArray()));
        using var reader = new StreamReader(zip.GetEntry("project.json")!.Open());
        return reader.ReadToEnd();
    }
}
