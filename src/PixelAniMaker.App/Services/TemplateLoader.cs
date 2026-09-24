using Avalonia.Platform;
using PixelAniMaker.Core.Animation;
using PixelAniMaker.Core.Imaging;
using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.App.Services;

/// <summary>Loads the built-in 4-head mannequin (16 parts, front/left/back) from the app assets.</summary>
public static class TemplateLoader
{
    private const string Folder = "avares://PixelAniMaker/Assets/Templates/chibi96/";
    private const string PlainFolder = "avares://PixelAniMaker/Assets/Templates/chibi96_plain/";

    /// <summary>Mannequin line colours: silhouette outline and the line where parts overlap.</summary>
    private static readonly Rgba OutlineColor = new(52, 40, 34);
    private static readonly Rgba InnerLineColor = new(120, 96, 74);

    /// <param name="jointDiscs">False loads the plain mannequin without ball-joint circles.</param>
    public static Character LoadChibi96(bool jointDiscs = true)
    {
        string folder = jointDiscs ? Folder : PlainFolder;
        var spec = CharacterSpec.Parse(ReadText(folder, "skeleton.json"));
        var palette = new Palette();
        var character = spec.Build(file => LoadRgba(new Uri(folder + file)), palette);
        SeedPalette(palette);
        character.Outline.OutlineIndex = palette.GetOrAdd(OutlineColor);
        character.Outline.InnerIndex = palette.GetOrAdd(InnerLineColor);
        character.Outline.Enabled = true;
        return character;
    }

    /// <summary>The default clips (idle, walk, run, jump, attack, hit).</summary>
    public static IReadOnlyList<AnimationClip> LoadDefaultAnimations() => AnimationJson.Parse(ReadText(Folder, "animations.json"));

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
