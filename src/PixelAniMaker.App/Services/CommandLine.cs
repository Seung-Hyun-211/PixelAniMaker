namespace PixelAniMaker.App.Services;

/// <summary>
/// Startup arguments.
/// <c>PixelAniMaker file.dotchar</c> opens the file;
/// <c>PixelAniMaker --export file.dotchar [--out folder] [--directions 4|8] [--frames]</c> writes sheets and GIFs without
/// showing a window; <c>--directions 8</c> adds the 3/4 rows when the character has them (default 4);
/// <c>--frames</c> also writes one PNG per frame.
/// </summary>
public sealed record CommandLine(string? OpenPath, string? ExportPath, string? OutputFolder, int Directions = 4, bool Frames = false)
{
    public bool IsExport => ExportPath is not null;

    public static CommandLine Parse(IReadOnlyList<string> args)
    {
        string? open = null, export = null, output = null;
        int directions = 4;
        bool frames = false;
        for (int i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "--export" when i + 1 < args.Count:
                    export = args[++i];
                    break;
                case "--out" when i + 1 < args.Count:
                    output = args[++i];
                    break;
                case "--directions" when i + 1 < args.Count:
                    directions = args[++i] == "8" ? 8 : 4;
                    break;
                case "--frames":
                    frames = true;
                    break;
                default:
                    if (!args[i].StartsWith("--", StringComparison.Ordinal))
                        open ??= args[i];
                    break;
            }
        }
        return new CommandLine(open, export, output, directions, frames);
    }
}
