using System.Runtime.CompilerServices;
using PixelAniMaker.Core.Animation;
using PixelAniMaker.Core.Imaging;
using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.Core.Tests;

/// <summary>The app's built-in templates (read from the repository, as the app ships them).</summary>
public class TemplateTests
{
    private static string Templates([CallerFilePath] string here = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "src", "PixelAniMaker.App", "Assets", "Templates"));

    private static RgbaImage Blank(string _) => new(1, 1, [new Rgba(1, 2, 3)]);

    [Theory]
    [InlineData("chibi96")]
    [InlineData("chibi96_plain")]
    public void Both_mannequins_have_3_4_views_for_every_part_and_can_drop_them(string folder)
    {
        string dir = Path.Combine(Templates(), folder);
        var spec = CharacterSpec.Parse(File.ReadAllText(Path.Combine(dir, "skeleton.json")));
        Assert.Equal(16, spec.Parts.Count);
        foreach (var part in spec.Parts)
            foreach (var view in new[] { "frontleft", "backleft" })
            {
                Assert.True(part.Views.ContainsKey(view), $"{part.Name} has no {view} view");
                Assert.True(File.Exists(Path.Combine(dir, part.Views[view].Image)), part.Views[view].Image);
            }
        // front-left: the far (right) arm is drawn before the chest, the near (left) arm after it
        int Order(string part) => spec.Parts.Single(p => p.Name == part).Views["frontleft"].Order;
        Assert.True(Order("upper_arm_r") < Order("chest") && Order("chest") < Order("upper_arm_l"));

        Assert.True(spec.Build(Blank).HasThreeQuarter);
        var classic = spec.WithoutThreeQuarter();
        Assert.All(classic.Parts, p => Assert.Equal(["back", "front", "left"], p.Views.Keys.Order().ToList()));
        Assert.False(classic.Build(Blank).HasThreeQuarter);
    }

    [Fact]
    public void Default_animations_have_3_4_tracks_that_new_projects_can_leave_out()
    {
        var clips = AnimationJson.Parse(File.ReadAllText(Path.Combine(Templates(), "chibi96", "animations.json")));
        // the six base clips first, then the added ones named "category · variant"
        Assert.Equal(["대기", "걷기", "달리기", "점프", "공격", "피격"], clips.Take(6).Select(c => c.Name));
        Assert.All(clips.Skip(6), c => Assert.Contains(" · ", c.Name));
        Assert.Equal(clips.Count, clips.Select(c => c.Name).Distinct().Count());
        Assert.All(clips, c =>
        {
            Assert.NotEmpty(c.Keys(Direction.FrontLeft));
            Assert.NotEmpty(c.Keys(Direction.BackLeft));
        });
        // the 3/4 tracks are the program's own draft of the classic ones
        var redrafted = AnimationJson.Parse(File.ReadAllText(Path.Combine(Templates(), "chibi96", "animations.json")));
        foreach (var clip in redrafted)
            ThreeQuarterViews.StripFrom(clip);
        Assert.True(ThreeQuarterDraft.Apply(redrafted, new History.UndoHistory()));
        for (int i = 0; i < clips.Count; i++)
            foreach (var d in DirectionExtensions.ThreeQuarterStored)
                Assert.Equal(clips[i].Keys(d).Select(k => (k.Frame, k.Easing)), redrafted[i].Keys(d).Select(k => (k.Frame, k.Easing)));

        foreach (var clip in clips)
            ThreeQuarterViews.StripFrom(clip);
        Assert.All(clips, c => Assert.False(c.HasThreeQuarterData));
    }
}
