using System.IO.Compression;
using System.Numerics;
using System.Text;
using PixelAniMaker.Core.Animation;
using PixelAniMaker.Core.Export;
using PixelAniMaker.Core.History;
using PixelAniMaker.Core.Imaging;
using PixelAniMaker.Core.Project;
using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.Core.Tests;

public class ThreeQuarterTests
{
    private static PoseData Pose(string part, double degrees) => new(new Dictionary<string, double> { [part] = degrees }, Vector2.Zero);

    private static byte[] Save(ProjectData project)
    {
        using var ms = new MemoryStream();
        ProjectFile.Save(project, ms, new RawCodec());
        return ms.ToArray();
    }

    private static string Entry(byte[] zip, string name)
    {
        using var archive = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
        using var reader = new StreamReader(archive.GetEntry(name)!.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static ProjectData Load(byte[] zip) => ProjectFile.Load(new MemoryStream(zip), new RawCodec());

    private static bool SamePixels(PartView a, PartView b) => a.Image.Pixels.SequenceEqual(b.Image.Pixels);

    [Fact]
    public void A_character_has_the_classic_directions_until_3_4_views_are_turned_on()
    {
        var c = GoldenTests.Build().Character;
        Assert.False(c.HasThreeQuarter);
        Assert.Equal(DirectionExtensions.All, c.Directions);
        Assert.Equal(DirectionExtensions.Stored, c.StoredDirections);
        var body = c.Find("body")!;
        Assert.Same(body.View(Direction.Front), body.View(Direction.FrontLeft));   // fallback
        Assert.Same(body.View(Direction.Back), body.View(Direction.BackRight));
    }

    [Fact]
    public void Turning_3_4_views_on_copies_front_and_back_and_is_one_undo_step()
    {
        var c = GoldenTests.Build().Character;
        var history = new UndoHistory();
        int changed = 0;
        c.DirectionsChanged += (_, _) => changed++;

        ThreeQuarterViews.Enable(c, history);
        Assert.True(c.HasThreeQuarter);
        Assert.Equal(DirectionExtensions.Every, c.Directions);
        foreach (var part in c.Parts)
        {
            Assert.True(part.HasOwnView(Direction.FrontLeft) && part.HasOwnView(Direction.BackLeft));
            Assert.NotSame(part.View(Direction.Front).Image, part.View(Direction.FrontLeft).Image);
            Assert.True(SamePixels(part.View(Direction.Front), part.View(Direction.FrontLeft)));
            Assert.True(SamePixels(part.View(Direction.Back), part.View(Direction.BackLeft)));
        }

        history.Undo();
        Assert.False(c.HasThreeQuarter);
        Assert.All(c.Parts, p => Assert.False(p.HasOwnView(Direction.FrontLeft)));
        Assert.Equal(2, changed);
    }

    [Fact]
    public void Right_facing_3_4_views_mirror_the_left_facing_ones_until_drawn_separately()
    {
        var c = GoldenTests.Build().Character;
        ThreeQuarterViews.Enable(c, new UndoHistory());
        var compositor = new Compositor();
        var left = compositor.Compose(c, Direction.FrontLeft);
        var right = compositor.Compose(c, Direction.FrontRight);
        left.MirrorHorizontally();
        Assert.Equal(left.Indices, right.Indices);

        var body = c.Find("body")!;
        var history = new UndoHistory();
        RightViewChange.Apply(body, history, separate: true, Direction.FrontRight);
        Assert.True(body.HasOwnView(Direction.FrontRight));
        Assert.NotSame(body.View(Direction.FrontLeft), body.View(Direction.FrontRight));
        history.Undo();
        Assert.Same(body.View(Direction.FrontLeft), body.View(Direction.FrontRight));
    }

    [Fact]
    public void An_empty_3_4_track_plays_the_front_or_back_track()
    {
        var clip = new AnimationClip("a", 4);
        clip.SetKey(Direction.Front, new Keyframe(0, Pose("arm_r", 40)));
        clip.SetKey(Direction.Back, new Keyframe(0, Pose("arm_r", -40)));
        Assert.Equal(40, clip.Evaluate(Direction.FrontLeft, 1).Get("arm_r"));
        Assert.Equal(40, clip.Evaluate(Direction.FrontRight, 1).Get("arm_r"));
        Assert.Equal(-40, clip.Evaluate(Direction.BackRight, 1).Get("arm_r"));
        Assert.False(clip.HasThreeQuarterData);

        // a key set on the right-facing view goes to the left-facing track, which then plays itself
        clip.SetKey(Direction.FrontRight, new Keyframe(0, Pose("arm_r", 10)));
        Assert.Single(clip.Keys(Direction.FrontLeft));
        Assert.Equal(10, clip.Evaluate(Direction.FrontLeft, 1).Get("arm_r"));
        Assert.Equal(40, clip.Evaluate(Direction.Front, 1).Get("arm_r"));
        Assert.True(clip.HasThreeQuarterData);
    }

    [Fact]
    public void Projects_with_3_4_views_save_as_format_2_and_load_back()
    {
        var project = GoldenTests.Build();
        var c = project.Character;
        ThreeQuarterViews.Enable(c, new UndoHistory());
        int red = c.Palette.GetOrAdd(new Rgba(255, 0, 0));
        c.Find("head")!.View(Direction.FrontLeft).Image.Set(1, 1, red);
        var wave = project.Clips[0];
        wave.SetKey(Direction.FrontLeft, new Keyframe(1, Pose("arm_r", 75)));
        wave.Touchups.Set(Direction.FrontRight, 0, new PixelOverrides { [(3, 3)] = 1 });

        var zip = Save(project);
        Assert.Contains("\"formatVersion\": 2", Entry(zip, "project.json"));
        Assert.Contains("\"frontleft\"", Entry(zip, "skeleton.json"));

        var loaded = Load(zip);
        Assert.True(loaded.Character.HasThreeQuarter);
        var head = loaded.Character.Find("head")!.View(Direction.FrontLeft);
        Assert.Equal(new Rgba(255, 0, 0), loaded.Character.Palette[head.Image[1, 1]]);
        Assert.True(SamePixels(loaded.Character.Find("body")!.View(Direction.BackLeft), c.Find("body")!.View(Direction.BackLeft)));
        var lw = loaded.Clips[0];
        Assert.Equal(75, lw.Keys(Direction.FrontLeft).Single().Pose.Get("arm_r"));
        Assert.True(lw.Touchups.Has(Direction.FrontRight, 0));
    }

    [Fact]
    public void Turning_3_4_views_off_removes_clip_data_and_saves_as_format_1_again()
    {
        var project = GoldenTests.Build();
        var c = project.Character;
        var history = new UndoHistory();
        ThreeQuarterViews.Enable(c, history);
        var wave = project.Clips[0];
        wave.SetKey(Direction.BackLeft, new Keyframe(0, Pose("body", 3)));
        wave.Touchups.Set(Direction.FrontLeft, 2, new PixelOverrides { [(1, 1)] = 1 });

        ThreeQuarterViews.Disable(c, project.Clips, history);
        Assert.False(c.HasThreeQuarter);
        Assert.False(wave.HasThreeQuarterData);
        var zip = Save(project);
        Assert.Contains("\"formatVersion\": 1", Entry(zip, "project.json"));
        Assert.DoesNotContain("frontleft", Entry(zip, "skeleton.json"));
        Assert.DoesNotContain("backleft", Entry(zip, "animations.json"));

        history.Undo();
        Assert.True(c.HasThreeQuarter);
        Assert.Single(wave.Keys(Direction.BackLeft));
        Assert.True(wave.Touchups.Has(Direction.FrontLeft, 2));
    }

    [Fact]
    public void Turning_off_only_clip_data_keeps_the_views_off_after_undo()
    {
        var project = GoldenTests.Build();
        var history = new UndoHistory();
        project.Clips[0].SetKey(Direction.FrontLeft, new Keyframe(0, Pose("body", 3)));
        ThreeQuarterViews.Disable(project.Character, project.Clips, history);
        Assert.False(project.Clips[0].HasThreeQuarterData);
        history.Undo();
        Assert.False(project.Character.HasThreeQuarter);
        Assert.True(project.Clips[0].HasThreeQuarterData);
    }

    [Fact]
    public void Importing_clips_drops_3_4_keys_only_for_characters_without_3_4_views()
    {
        var clip = new AnimationClip("run", 2);
        clip.SetKey(Direction.Front, new Keyframe(0, Pose("body", 1)));
        clip.SetKey(Direction.FrontLeft, new Keyframe(0, Pose("body", 2)));

        var classic = GoldenTests.Build().Character;
        var toClassic = ClipImport.Import([clip], [], classic);
        Assert.True(toClassic.DroppedThreeQuarter);
        Assert.Empty(toClassic.Clips[0].Keys(Direction.FrontLeft));
        Assert.Single(clip.Keys(Direction.FrontLeft));   // the source clip is untouched

        var withViews = GoldenTests.Build().Character;
        ThreeQuarterViews.Enable(withViews, new UndoHistory());
        var toViews = ClipImport.Import([clip], [], withViews);
        Assert.False(toViews.DroppedThreeQuarter);
        Assert.Single(toViews.Clips[0].Keys(Direction.FrontLeft));
    }

    [Fact]
    public void An_eight_direction_sheet_keeps_each_clips_classic_rows_first_and_adds_3_4_rows_after()
    {
        var project = GoldenTests.Build();
        var c = project.Character;
        ThreeQuarterViews.Enable(c, new UndoHistory());
        c.Find("body")!.View(Direction.FrontLeft).Image.Set(2, 2, c.Palette.GetOrAdd(new Rgba(255, 0, 0)));

        var four = SpriteSheet.Build(c, project.Clips, new Compositor());
        var eight = SpriteSheet.Build(c, project.Clips, new Compositor(), c.ExportDirections(includeThreeQuarter: true));
        Assert.Equal(four.Image.Width, eight.Image.Width);
        Assert.Equal(four.Image.Height * 2, eight.Image.Height);
        int rowPixels = four.Image.Width * c.Height;
        for (int clip = 0; clip < project.Clips.Count; clip++)
            Assert.True(four.Image.Pixels.AsSpan(clip * 4 * rowPixels, 4 * rowPixels)
                .SequenceEqual(eight.Image.Pixels.AsSpan(clip * 8 * rowPixels, 4 * rowPixels)), $"clip {clip}: classic rows differ");

        var clipDirections = eight.Clips[0].Directions.Keys.ToList();
        Assert.Equal(["front", "left", "right", "back", "frontleft", "frontright", "backleft", "backright"], clipDirections);
        Assert.Equal(["front", "left", "right", "back"], four.Clips[0].Directions.Keys.ToList());
        Assert.Equal(4 * c.Height, eight.Clips[0].Directions["frontleft"][0].Y);   // row 5 of the first clip
    }

    [Fact]
    public void Characters_without_3_4_views_export_four_directions_even_when_eight_are_asked_for()
    {
        var c = GoldenTests.Build().Character;
        Assert.Equal(DirectionExtensions.All, c.ExportDirections(includeThreeQuarter: true));
        ThreeQuarterViews.Enable(c, new UndoHistory());
        Assert.Equal(DirectionExtensions.All, c.ExportDirections(includeThreeQuarter: false));
        Assert.Equal(DirectionExtensions.Every, c.ExportDirections(includeThreeQuarter: true));

        using var four = new MemoryStream();
        using var eight = new MemoryStream();
        var clip = GoldenTests.Build().Clips[0];
        AnimationGif.Write(four, c, clip, new Compositor(), 1);
        AnimationGif.Write(eight, c, clip, new Compositor(), 1, DirectionExtensions.Every);
        Assert.Equal(c.Width * 4, BitConverter.ToUInt16(four.ToArray(), 6));    // GIF logical screen width
        Assert.Equal(c.Width * 8, BitConverter.ToUInt16(eight.ToArray(), 6));
    }

    [Fact]
    public void Files_from_a_newer_format_are_refused_with_a_clear_message()
    {
        var zip = Save(GoldenTests.Build());
        using var edited = new MemoryStream();
        using (var source = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read))
        using (var target = new ZipArchive(edited, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var e in source.Entries)
            {
                using var from = e.Open();
                using var to = target.CreateEntry(e.FullName).Open();
                if (e.FullName == "project.json")
                    to.Write(Encoding.UTF8.GetBytes(new StreamReader(from).ReadToEnd()
                        .Replace("\"formatVersion\": 1", $"\"formatVersion\": {ProjectFile.FormatVersion + 1}")));
                else
                    from.CopyTo(to);
            }
        edited.Position = 0;
        var error = Assert.Throws<FormatException>(() => ProjectFile.Load(edited, new RawCodec()));
        Assert.Contains("newer version", error.Message);
    }

    [Fact]
    public void Eye_parts_added_after_3_4_views_are_turned_towards_the_face_side()
    {
        var c = EyePartsTests.HeadAndBody();   // a head of realistic size (33 px), so eye widths can differ
        ThreeQuarterViews.Enable(c, new UndoHistory());
        EyeParts.Add(c);
        var near = c.Find(EyeParts.Left)!;
        var far = c.Find(EyeParts.Right)!;
        Assert.True(near.HasOwnView(Direction.FrontLeft) && near.HasOwnView(Direction.BackLeft));

        // front-3/4 faces screen-left: both eyes move left of where they sit in the front view,
        // and the far eye is drawn narrower than the near one
        Assert.True(near.View(Direction.FrontLeft).RestPivot.X < near.View(Direction.Front).RestPivot.X);
        Assert.True(far.View(Direction.FrontLeft).RestPivot.X < far.View(Direction.Front).RestPivot.X);
        Assert.True(far.View(Direction.FrontLeft).RestPivot.X < near.View(Direction.FrontLeft).RestPivot.X);
        static int Width(PartView v)
        {
            var xs = Enumerable.Range(0, v.Image.Width * v.Image.Height).Where(i => v.Image.Pixels[i] != 0).Select(i => i % v.Image.Width).ToList();
            return xs.Max() - xs.Min() + 1;
        }
        Assert.True(Width(far.View(Direction.FrontLeft)) < Width(near.View(Direction.FrontLeft)));
        Assert.All(new[] { near, far }, e => Assert.All(e.View(Direction.BackLeft).Image.Pixels.ToArray(), p => Assert.Equal(0, p)));
    }

    [Fact]
    public void Undo_names_say_which_mirrored_view_was_separated()
    {
        var c = GoldenTests.Build().Character;
        ThreeQuarterViews.Enable(c, new UndoHistory());
        var history = new UndoHistory();
        RightViewChange.Apply(c.Find("body")!, history, separate: true);
        RightViewChange.Apply(c.Find("body")!, history, separate: true, Direction.FrontRight);
        Assert.Equal("우측면 따로 그리기", history.Done[0].Name);
        Assert.Equal("앞 반측면 (우) 따로 그리기", history.Done[1].Name);
    }
}
