namespace PixelAniMaker.App.Services;

/// <summary>
/// Headless export for scripts and pipelines: one sheet with every clip (+ JSON) and one GIF per clip.
/// </summary>
public static class BatchExporter
{
    /// <returns>Process exit code: 0 on success, 1 on failure.</returns>
    /// <param name="directions">4, or 8 to add the 3/4 rows when the character has 3/4 views.</param>
    public static int Run(string projectPath, string? outputFolder, TextWriter log, int directions = 4)
    {
        try
        {
            var editor = new EditorSession();
            var animation = new AnimationSession(editor);
            var project = new ProjectService(editor, animation, new AppSettings { WriteSheetMetadata = true, ExportThreeQuarter = directions == 8 });
            project.Open(projectPath, rememberRecent: false);

            string folder = outputFolder ?? Path.GetDirectoryName(Path.GetFullPath(projectPath))!;
            Directory.CreateDirectory(folder);
            string name = Path.GetFileNameWithoutExtension(projectPath);

            string sheet = Path.Combine(folder, $"{name}_all.png");
            project.ExportSheet(sheet, allClips: true);
            log.WriteLine($"sheet: {sheet}");

            foreach (var clip in animation.Clips.ToList())
            {
                animation.CurrentClip = clip;
                string gif = Path.Combine(folder, $"{name}_{SafeFileName(clip.Name)}.gif");
                project.ExportGif(gif);
                log.WriteLine($"gif:   {gif}");
            }
            return 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException
                                       or InvalidOperationException or NotSupportedException or InvalidDataException)
        {
            log.WriteLine($"export failed: {ex.Message}");
            return 1;
        }
    }

    private static string SafeFileName(string name) =>
        string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
}
