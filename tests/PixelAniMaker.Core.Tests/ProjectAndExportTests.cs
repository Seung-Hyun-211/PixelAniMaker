using System.Numerics;
using PixelAniMaker.Core.Animation;
using PixelAniMaker.Core.Editing;
using PixelAniMaker.Core.Export;
using PixelAniMaker.Core.Imaging;
using PixelAniMaker.Core.Project;
using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.Core.Tests;

/// <summary>Stores images as raw width, height, RGBA bytes — enough to test the container format.</summary>
internal sealed class RawCodec : IImageCodec
{
    public byte[] EncodePng(RgbaImage image)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write(image.Width);
        w.Write(image.Height);
        foreach (var p in image.Pixels)
            w.Write([p.R, p.G, p.B, p.A]);
        w.Flush();
        return ms.ToArray();
    }

    public RgbaImage DecodePng(Stream stream)
    {
        using var r = new BinaryReader(stream);
        int w = r.ReadInt32(), h = r.ReadInt32();
        var pixels = new Rgba[w * h];
        for (int i = 0; i < pixels.Length; i++)
        {
            var b = r.ReadBytes(4);
            pixels[i] = new Rgba(b[0], b[1], b[2], b[3]);
        }
        return new RgbaImage(w, h, pixels);
    }
}

public class ProjectAndExportTests
{
    private static readonly Rgba Red = new(255, 0, 0);
    private static readonly Rgba Blue = new(0, 0, 255);

    private static PartSpec Spec(string name, string? parent, int x, int y, float jx, float jy, int order)
    {
        var v = new PartViewSpec(x, y, jx, jy, order, name);
        return new(name, name + "!", parent, new Dictionary<string, PartViewSpec> { ["front"] = v, ["left"] = v, ["back"] = v });
    }

    private static Character TwoParts() =>
        new CharacterSpec(16, 16, [Spec("body", null, 6, 6, 8, 8, 0), Spec("arm", "body", 7, 10, 8, 10, 1)])
            .Build(n => new RgbaImage(n == "body" ? 4 : 2, 4, Enumerable.Repeat(n == "body" ? Red : Blue, n == "body" ? 16 : 8).ToArray()));

    private static AnimationClip Wave()
    {
        var clip = new AnimationClip("흔들기", 4, fps: 8, loop: true);
        clip.SetKey(Direction.Front, new Keyframe(0, new PoseData(new Dictionary<string, double> { ["arm"] = -30 }, new Vector2(0, 1))));
        clip.SetKey(Direction.Left, new Keyframe(2, new PoseData(new Dictionary<string, double> { ["arm"] = 45 }, Vector2.Zero), Easing.Step));
        return clip;
    }

    [Fact]
    public void Project_round_trips_pixels_palette_outline_and_clips()
    {
        var c = TwoParts();
        var arm = c.Find("arm")!;
        var doc = c.CreateDocument(arm, Direction.Left);
        doc.Colors.Primary = c.Palette.GetOrAdd(new Rgba(10, 200, 30));
        doc.BeginStroke(PencilTool.Pencil, 1, 3);
        doc.EndStroke();
        c.Outline.OutlineIndex = 1;
        c.Outline.InnerIndex = 2;
        c.Outline.Enabled = true;

        using var ms = new MemoryStream();
        ProjectFile.Save(new ProjectData(c, [Wave()]), ms, new RawCodec());
        ms.Position = 0;
        var loaded = ProjectFile.Load(ms, new RawCodec());

        var lc = loaded.Character;
        Assert.Equal(c.Palette.Colors, lc.Palette.Colors);
        Assert.Equal(arm.View(Direction.Left).Image.Pixels.ToArray(), lc.Find("arm")!.View(Direction.Left).Image.Pixels.ToArray());
        Assert.Equal(arm.View(Direction.Front).Image.Pixels.ToArray(), lc.Find("arm")!.View(Direction.Front).Image.Pixels.ToArray());
        Assert.Equal("arm!", lc.Find("arm")!.Label);
        Assert.Equal("body", lc.Find("arm")!.Parent!.Name);
        Assert.True(lc.Outline.Enabled);
        Assert.Equal((1, 2), (lc.Outline.OutlineIndex, lc.Outline.InnerIndex));

        var clip = Assert.Single(loaded.Clips);
        Assert.Equal(("흔들기", 4, 8, true), (clip.Name, clip.FrameCount, clip.Fps, clip.Loop));
        Assert.Equal(-30, clip.KeyAt(Direction.Front, 0)!.Pose.Get("arm"));
        Assert.Equal(1f, clip.KeyAt(Direction.Front, 0)!.Pose.Offset.Y);
        Assert.Equal(Easing.Step, clip.KeyAt(Direction.Left, 2)!.Easing);
    }

    [Fact]
    public void Newer_format_is_rejected()
    {
        using var ms = new MemoryStream();
        using (var zip = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Create, true))
        using (var w = new StreamWriter(zip.CreateEntry("project.json").Open()))
            w.Write("""{"formatVersion":99,"palette":[],"outline":{"enabled":false,"outlineIndex":0,"innerIndex":0}}""");
        ms.Position = 0;
        Assert.Throws<FormatException>(() => ProjectFile.Load(ms, new RawCodec()));
    }

    [Fact]
    public void Sheet_has_four_rows_per_clip_and_metadata()
    {
        var c = TwoParts();
        var sheet = SpriteSheet.Build(c, [Wave(), new AnimationClip("대기", 2)], new Compositor());

        Assert.Equal((4 * 16, 8 * 16), (sheet.Image.Width, sheet.Image.Height));
        Assert.Equal(14, sheet.OriginY);          // arm bottom at y 13
        var wave = sheet.Clips[0];
        Assert.Equal(4, wave.Directions["right"].Count);
        Assert.Equal(new SheetFrame(16, 16, 125), wave.Directions["left"][1]);
        Assert.Equal(4 * 16, sheet.Clips[1].Directions["front"][0].Y);
        Assert.Contains("\"cellWidth\": 16", sheet.MetadataJson());
    }

    [Fact]
    public void Sheet_right_row_mirrors_left_row()
    {
        var c = TwoParts();
        var sheet = SpriteSheet.Build(c, [Wave()], new Compositor());
        int W = sheet.Image.Width;
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
                Assert.Equal(sheet.Image.Pixels[(16 + y) * W + x], sheet.Image.Pixels[(32 + y) * W + 15 - x]);
    }

    [Theory]
    [InlineData(2, 50)]
    [InlineData(8, 5000)]   // long enough to fill the 4096-entry table and emit clear codes
    public void Lzw_round_trips(int bits, int length)
    {
        var rng = new Random(bits);
        var data = new byte[length];
        for (int i = 0; i < length; i++)
            data[i] = (byte)(i % 7 == 0 ? rng.Next(1 << bits) : data[Math.Max(0, i - 1)]);
        var encoded = GifEncoder.Lzw(data, Math.Max(2, bits));
        Assert.Equal(data, LzwDecode(encoded, Math.Max(2, bits)));
    }

    [Fact]
    public void Gif_has_header_loop_and_trailer()
    {
        using var ms = new MemoryStream();
        GifEncoder.Encode(ms, 2, 2, [Rgba.Transparent, Red, Blue], [new byte[] { 0, 1, 2, 1 }, new byte[] { 1, 1, 0, 2 }], 100, loop: true);
        var bytes = ms.ToArray();
        Assert.Equal("GIF89a"u8.ToArray(), bytes[..6]);
        Assert.Contains("NETSCAPE2.0", System.Text.Encoding.ASCII.GetString(bytes));
        Assert.Equal(0x3B, bytes[^1]);
    }

    [Fact]
    public void Gif_rejects_large_palette()
    {
        var palette = Enumerable.Range(0, 300).Select(i => new Rgba((byte)i, 0, 0)).ToList();
        Assert.Throws<NotSupportedException>(() =>
            GifEncoder.Encode(new MemoryStream(), 1, 1, palette, [new byte[1]], 100, false));
    }

    /// <summary>Reference GIF LZW decoder.</summary>
    private static byte[] LzwDecode(byte[] data, int minCodeSize)
    {
        int clear = 1 << minCodeSize, end = clear + 1;
        var dict = new List<byte[]>();
        void Reset()
        {
            dict.Clear();
            for (int i = 0; i < clear; i++) dict.Add([(byte)i]);
            dict.Add([]);
            dict.Add([]);
        }
        Reset();
        int codeSize = minCodeSize + 1, bitPos = 0, prev = -1;
        var output = new List<byte>();
        while (true)
        {
            int code = 0;
            for (int b = 0; b < codeSize; b++, bitPos++)
                code |= ((data[bitPos >> 3] >> (bitPos & 7)) & 1) << b;
            if (code == clear) { Reset(); codeSize = minCodeSize + 1; prev = -1; continue; }
            if (code == end) break;
            byte[] entry;
            if (prev == -1) entry = dict[code];
            else if (code < dict.Count) { entry = dict[code]; dict.Add([.. dict[prev], entry[0]]); }
            else { entry = [.. dict[prev], dict[prev][0]]; dict.Add(entry); }
            output.AddRange(entry);
            prev = code;
            if (dict.Count == 1 << codeSize && codeSize < 12) codeSize++;
        }
        return output.ToArray();
    }
}

public class AnimationGifTests
{
    [Fact]
    public void Gif_is_four_directions_wide_and_scaled()
    {
        var v = new PartViewSpec(2, 2, 3, 3, 0, "a");
        var c = new CharacterSpec(8, 10, [new PartSpec("a", "a", null,
                new Dictionary<string, PartViewSpec> { ["front"] = v, ["left"] = v, ["back"] = v })])
            .Build(_ => new RgbaImage(2, 2, Enumerable.Repeat(new Rgba(9, 9, 9), 4).ToArray()));
        using var ms = new MemoryStream();
        AnimationGif.Write(ms, c, new AnimationClip("a", 3), new Compositor(), scale: 3);
        var bytes = ms.ToArray();
        Assert.Equal(8 * 4 * 3, BitConverter.ToUInt16(bytes, 6));
        Assert.Equal(10 * 3, BitConverter.ToUInt16(bytes, 8));
    }
}
