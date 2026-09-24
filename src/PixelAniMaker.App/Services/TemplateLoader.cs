using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using PixelAniMaker.Core.Animation;
using PixelAniMaker.Core.Imaging;
using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.App.Services;

/// <summary>Loads the built-in 4-head mannequin (16 parts, front/left/back) from the app assets.</summary>
public static class TemplateLoader
{
    private const string Folder = "avares://PixelAniMaker/Assets/Templates/chibi96/";

    /// <summary>Mannequin line colours: silhouette outline and the line where parts overlap.</summary>
    private static readonly Rgba OutlineColor = new(52, 40, 34);
    private static readonly Rgba InnerLineColor = new(120, 96, 74);

    public static Character LoadChibi96()
    {
        var spec = CharacterSpec.Parse(ReadText("skeleton.json"));
        var palette = new Palette();
        var character = spec.Build(file => LoadRgba(new Uri(Folder + file)), palette);
        SeedPalette(palette);
        character.Outline.OutlineIndex = palette.GetOrAdd(OutlineColor);
        character.Outline.InnerIndex = palette.GetOrAdd(InnerLineColor);
        character.Outline.Enabled = true;
        return character;
    }

    /// <summary>The default clips (idle, walk, run, jump, attack, hit).</summary>
    public static IReadOnlyList<AnimationClip> LoadDefaultAnimations() => AnimationJson.Parse(ReadText("animations.json"));

    private static string ReadText(string file)
    {
        using var reader = new StreamReader(AssetLoader.Open(new Uri(Folder + file)));
        return reader.ReadToEnd();
    }

    private static unsafe RgbaImage LoadRgba(Uri uri)
    {
        using var stream = AssetLoader.Open(uri);
        using var bitmap = new Bitmap(stream);
        var size = bitmap.PixelSize;
        var raw = new uint[size.Width * size.Height];
        fixed (uint* p = raw)
            bitmap.CopyPixels(new PixelRect(size), (nint)p, raw.Length * 4, size.Width * 4);

        bool rgbaOrder = bitmap.Format == PixelFormat.Rgba8888;
        var pixels = new Rgba[raw.Length];
        for (int i = 0; i < raw.Length; i++)
        {
            var c = Rgba.FromBgra32(raw[i]);
            pixels[i] = rgbaOrder ? new Rgba(c.B, c.G, c.R, c.A) : c;
            if (pixels[i].A < 128)
                pixels[i] = Rgba.Transparent;
        }
        return new RgbaImage(size.Width, size.Height, pixels);
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
