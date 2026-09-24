using System.IO.Compression;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using PixelAniMaker.Core.Animation;
using PixelAniMaker.Core.Imaging;
using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.Core.Project;

/// <summary>A character with its animation clips — everything a .dotchar file holds.</summary>
public sealed record ProjectData(Character Character, IReadOnlyList<AnimationClip> Clips);

/// <summary>
/// The .dotchar format: a zip with project.json (version, palette, outline), skeleton.json,
/// parts/&lt;direction&gt;/&lt;part&gt;.png and animations.json — readable by people and other tools.
/// </summary>
public static class ProjectFile
{
    public const string Extension = ".dotchar";
    public const int FormatVersion = 1;

    private const string ProjectEntry = "project.json";
    private const string SkeletonEntry = "skeleton.json";
    private const string AnimationsEntry = "animations.json";
    private const string PartsFolder = "parts/";

    private sealed record OutlineSpec(bool Enabled, int OutlineIndex, int InnerIndex);

    private sealed record ProjectSpec(int FormatVersion, List<string> Palette, OutlineSpec Outline);

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static void Save(ProjectData project, Stream output, IImageCodec codec)
    {
        var c = project.Character;
        using var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);

        var palette = c.Palette.Colors.Skip(1).Select(color => color.ToString()).ToList(); // index 0 is always transparent
        Write(zip, ProjectEntry, JsonSerializer.Serialize(new ProjectSpec(FormatVersion, palette,
            new OutlineSpec(c.Outline.Enabled, c.Outline.OutlineIndex, c.Outline.InnerIndex)), Json));
        Write(zip, SkeletonEntry, CharacterSpec.From(c).ToJson());
        Write(zip, AnimationsEntry, AnimationJson.Serialize(project.Clips));

        foreach (var part in c.Parts)
            foreach (var d in DirectionExtensions.Stored)
            {
                using var s = zip.CreateEntry(PartsFolder + CharacterSpec.ImagePath(d, part.Name)).Open();
                s.Write(codec.EncodePng(part.View(d).Image.ToRgba(c.Palette)));
            }
    }

    public static ProjectData Load(Stream input, IImageCodec codec)
    {
        using var zip = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
        var project = JsonSerializer.Deserialize<ProjectSpec>(Read(zip, ProjectEntry), Json)
            ?? throw new FormatException("Missing project settings.");
        if (project.FormatVersion > FormatVersion)
            throw new FormatException($"This file was made by a newer version (format {project.FormatVersion}).");

        var palette = new Palette();
        foreach (var hex in project.Palette)
            palette.GetOrAdd(Rgba.TryParseHex(hex, out var color) ? color : throw new FormatException($"Bad colour '{hex}'."));

        var character = CharacterSpec.Parse(Read(zip, SkeletonEntry)).Build(path =>
        {
            var entry = zip.GetEntry(PartsFolder + path) ?? throw new FormatException($"Missing image '{path}'.");
            using var s = entry.Open();
            return codec.DecodePng(s);
        }, palette);
        character.Outline.OutlineIndex = project.Outline.OutlineIndex;
        character.Outline.InnerIndex = project.Outline.InnerIndex;
        character.Outline.Enabled = project.Outline.Enabled;

        var clips = zip.GetEntry(AnimationsEntry) is null ? [] : AnimationJson.Parse(Read(zip, AnimationsEntry));
        return new ProjectData(character, clips);
    }

    private static void Write(ZipArchive zip, string name, string text)
    {
        using var s = zip.CreateEntry(name).Open();
        s.Write(Encoding.UTF8.GetBytes(text));
    }

    private static string Read(ZipArchive zip, string name)
    {
        var entry = zip.GetEntry(name) ?? throw new FormatException($"Missing '{name}'.");
        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
