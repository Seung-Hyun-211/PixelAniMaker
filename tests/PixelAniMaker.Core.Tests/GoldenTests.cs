using System.IO.Compression;
using System.Numerics;
using System.Runtime.CompilerServices;
using PixelAniMaker.Core.Animation;
using PixelAniMaker.Core.Editing;
using PixelAniMaker.Core.Export;
using PixelAniMaker.Core.History;
using PixelAniMaker.Core.Imaging;
using PixelAniMaker.Core.Project;
using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.Core.Tests;

/// <summary>
/// Regression guard: a 4-direction project using every feature (layers, angle variant, attachment,
/// own right view, eye parts, keys in every stored direction, easings, touch-ups) must save and export
/// exactly as recorded in Golden/. Set GOLDEN_UPDATE=1 to re-record after an intended change.
/// </summary>
public class GoldenTests
{
    private static string GoldenDir([CallerFilePath] string here = "") => Path.Combine(Path.GetDirectoryName(here)!, "Golden", "four-directions");

    private static bool Updating => Environment.GetEnvironmentVariable("GOLDEN_UPDATE") == "1";

    /// <summary>Deterministic colours that differ per part, direction and pixel, so rotations sample varied pixels.</summary>
    private static RgbaImage Picture(string file, int w, int h)
    {
        int seed = file.Sum(c => c);
        var pixels = new Rgba[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int k = (x * 3 + y * 5 + seed) % 6;   // six shades per image keep the palette small
                pixels[y * w + x] = (x + y) % 5 == 0 ? Rgba.Transparent
                    : new Rgba((byte)(seed * 7 % 200 + 40), (byte)(40 * k), (byte)(seed * 3 % 256));
            }
        return new RgbaImage(w, h, pixels);
    }

    internal static ProjectData Build()
    {
        (string Name, string? Parent, int X, int Y, int W, int H, float Jx, float Jy, int Order)[] parts =
        [
            ("body", null, 10, 16, 12, 14, 16, 24, 0),
            ("head", "body", 9, 3, 14, 13, 16, 16, 1),
            ("arm_r", "body", 6, 17, 4, 10, 8, 18, 2),
            ("arm_l", "body", 22, 17, 4, 10, 24, 18, 3),
        ];
        var sizes = parts.ToDictionary(p => p.Name, p => (p.W, p.H));
        var spec = new CharacterSpec(32, 40, parts.Select(p => new PartSpec(p.Name, p.Name, p.Parent,
            new[] { "front", "left", "back" }.ToDictionary(d => d,
                d => new PartViewSpec(p.X, p.Y, p.Jx, p.Jy, d == "back" ? 3 - p.Order : p.Order, $"{d}/{p.Name}")))).ToList());
        var c = spec.Build(file => Picture(file, sizes[file.Split('/')[1]].W, sizes[file.Split('/')[1]].H));
        c.Outline.OutlineIndex = c.Palette.GetOrAdd(new Rgba(10, 10, 10));
        c.Outline.InnerIndex = c.Palette.GetOrAdd(new Rgba(90, 60, 60));
        c.Outline.Enabled = true;
        var history = new UndoHistory();

        var body = c.Find("body")!.View(Direction.Front);
        var clothes = body.Layers.CreateLayer("옷");
        clothes.Image.Set(2, 3, c.Palette.GetOrAdd(new Rgba(200, 30, 30)));
        LayerChange.Apply(body.Layers, history, "옷", list => list.Add(clothes));

        var arm = c.Find("arm_r")!.View(Direction.Front);
        arm.Variants.Set(90, VariantChange.RenderFromBase(arm, 90));
        c.Find("arm_l")!.View(Direction.Front).Attachments.Set("grip", new Vector2(2, 9));

        var head = c.Find("head")!;
        RightViewChange.Apply(head, history, separate: true);
        head.View(Direction.Right).Image.Set(3, 4, c.Palette.GetOrAdd(new Rgba(0, 0, 255)));
        EyeParts.Add(c);

        var wave = new AnimationClip("wave", 4, 8, loop: true);
        wave.SetKey(Direction.Front, new Keyframe(0, Pose(("arm_r", 30))));
        wave.SetKey(Direction.Front, new Keyframe(2, Pose(("arm_r", 120)) with { Offset = new Vector2(0, -1) }, Easing.Linear));
        wave.SetKey(Direction.Left, new Keyframe(0, Pose(("arm_l", -20))));
        wave.SetKey(Direction.Left, new Keyframe(2, Pose(("body", 5), ("head", -10)), Easing.Step));
        wave.SetKey(Direction.Back, new Keyframe(1, Pose(("arm_l", 45))));
        wave.Touchups.Set(Direction.Right, 1, new PixelOverrides { [(5, 5)] = 1, [(6, 5)] = 0 });
        wave.Touchups.Set(Direction.Back, 2, new PixelOverrides { [(10, 20)] = 2 });
        var idle = new AnimationClip("idle", 2, 6, loop: false);
        return new ProjectData(c, [wave, idle]);
    }

    private static PoseData Pose(params (string Part, double Degrees)[] rotations) =>
        new(rotations.ToDictionary(r => r.Part, r => r.Degrees), Vector2.Zero);

    private static Dictionary<string, byte[]> Entries(byte[] zip)
    {
        using var archive = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
        return archive.Entries.ToDictionary(e => e.FullName, e =>
        {
            using var s = e.Open();
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            return ms.ToArray();
        });
    }

    private static byte[] Save(ProjectData project)
    {
        using var ms = new MemoryStream();
        ProjectFile.Save(project, ms, new RawCodec());
        return ms.ToArray();
    }

    /// <summary>Every output of the fixture, by golden file name.</summary>
    private static Dictionary<string, byte[]> Outputs(ProjectData project)
    {
        var files = Entries(Save(project)).ToDictionary(e => "project/" + e.Key, e => e.Value);
        var sheet = SpriteSheet.Build(project.Character, project.Clips, new Compositor());
        files["sheet.rgba"] = new RawCodec().EncodePng(sheet.Image);
        files["sheet.json"] = System.Text.Encoding.UTF8.GetBytes(sheet.MetadataJson());
        using var gif = new MemoryStream();
        AnimationGif.Write(gif, project.Character, project.Clips[0], new Compositor(), scale: 2);
        files["wave.gif"] = gif.ToArray();
        return files;
    }

    private static void AssertMatchesGolden(Dictionary<string, byte[]> files)
    {
        string dir = GoldenDir();
        if (Updating)
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
            foreach (var (name, bytes) in files)
            {
                string path = Path.Combine(dir, name);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, bytes);
            }
            return;
        }
        var expected = Directory.GetFiles(dir, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(dir, f).Replace('\\', '/')).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(expected, files.Keys.Order(StringComparer.Ordinal).ToList());
        foreach (var name in expected)
            Assert.True(File.ReadAllBytes(Path.Combine(dir, name)).SequenceEqual(files[name]), $"{name} differs from the golden file");
    }

    [Fact]
    public void Four_direction_project_saves_and_exports_as_recorded() => AssertMatchesGolden(Outputs(Build()));

    [Fact]
    public void Recorded_project_loads_and_saves_back_unchanged()
    {
        if (Updating)
            return;
        string dir = Path.Combine(GoldenDir(), "project");
        using var zip = new MemoryStream();
        using (var archive = new ZipArchive(zip, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
            {
                using var s = archive.CreateEntry(Path.GetRelativePath(dir, file).Replace('\\', '/')).Open();
                s.Write(File.ReadAllBytes(file));
            }
        zip.Position = 0;
        var loaded = ProjectFile.Load(zip, new RawCodec());

        var saved = Entries(Save(loaded));
        foreach (var (name, bytes) in saved)
            Assert.True(File.ReadAllBytes(Path.Combine(dir, name)).SequenceEqual(bytes), $"{name} changed after load and save");
        Assert.Equal(Directory.GetFiles(dir, "*", SearchOption.AllDirectories).Length, saved.Count);
    }
}
