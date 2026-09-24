using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using PixelAniMaker.Core.Imaging;

namespace PixelAniMaker.App.Services;

/// <summary>Loads the built-in mannequin template and its colours.</summary>
public static class TemplateLoader
{
    private static readonly Uri FrontUri = new("avares://PixelAniMaker/Assets/Templates/chibi_front_64x128.png");

    public static Bitmap? LoadFront64x128()
    {
        try
        {
            using var stream = AssetLoader.Open(FrontUri);
            return new Bitmap(stream);
        }
        catch (Exception ex) when (ex is FileNotFoundException or IOException)
        {
            return null;
        }
    }

    /// <summary>Adds the mannequin colours plus a few basics to a new palette.</summary>
    public static void SeedPalette(Palette palette)
    {
        Rgba[] colors =
        [
            new(52, 40, 34), new(120, 96, 74), new(150, 126, 98), new(176, 150, 120),
            new(190, 170, 146), new(222, 206, 184),
            new(170, 40, 50), new(230, 120, 60), new(240, 200, 80), new(80, 160, 80),
            new(60, 110, 180), new(120, 80, 160),
        ];
        foreach (var c in colors)
            palette.GetOrAdd(c);
    }
}
