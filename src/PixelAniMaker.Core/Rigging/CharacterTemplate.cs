using System.Numerics;
using System.Text.Json;
using PixelAniMaker.Core.Imaging;

namespace PixelAniMaker.Core.Rigging;

/// <summary>An RGBA image as decoded by the UI layer.</summary>
public sealed record RgbaImage(int Width, int Height, Rgba[] Pixels);

/// <summary>One part entry of skeleton.json.</summary>
public sealed record PartSpec(
    string Name, string Label, string? Parent, int Order,
    int X, int Y, float JointX, float JointY, string Image);

/// <summary>Contents of skeleton.json: canvas size and the part list.</summary>
public sealed record CharacterSpec(int Width, int Height, IReadOnlyList<PartSpec> Parts)
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public static CharacterSpec Parse(string json) =>
        JsonSerializer.Deserialize<CharacterSpec>(json, Json) ?? throw new FormatException("Empty skeleton spec.");

    /// <summary>Builds the character; <paramref name="loadImage"/> decodes a part's image file.</summary>
    public Character Build(Func<string, RgbaImage> loadImage, Palette? palette = null)
    {
        palette ??= new Palette();
        var parts = Parts.ToDictionary(s => s.Name, s =>
        {
            var rgba = loadImage(s.Image);
            var image = IndexedImage.FromRgba(rgba.Width, rgba.Height, rgba.Pixels, palette);
            return new Part(s.Name, s.Label, image, new Vector2(s.X, s.Y), new Vector2(s.JointX, s.JointY), s.Order);
        });
        foreach (var spec in Parts.Where(s => s.Parent is not null))
        {
            if (!parts.TryGetValue(spec.Parent!, out var parent))
                throw new FormatException($"Part '{spec.Name}' has unknown parent '{spec.Parent}'.");
            parts[spec.Name].AttachTo(parent);
        }
        return new Character(Width, Height, palette, parts.Values);
    }
}
