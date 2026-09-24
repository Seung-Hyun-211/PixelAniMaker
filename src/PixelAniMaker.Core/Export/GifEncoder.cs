using PixelAniMaker.Core.Imaging;

namespace PixelAniMaker.Core.Export;

/// <summary>
/// Minimal animated GIF89a writer for palette images: one global colour table, palette index 0 as the
/// transparent colour, frames cleared between each other, optional infinite loop.
/// </summary>
public static class GifEncoder
{
    public const int MaxColors = 256;

    /// <param name="frames">Palette indices, row-major, <paramref name="width"/>×<paramref name="height"/> each.</param>
    /// <param name="delayMs">Time per frame (GIF stores hundredths of a second).</param>
    public static void Encode(Stream output, int width, int height, IReadOnlyList<Rgba> palette,
        IEnumerable<byte[]> frames, int delayMs, bool loop)
    {
        if (palette.Count > MaxColors)
            throw new NotSupportedException($"GIF supports at most {MaxColors} colours; the palette has {palette.Count}.");

        int tableBits = Math.Max(1, (int)Math.Ceiling(Math.Log2(Math.Max(2, palette.Count))));
        using var w = new BinaryWriter(output, System.Text.Encoding.ASCII, leaveOpen: true);

        w.Write("GIF89a"u8);
        w.Write((ushort)width);
        w.Write((ushort)height);
        w.Write((byte)(0x80 | ((tableBits - 1) << 4) | (tableBits - 1))); // global table, colour resolution, size
        w.Write((byte)0);   // background colour index
        w.Write((byte)0);   // aspect ratio
        for (int i = 0; i < 1 << tableBits; i++)
        {
            var c = i < palette.Count ? palette[i] : default;
            w.Write(c.R);
            w.Write(c.G);
            w.Write(c.B);
        }

        if (loop)
        {
            w.Write([0x21, 0xFF, 0x0B]);
            w.Write("NETSCAPE2.0"u8);
            w.Write([0x03, 0x01, 0x00, 0x00, 0x00]); // loop count 0 = forever
        }

        ushort delay = (ushort)Math.Max(1, (delayMs + 5) / 10);
        foreach (var frame in frames)
        {
            if (frame.Length != width * height)
                throw new ArgumentException("Frame size does not match the image size.", nameof(frames));
            // graphic control: disposal 2 (restore background), transparent index 0
            w.Write([0x21, 0xF9, 0x04, (byte)((2 << 2) | 1)]);
            w.Write(delay);
            w.Write([(byte)0, (byte)0]);
            // image descriptor
            w.Write((byte)0x2C);
            w.Write((ushort)0);
            w.Write((ushort)0);
            w.Write((ushort)width);
            w.Write((ushort)height);
            w.Write((byte)0);

            int minCodeSize = Math.Max(2, tableBits);
            w.Write((byte)minCodeSize);
            WriteSubBlocks(w, Lzw(frame, minCodeSize));
        }
        w.Write((byte)0x3B);
    }

    /// <summary>GIF-flavoured LZW with variable code size (up to 12 bits) and clear codes when the table fills.</summary>
    internal static byte[] Lzw(byte[] pixels, int minCodeSize)
    {
        int clear = 1 << minCodeSize, end = clear + 1;
        var output = new List<byte>(pixels.Length);
        var table = new Dictionary<int, int>();
        int codeSize = minCodeSize + 1, next = end + 1;
        int bitBuffer = 0, bitCount = 0;
        bool resetAfterEmit = false;

        void Emit(int code)
        {
            bitBuffer |= code << bitCount;
            bitCount += codeSize;
            while (bitCount >= 8)
            {
                output.Add((byte)bitBuffer);
                bitBuffer >>= 8;
                bitCount -= 8;
            }
            if (resetAfterEmit)
            {
                codeSize = minCodeSize + 1;
                resetAfterEmit = false;
            }
            else if (next > (1 << codeSize) - 1 && codeSize < 12)
            {
                codeSize++;
            }
        }

        Emit(clear);
        if (pixels.Length == 0)
        {
            Emit(end);
            return Flush();
        }

        int prefix = pixels[0];
        for (int i = 1; i < pixels.Length; i++)
        {
            int key = (prefix << 8) | pixels[i];
            if (table.TryGetValue(key, out int code))
            {
                prefix = code;
                continue;
            }
            Emit(prefix);
            if (next < 4096)
            {
                table[key] = next++;
            }
            else
            {
                resetAfterEmit = true;
                Emit(clear);
                table.Clear();
                next = end + 1;
            }
            prefix = pixels[i];
        }
        Emit(prefix);
        Emit(end);
        return Flush();

        byte[] Flush()
        {
            if (bitCount > 0)
                output.Add((byte)bitBuffer);
            return output.ToArray();
        }
    }

    private static void WriteSubBlocks(BinaryWriter w, byte[] data)
    {
        for (int i = 0; i < data.Length; i += 255)
        {
            int n = Math.Min(255, data.Length - i);
            w.Write((byte)n);
            w.Write(data, i, n);
        }
        w.Write((byte)0);
    }
}
