using System.Text.Json;
using PixelAniMaker.Core.Animation;
using PixelAniMaker.Core.Imaging;
using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.Core.Export;

/// <summary>An attachment point in one frame: position inside the cell and the part's rotation (degrees, clockwise).</summary>
public sealed record SheetAttachment(string Part, string Name, float X, float Y, float Angle);

/// <summary>Where one frame sits on the sheet, with its attachment points (omitted when there are none).</summary>
public sealed record SheetFrame(int X, int Y, int DurationMs, IReadOnlyList<SheetAttachment>? Attachments = null);

public sealed record SheetClip(string Name, int Fps, bool Loop, IReadOnlyDictionary<string, IReadOnlyList<SheetFrame>> Directions);

/// <summary>
/// A sprite sheet: every clip gets one row per exported direction — the classic four (front, left,
/// right, back), then with 3/4 views front-left, front-right, back-left, back-right — one column per
/// frame, all cells the canvas size. <see cref="OriginX"/>/<see cref="OriginY"/> is the feet position inside a cell.
/// </summary>
public sealed record SpriteSheet(RgbaImage Image, int CellWidth, int CellHeight, int OriginX, int OriginY, IReadOnlyList<SheetClip> Clips)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    /// <param name="directions">Rows per clip, in order (default: the classic four).</param>
    public static SpriteSheet Build(Character character, IReadOnlyList<AnimationClip> clips, Compositor compositor,
        IReadOnlyList<Direction>? directions = null)
    {
        directions ??= DirectionExtensions.All;
        int w = character.Width, h = character.Height;
        int columns = Math.Max(1, clips.Max(c => c.FrameCount));
        int rows = clips.Count * directions.Count;
        var image = RgbaImage.Blank(columns * w, rows * h);
        var sheetClips = new List<SheetClip>();

        int row = 0;
        foreach (var clip in clips)
        {
            var baked = SpriteBaker.Bake(character, clip, compositor, directions: directions);
            var byDirection = new Dictionary<string, IReadOnlyList<SheetFrame>>();
            foreach (var d in directions)
            {
                var poses = SpriteBaker.FramePoses(character, clip, d);
                var frames = new List<SheetFrame>();
                for (int f = 0; f < clip.FrameCount; f++)
                {
                    Blit(baked[d][f], character.Palette, image, f * w, row * h);
                    frames.Add(new SheetFrame(f * w, row * h, clip.DurationMs(f), FrameAttachments(character, d, poses[f])));
                }
                byDirection[d.ToString().ToLowerInvariant()] = frames;
                row++;
            }
            sheetClips.Add(new SheetClip(clip.Name, clip.Fps, clip.Loop, byDirection));
        }

        var (ox, oy) = FeetOrigin(character, compositor);
        return new SpriteSheet(image, w, h, ox, oy, sheetClips);
    }

    /// <summary>Engine-neutral description of the sheet: cell size, origin and every frame rectangle.</summary>
    public string MetadataJson() => JsonSerializer.Serialize(new
    {
        cellWidth = CellWidth,
        cellHeight = CellHeight,
        origin = new { x = OriginX, y = OriginY },
        animations = Clips,
    }, Json);

    private static IReadOnlyList<SheetAttachment>? FrameAttachments(Character character, Direction direction, PoseData pose)
    {
        var list = Attachments.Place(character, direction, pose)
            .Select(a => new SheetAttachment(a.Part.Name, a.Name, Round(a.Position.X), Round(a.Position.Y), Round(a.Degrees)))
            .ToList();
        return list.Count > 0 ? list : null;

        static float Round(float v) => MathF.Round(v, 1);
    }

    /// <summary>Horizontal centre and the row just below the lowest pixel of the rest pose.</summary>
    private static (int X, int Y) FeetOrigin(Character character, Compositor compositor)
    {
        var rest = compositor.Compose(character, Direction.Front, PoseData.Rest);
        int bottom = rest.Height;
        for (int y = rest.Height - 1; y >= 0; y--)
        {
            if (Enumerable.Range(0, rest.Width).Any(x => rest.OwnerAt(x, y) != CompositeResult.NoPart))
            {
                bottom = y + 1;
                break;
            }
        }
        return (character.Width / 2, bottom);
    }

    private static void Blit(CompositeResult src, Palette palette, RgbaImage dst, int ox, int oy)
    {
        for (int y = 0; y < src.Height; y++)
            for (int x = 0; x < src.Width; x++)
                dst.Pixels[(oy + y) * dst.Width + ox + x] = palette[src.Indices[y * src.Width + x]];
    }
}
