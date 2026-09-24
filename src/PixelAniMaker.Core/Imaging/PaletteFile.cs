using System.Globalization;
using System.Text;

namespace PixelAniMaker.Core.Imaging;

/// <summary>
/// Palette files shared with other pixel-art tools: GIMP/Aseprite <c>.gpl</c>, Lospec <c>.hex</c>
/// (one RRGGBB per line) and <c>.png</c> (one pixel per colour; on import every distinct opaque colour
/// in reading order). Colours are listed without the transparent entry.
/// </summary>
public static class PaletteFile
{
    public static IReadOnlyList<string> Extensions { get; } = [".gpl", ".hex", ".png"];

    public static IReadOnlyList<Rgba> Read(string fileName, Stream stream, IImageCodec codec) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".gpl" => ParseGpl(ReadText(stream)),
            ".hex" => ParseHex(ReadText(stream)),
            ".png" => FromImage(codec.DecodePng(stream)),
            var ext => throw new FormatException($"지원하지 않는 팔레트 형식입니다: {ext}"),
        };

    public static void Write(string fileName, Stream stream, IReadOnlyList<Rgba> colors, IImageCodec codec, string name = "PixelAniMaker")
    {
        byte[] bytes = Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".gpl" => Encoding.UTF8.GetBytes(ToGpl(colors, name)),
            ".hex" => Encoding.UTF8.GetBytes(string.Concat(colors.Select(c => c.ToString()[1..7].ToLowerInvariant() + "\n"))),
            ".png" => codec.EncodePng(new RgbaImage(Math.Max(1, colors.Count), 1, colors.Count > 0 ? [.. colors] : [Rgba.Transparent])),
            var ext => throw new FormatException($"지원하지 않는 팔레트 형식입니다: {ext}"),
        };
        stream.Write(bytes);
    }

    public static IReadOnlyList<Rgba> ParseGpl(string text)
    {
        var lines = text.Split('\n').Select(l => l.Trim()).ToList();
        if (lines.Count == 0 || !lines[0].StartsWith("GIMP Palette", StringComparison.Ordinal))
            throw new FormatException("GIMP 팔레트(.gpl) 파일이 아닙니다.");
        var colors = new List<Rgba>();
        foreach (var line in lines.Skip(1))
        {
            if (line.Length == 0 || line[0] == '#' || line.Contains(':'))   // comments, "Name:", "Columns:"
                continue;
            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3 && byte.TryParse(parts[0], out var r) && byte.TryParse(parts[1], out var g) && byte.TryParse(parts[2], out var b))
                colors.Add(new Rgba(r, g, b));
        }
        return colors;
    }

    public static IReadOnlyList<Rgba> ParseHex(string text)
    {
        var colors = new List<Rgba>();
        foreach (var line in text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0))
            colors.Add(Rgba.TryParseHex(line, out var c) ? c : throw new FormatException($"잘못된 색: {line}"));
        return colors;
    }

    private static IReadOnlyList<Rgba> FromImage(RgbaImage image) =>
        image.Pixels.Where(p => !p.IsTransparent).Distinct().ToList();

    private static string ToGpl(IReadOnlyList<Rgba> colors, string name)
    {
        var sb = new StringBuilder();
        sb.Append("GIMP Palette\nName: ").Append(name).Append("\nColumns: 8\n#\n");
        foreach (var c in colors)
            sb.Append(CultureInfo.InvariantCulture, $"{c.R,3} {c.G,3} {c.B,3}\t{c.ToString()[1..]}\n");
        return sb.ToString();
    }

    private static string ReadText(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        return reader.ReadToEnd();
    }
}
