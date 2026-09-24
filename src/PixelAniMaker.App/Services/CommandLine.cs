namespace PixelAniMaker.App.Services;

/// <summary>
/// Startup arguments.
/// <c>PixelAniMaker file.dotchar</c> opens the file;
/// <c>PixelAniMaker --export file.dotchar [--out folder]</c> writes sheets and GIFs without showing a window.
/// </summary>
public sealed record CommandLine(string? OpenPath, string? ExportPath, string? OutputFolder)
{
    public bool IsExport => ExportPath is not null;

    public static CommandLine Parse(IReadOnlyList<string> args)
    {
        string? open = null, export = null, output = null;
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
                default:
                    if (!args[i].StartsWith("--", StringComparison.Ordinal))
                        open ??= args[i];
                    break;
            }
        }
        return new CommandLine(open, export, output);
    }
}
