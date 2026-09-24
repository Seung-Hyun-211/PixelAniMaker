using System.Text.Json;
using Avalonia.Threading;

namespace PixelAniMaker.App.Services;

/// <summary>An autosave copy left behind by a session that did not close normally.</summary>
public sealed record Recovery(string File, string? OriginalPath, DateTime SavedAt);

/// <summary>
/// Periodically writes a copy of unsaved work to %AppData%/PixelAniMaker/autosave (never touching the
/// user's file). The copy is removed when the work is saved, replaced or the app closes normally, so a
/// copy found at startup means the previous session crashed.
/// </summary>
public sealed class AutosaveService
{
    private sealed record Meta(string? OriginalPath, DateTime SavedAt);

    private static readonly string Folder = AppSettings.DataFolder("autosave");

    private readonly ProjectService _project;
    private readonly string _file = Path.Combine(Folder, $"{Guid.NewGuid():N}.dotchar");
    private readonly DispatcherTimer? _timer;

    public AutosaveService(ProjectService project)
    {
        _project = project;
        project.DocumentReset += (_, _) => DiscardCurrent();
        if (project.Settings.AutosaveSeconds <= 0)
            return;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(project.Settings.AutosaveSeconds) };
        _timer.Tick += (_, _) => SaveNow();
        _timer.Start();
    }

    /// <summary>Writes the copy if there is unsaved work. Failures are ignored: autosave must never interrupt.</summary>
    public void SaveNow()
    {
        if (!_project.IsDirty)
            return;
        try
        {
            Directory.CreateDirectory(Folder);
            _project.WriteCopy(_file);
            File.WriteAllText(MetaPath(_file), JsonSerializer.Serialize(new Meta(_project.CurrentPath, DateTime.Now)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>Removes this session's copy (saved, replaced or closed normally).</summary>
    public void DiscardCurrent() => Delete(_file);

    public static void Discard(Recovery recovery) => Delete(recovery.File);

    /// <summary>Copies left by earlier sessions, newest first.</summary>
    public static IReadOnlyList<Recovery> FindRecoveries()
    {
        if (!Directory.Exists(Folder))
            return [];
        var found = new List<Recovery>();
        foreach (var file in Directory.GetFiles(Folder, "*.dotchar"))
        {
            try
            {
                var meta = JsonSerializer.Deserialize<Meta>(File.ReadAllText(MetaPath(file)));
                found.Add(new Recovery(file, meta?.OriginalPath, meta?.SavedAt ?? File.GetLastWriteTime(file)));
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                found.Add(new Recovery(file, null, File.GetLastWriteTime(file)));
            }
        }
        return found.OrderByDescending(r => r.SavedAt).ToList();
    }

    private static string MetaPath(string file) => file + ".json";

    private static void Delete(string file)
    {
        try
        {
            File.Delete(file);
            File.Delete(MetaPath(file));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
