using PixelAniMaker.Core.Animation;
using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.Core.Export;

/// <summary>Writes one clip as an animated GIF with the directions side by side (default: the classic four); held frames stay longer.</summary>
public static class AnimationGif
{
    public static void Write(Stream output, Character character, AnimationClip clip, Compositor compositor, int scale = 2,
        IReadOnlyList<Direction>? directions = null)
    {
        scale = Math.Max(1, scale);
        int cw = character.Width, ch = character.Height;
        directions ??= DirectionExtensions.All;
        int width = cw * directions.Count * scale, height = ch * scale;

        var baked = SpriteBaker.Bake(character, clip, compositor, directions: directions);
        var frames = Enumerable.Range(0, clip.FrameCount).Select(f =>
        {
            var pixels = new byte[width * height];
            for (int d = 0; d < directions.Count; d++)
            {
                var src = baked[directions[d]][f];
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < cw * scale; x++)
                        pixels[y * width + d * cw * scale + x] = (byte)src.Indices[(y / scale) * cw + x / scale];
            }
            return pixels;
        });

        GifEncoder.Encode(output, width, height, character.Palette.Colors, frames, clip.DurationMs, clip.Loop);
    }
}
