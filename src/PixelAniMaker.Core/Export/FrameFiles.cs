using PixelAniMaker.Core.Animation;
using PixelAniMaker.Core.Imaging;
using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.Core.Export;

/// <summary>One exported frame image and its file name (no folder).</summary>
public sealed record FrameFile(string FileName, RgbaImage Image);

/// <summary>
/// Every frame as its own canvas-size PNG, named "prefix_clip_direction_frame.png" — frames count from 1
/// as on the timeline, zero-padded so the files sort in playing order. Clips with the same name get "_2", "_3"….
/// </summary>
public static class FrameFiles
{
    /// <param name="directions">Directions to write, in order (default: the classic four).</param>
    public static IReadOnlyList<FrameFile> Build(Character character, IReadOnlyList<AnimationClip> clips, Compositor compositor,
        string prefix, IReadOnlyList<Direction>? directions = null)
    {
        directions ??= DirectionExtensions.All;
        var files = new List<FrameFile>();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var clip in clips)
        {
            string clipName = SafeName(clip.Name);
            for (int n = 2; !used.Add(clipName); n++)
                clipName = $"{SafeName(clip.Name)}_{n}";
            int digits = Math.Max(2, clip.FrameCount.ToString().Length);
            var baked = SpriteBaker.Bake(character, clip, compositor, directions: directions);
            foreach (var d in directions)
                for (int f = 0; f < clip.FrameCount; f++)
                    files.Add(new FrameFile($"{SafeName(prefix)}_{clipName}_{d.Key()}_{(f + 1).ToString().PadLeft(digits, '0')}.png",
                        ToRgba(baked[d][f], character.Palette)));
        }
        return files;
    }

    /// <summary>The name with characters that are not allowed in file names replaced by '_'.</summary>
    public static string SafeName(string name) =>
        string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

    private static RgbaImage ToRgba(CompositeResult frame, Palette palette) =>
        new(frame.Width, frame.Height, frame.Indices.Select(i => palette[i]).ToArray());
}
