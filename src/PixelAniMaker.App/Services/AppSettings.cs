using System.Text.Json;

namespace PixelAniMaker.App.Services;

/// <summary>Per-user settings stored in %AppData%/PixelAniMaker/settings.json.</summary>
public sealed class AppSettings
{
    private const int MaxRecentFiles = 8;

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PixelAniMaker", "settings.json");

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public List<string> RecentFiles { get; set; } = [];

    /// <summary>Write a .json frame description next to exported sprite sheets.</summary>
    public bool WriteSheetMetadata { get; set; } = true;

    /// <summary>Scale of exported GIFs.</summary>
    public int GifScale { get; set; } = 2;

    /// <summary>Shortcuts changed by the user: action id → key ("" = none). Others use the defaults.</summary>
    public Dictionary<string, string> Shortcuts { get; set; } = [];

    /// <summary>Docking layout saved at exit (null = default layout).</summary>
    public LayoutState? Layout { get; set; }

    /// <summary>Main window placement saved at exit (null = maximized).</summary>
    public WindowPlacement? Window { get; set; }

    /// <summary>UI language: "ko" (default) or "en". Applied at start-up.</summary>
    public string Language { get; set; } = Localizer.Korean;

    /// <summary>Seconds between autosave copies of unsaved work; 0 turns autosave off.</summary>
    public int AutosaveSeconds { get; set; } = 120;

    /// <summary>Folder next to settings.json, e.g. for autosave copies.</summary>
    public static string DataFolder(string name) => Path.Combine(Path.GetDirectoryName(FilePath)!, name);

    public static AppSettings Load()
    {
        try
        {
            return File.Exists(FilePath)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings()
                : new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new AppSettings(); // a broken settings file must not stop the app
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Json));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // settings are a convenience; ignore write failures
        }
    }

    public void AddRecent(string path)
    {
        RecentFiles.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        RecentFiles.Insert(0, path);
        if (RecentFiles.Count > MaxRecentFiles)
            RecentFiles.RemoveRange(MaxRecentFiles, RecentFiles.Count - MaxRecentFiles);
        Save();
    }
}
