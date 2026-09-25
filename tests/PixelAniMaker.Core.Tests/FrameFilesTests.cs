using PixelAniMaker.Core.Animation;
using PixelAniMaker.Core.Export;
using PixelAniMaker.Core.History;
using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.Core.Tests;

public class FrameFilesTests
{
    [Fact]
    public void Every_frame_file_is_the_matching_sheet_cell()
    {
        var project = GoldenTests.Build();
        var c = project.Character;
        ThreeQuarterViews.Enable(c, new UndoHistory());
        var directions = c.ExportDirections(includeThreeQuarter: true);

        var sheet = SpriteSheet.Build(c, project.Clips, new Compositor(), directions);
        var files = FrameFiles.Build(c, project.Clips, new Compositor(), "hero", directions);

        Assert.Equal(project.Clips.Sum(clip => clip.FrameCount) * 8, files.Count);
        var byName = files.ToDictionary(f => f.FileName);
        foreach (var clip in sheet.Clips)
            foreach (var (direction, frames) in clip.Directions)
                for (int f = 0; f < frames.Count; f++)
                {
                    var image = byName[$"hero_{clip.Name}_{direction}_{f + 1:D2}.png"].Image;
                    Assert.Equal((c.Width, c.Height), (image.Width, image.Height));
                    for (int y = 0; y < c.Height; y++)
                        Assert.True(image.Pixels.AsSpan(y * c.Width, c.Width)
                            .SequenceEqual(sheet.Image.Pixels.AsSpan((frames[f].Y + y) * sheet.Image.Width + frames[f].X, c.Width)),
                            $"{clip.Name} {direction} {f + 1} row {y}");
                }
    }

    [Fact]
    public void Names_follow_the_timeline_numbers_and_stay_unique_and_valid()
    {
        var c = GoldenTests.Build().Character;
        AnimationClip[] clips = [new("walk/run", 12), new("walk/run", 1)];

        var names = FrameFiles.Build(c, clips, new Compositor(), "a", [Direction.Front]).Select(f => f.FileName).ToList();

        Assert.Equal(13, names.Count);
        Assert.Equal("a_walk_run_front_01.png", names[0]);
        Assert.Equal("a_walk_run_front_12.png", names[11]);
        Assert.Equal("a_walk_run_2_front_01.png", names[12]);
        Assert.Equal(names.Count, names.Distinct().Count());
        Assert.Equal(FrameFiles.Build(c, [new AnimationClip("idle", 100)], new Compositor(), "a", [Direction.Front])[0].FileName,
            "a_idle_front_001.png");
    }
}
