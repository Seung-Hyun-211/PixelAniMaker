using System.Numerics;
using System.Text.Json;
using PixelAniMaker.Core.Imaging;

namespace PixelAniMaker.Core.Rigging;

/// <summary>An angle variant image of a part view.</summary>
public sealed record VariantSpec(int Angle, float PivotX, float PivotY, string Image);

/// <summary>A drawing layer of a part view; the first one uses the view's own image file.</summary>
public sealed record LayerSpec(string Name, string Image, bool Visible = true);

/// <summary>A named attachment point in base-image pixels.</summary>
public sealed record AttachmentSpec(string Name, float X, float Y);

/// <summary>A part's look in one direction, as stored in skeleton.json.</summary>
public sealed record PartViewSpec(int X, int Y, float JointX, float JointY, int Order, string Image,
    IReadOnlyList<VariantSpec>? Variants = null, IReadOnlyList<AttachmentSpec>? Attachments = null,
    IReadOnlyList<LayerSpec>? Layers = null);

/// <summary>
/// One part entry of skeleton.json. <see cref="Views"/> is keyed "front", "left", "back", plus "right"
/// when the part's right view is drawn separately. A right view is stored in the same orientation as
/// the left view (the program mirrors it for display), so its joints and angles match the left view.
/// <see cref="Detail"/> marks a detail part such as an eye (omitted when false; older versions ignore it).
/// </summary>
public sealed record PartSpec(string Name, string Label, string? Parent, IReadOnlyDictionary<string, PartViewSpec> Views,
    bool? Detail = null);

/// <summary>Contents of skeleton.json: canvas size and the part list.</summary>
public sealed record CharacterSpec(int Width, int Height, IReadOnlyList<PartSpec> Parts)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static CharacterSpec Parse(string json) =>
        JsonSerializer.Deserialize<CharacterSpec>(json, Json) ?? throw new FormatException("Empty skeleton spec.");

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    /// <summary>File name used for a part's image in one direction.</summary>
    public static string ImagePath(Direction direction, string part, int? angle = null) =>
        $"{direction.ToString().ToLowerInvariant()}/{part}{(angle is { } a ? $"@{a}" : "")}.png";

    /// <summary>Directions a part has its own data for (mirrored ones only when drawn separately, 3/4 only when on).</summary>
    private static IEnumerable<Direction> OwnDirections(Part part) => DirectionExtensions.Every.Where(part.HasOwnView);

    /// <summary>File of a view's layer: the view's own image for the first layer, "part.layerN.png" for the others.</summary>
    public static string LayerPath(Direction direction, string part, int layer) =>
        layer == 0 ? ImagePath(direction, part) : $"{direction.ToString().ToLowerInvariant()}/{part}.layer{layer}.png";

    /// <summary>Every image a character needs saved: path → image (layers and angle variants).</summary>
    public static IEnumerable<(string Path, IndexedImage Image)> Images(Character character) =>
        from p in character.Parts
        from d in OwnDirections(p)
        let v = p.View(d)
        from item in v.Layers.All.Select((l, i) => (LayerPath(d, p.Name, i), l.Image))
            .Concat(v.Variants.All.Select(kv => (ImagePath(d, p.Name, kv.Key), kv.Value.Image)))
        select item;

    /// <summary>Layer list for skeleton.json, or null for a plain single-layer view.</summary>
    private static List<LayerSpec>? LayerSpecs(PartView view, Direction direction, string part) =>
        view.Layers.All is [{ Name: PartLayers.BaseName, Visible: true }]
            ? null
            : view.Layers.All.Select((l, i) => new LayerSpec(l.Name, LayerPath(direction, part, i), l.Visible)).ToList();

    /// <summary>Describes an existing character; images are referenced by <see cref="ImagePath"/>.</summary>
    public static CharacterSpec From(Character character) => new(character.Width, character.Height,
        character.Parts.Select(p => new PartSpec(p.Name, p.Label, p.Parent?.Name,
            OwnDirections(p).ToDictionary(d => d.ToString().ToLowerInvariant(), d =>
            {
                var v = p.View(d);
                var variants = v.Variants.All.Select(kv => new VariantSpec(kv.Key, kv.Value.LocalPivot.X, kv.Value.LocalPivot.Y,
                    ImagePath(d, p.Name, kv.Key))).ToList();
                var attachments = v.Attachments.All.Select(kv => new AttachmentSpec(kv.Key, kv.Value.X, kv.Value.Y)).ToList();
                return new PartViewSpec((int)v.RestPosition.X, (int)v.RestPosition.Y, v.RestPivot.X, v.RestPivot.Y,
                    v.DrawOrder, ImagePath(d, p.Name), variants.Count > 0 ? variants : null,
                    attachments.Count > 0 ? attachments : null, LayerSpecs(v, d, p.Name));
            }), p.IsDetail ? true : null)).ToList());

    /// <summary>Builds the character; <paramref name="loadImage"/> decodes an image file named in the spec.</summary>
    public Character Build(Func<string, RgbaImage> loadImage, Palette? palette = null)
    {
        var pal = palette ?? new Palette();
        var parts = Parts.ToDictionary(s => s.Name, s => new Part(s.Name, s.Label,
            DirectionExtensions.Every
                .Where(d => DirectionExtensions.Stored.Contains(d) || s.Views.ContainsKey(d.Key()))
                .ToDictionary(d => d, d => BuildView(s, d, loadImage, pal))) { IsDetail = s.Detail == true });
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
        string key = direction.Key();
        if (!part.Views.TryGetValue(key, out var v))
            throw new FormatException($"Part '{part.Name}' has no '{key}' view.");
        var baseImage = Load(v.Image);
        var view = new PartView(baseImage, new Vector2(v.X, v.Y), new Vector2(v.JointX, v.JointY), v.Order);
        foreach (var variant in v.Variants ?? [])
            view.Variants.Set(variant.Angle, new PartVariant(Load(variant.Image), new Vector2(variant.PivotX, variant.PivotY)));
        if (v.Layers is { Count: > 0 } layers)
        {
            view.Layers.Items.Clear();
            for (int i = 0; i < layers.Count; i++)
            {
                var image = i == 0 ? baseImage : Load(layers[i].Image);
                if (image.Width != baseImage.Width || image.Height != baseImage.Height)
                    throw new FormatException($"Layer '{layers[i].Name}' of '{part.Name}' has a different size.");
                view.Layers.Items.Add(new PartLayer(layers[i].Name, image, layers[i].Visible));
            }
        }
        foreach (var a in v.Attachments ?? [])
            view.Attachments.Set(a.Name, new Vector2(a.X, a.Y));
        return view;

        IndexedImage Load(string file)
        {
            var rgba = loadImage(file);
            return IndexedImage.FromRgba(rgba.Width, rgba.Height, rgba.Pixels, palette);
        }
    }
}
