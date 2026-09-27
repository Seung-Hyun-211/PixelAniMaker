using Avalonia.Platform;
using PixelAniMaker.Core.Animation;
using PixelAniMaker.Core.Imaging;
using PixelAniMaker.Core.Rigging;
using PixelAniMaker.Core.Rigging.Body;

namespace PixelAniMaker.App.Services;

/// <summary>
/// Loads the mannequins new projects start from: the hand-drawn 4-head one (16 parts, front/left/back) from the
/// app assets, on a 128×160 canvas (the 96×128 template with <see cref="CanvasMargins.Default"/> around it), or
/// a generated one of any other head count in <see cref="BodyProportions.Table"/> or any other
/// <see cref="BodyShape"/>, with the same parts.
/// </summary>
public static class TemplateLoader
{
    private const string Folder = "avares://PixelAniMaker/Assets/Templates/chibi96/";
    private const string PlainFolder = "avares://PixelAniMaker/Assets/Templates/chibi96_plain/";

    /// <summary>Mannequin line colours: silhouette outline and the line where parts overlap.</summary>
    private static readonly Rgba OutlineColor = new(52, 40, 34);
    private static readonly Rgba InnerLineColor = new(120, 96, 74);

    /// <summary>The hand-drawn template's head count.</summary>
    public const double ChibiHeads = 4;

    /// <summary>The body types a new project can start from, in head counts.</summary>
    public static IReadOnlyList<double> BodyTypes { get; } = BodyProportions.Table.Select(r => r.Heads).ToList();

    /// <summary>The builds a new project can start from.</summary>
    public static IReadOnlyList<BodyShape> BodyShapes { get; } = Enum.GetValues<BodyShape>();

    /// <param name="heads">
    /// Head count: <see cref="ChibiHeads"/> with the standard shape loads the hand-drawn template, the rest is generated.
    /// </param>
    /// <param name="jointDiscs">False loads the plain mannequin without ball-joint circles.</param>
    /// <param name="threeQuarter">True keeps the template's 3/4 views (the project is then saved as format 2).</param>
    /// <param name="shape">Build: widths and thicknesses of the body (see <see cref="BodyShapeFactors"/>).</param>
    public static Character LoadMannequin(double heads = ChibiHeads, bool jointDiscs = true, bool threeQuarter = false,
        BodyShape shape = BodyShape.Standard)
    {
        if (heads == ChibiHeads && shape == BodyShape.Standard)
        {
            string folder = jointDiscs ? Folder : PlainFolder;
            return Finish(CharacterSpec.Parse(ReadText(folder, "skeleton.json")), file => LoadRgba(new Uri(folder + file)), threeQuarter);
        }
        var generated = MannequinBuilder.Build(BodyProportions.For(heads, shape: shape), jointDiscs);
        return Finish(generated.Spec, file => generated.Images[file], threeQuarter);
    }

    private static Character Finish(CharacterSpec spec, Func<string, RgbaImage> loadImage, bool threeQuarter)
    {
        if (!threeQuarter)
            spec = spec.WithoutThreeQuarter();
        var palette = new Palette();
        var character = spec.Build(loadImage, palette);
        SeedPalette(palette);
        character.Outline.OutlineIndex = palette.GetOrAdd(OutlineColor);
        character.Outline.InnerIndex = palette.GetOrAdd(InnerLineColor);
        character.Outline.Enabled = true;
        CanvasResize.ApplyUnrecorded(character, CanvasMargins.Default);   // room for jumps, raised arms, hats and tails
        return character;
    }

    /// <summary>
    /// True for the six base clips (idle, walk, run, jump, attack, hit); the library's other clips are named
    /// "category · variant" (variants of the six, other actions, one-frame poses).
    /// </summary>
    public static bool IsBaseClip(AnimationClip clip) => !clip.Name.Contains('·');

    /// <summary>The default clips: the six base clips, or with <paramref name="library"/> every clip of the library.</summary>
    /// <param name="threeQuarter">True keeps their 3/4 tracks; otherwise they are dropped (format 1 projects).</param>
    /// <param name="heads">The body they are for: body moves (jump height and the like) grow with its height.</param>
    public static IReadOnlyList<AnimationClip> LoadDefaultAnimations(bool threeQuarter = false, double heads = ChibiHeads, bool library = false)
    {
        var clips = AnimationJson.Parse(ReadText(Folder, "animations.json")).Where(c => library || IsBaseClip(c)).ToList();
        float scale = (float)(BodyProportions.For(heads).BodyHeightPixels / BodyProportions.For(ChibiHeads).BodyHeightPixels);
        foreach (var clip in clips)
        {
            clip.Name = Localizer.T(clip.Name); // names are data: new projects get them in the UI language
            if (!threeQuarter)
                ThreeQuarterViews.StripFrom(clip);
            if (heads != ChibiHeads)
                clip.ScaleOffsets(scale);
        }
        return clips;
    }

    /// <summary>
    /// The library clips (all but the six base ones) for an existing character: body moves are scaled to its drawn
    /// height at rest, as a new project of that height would get them. 3/4 tracks stay; importing drops them when
    /// the character has no 3/4 views.
    /// </summary>
    public static IReadOnlyList<AnimationClip> LibraryClipsFor(Character character, Compositor compositor)
    {
        var rest = compositor.Compose(character, Direction.Front, PoseData.Rest);
        int top = int.MaxValue, bottom = -1;
        for (int i = 0; i < rest.Owners.Length; i++)
            if (rest.Owners[i] != CompositeResult.NoPart)
            {
                top = Math.Min(top, i / rest.Width);
                bottom = Math.Max(bottom, i / rest.Width);
            }
        float scale = bottom < 0 ? 1 : (float)((bottom - top + 1) / BodyProportions.For(ChibiHeads).BodyHeightPixels);
        var clips = AnimationJson.Parse(ReadText(Folder, "animations.json")).Where(c => !IsBaseClip(c)).ToList();
        foreach (var clip in clips)
        {
            clip.Name = Localizer.T(clip.Name);
            if (MathF.Abs(scale - 1) > 0.02f)
                clip.ScaleOffsets(scale);
        }
        return clips;
    }

    private static string ReadText(string folder, string file)
    {
        using var reader = new StreamReader(AssetLoader.Open(new Uri(folder + file)));
        return reader.ReadToEnd();
    }

    private static RgbaImage LoadRgba(Uri uri)
    {
        using var stream = AssetLoader.Open(uri);
        return AvaloniaImageCodec.Instance.DecodePng(stream);
    }

    /// <summary>Adds a few basic colours after the mannequin colours.</summary>
    private static void SeedPalette(Palette palette)
    {
        Rgba[] colors =
        [
            new(0, 0, 0), new(255, 255, 255),
            new(170, 40, 50), new(230, 120, 60), new(240, 200, 80), new(80, 160, 80),
            new(60, 110, 180), new(120, 80, 160),
        ];
        foreach (var c in colors)
            palette.GetOrAdd(c);
    }
}
