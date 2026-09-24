using System.Numerics;
using System.Text.Json;
using PixelAniMaker.Core.Imaging;

namespace PixelAniMaker.Core.Rigging;

/// <summary>A part's look in one direction, as stored in skeleton.json.</summary>
public sealed record PartViewSpec(int X, int Y, float JointX, float JointY, int Order, string Image);

/// <summary>One part entry of skeleton.json. <see cref="Views"/> is keyed "front", "left", "back".</summary>
public sealed record PartSpec(string Name, string Label, string? Parent, IReadOnlyDictionary<string, PartViewSpec> Views);

/// <summary>Contents of skeleton.json: canvas size and the part list.</summary>
public sealed record CharacterSpec(int Width, int Height, IReadOnlyList<PartSpec> Parts)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static CharacterSpec Parse(string json) =>
        JsonSerializer.Deserialize<CharacterSpec>(json, Json) ?? throw new FormatException("Empty skeleton spec.");

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    /// <summary>File name used for a part's image in one direction.</summary>
    public static string ImagePath(Direction direction, string part) => $"{direction.ToString().ToLowerInvariant()}/{part}.png";

    /// <summary>Describes an existing character; images are referenced by <see cref="ImagePath"/>.</summary>
    public static CharacterSpec From(Character character) => new(character.Width, character.Height,
        character.Parts.Select(p => new PartSpec(p.Name, p.Label, p.Parent?.Name,
            DirectionExtensions.Stored.ToDictionary(d => d.ToString().ToLowerInvariant(), d =>
            {
                var v = p.View(d);
                return new PartViewSpec((int)v.RestPosition.X, (int)v.RestPosition.Y, v.RestPivot.X, v.RestPivot.Y,
                    v.DrawOrder, ImagePath(d, p.Name));
            }))).ToList());

    /// <summary>Builds the character; <paramref name="loadImage"/> decodes an image file named in the spec.</summary>
    public Character Build(Func<string, RgbaImage> loadImage, Palette? palette = null)
    {
        var pal = palette ?? new Palette();
        var parts = Parts.ToDictionary(s => s.Name, s => new Part(s.Name, s.Label,
            DirectionExtensions.Stored.ToDictionary(d => d, d => BuildView(s, d, loadImage, pal))));
        foreach (var spec in Parts.Where(s => s.Parent is not null))
        {
            if (!parts.TryGetValue(spec.Parent!, out var parent))
                throw new FormatException($"Part '{spec.Name}' has unknown parent '{spec.Parent}'.");
            parts[spec.Name].AttachTo(parent);
        }
        return new Character(Width, Height, pal, Parts.Select(s => parts[s.Name]));
    }

    private static PartView BuildView(PartSpec part, Direction direction, Func<string, RgbaImage> loadImage, Palette palette)
    {
        string key = direction.ToString().ToLowerInvariant();
        if (!part.Views.TryGetValue(key, out var v))
            throw new FormatException($"Part '{part.Name}' has no '{key}' view.");
        var rgba = loadImage(v.Image);
        var image = IndexedImage.FromRgba(rgba.Width, rgba.Height, rgba.Pixels, palette);
        return new PartView(image, new Vector2(v.X, v.Y), new Vector2(v.JointX, v.JointY), v.Order);
    }
}
