using CommunityToolkit.Mvvm.ComponentModel;
using PixelAniMaker.Core.Animation;
using PixelAniMaker.Core.Export;
using PixelAniMaker.Core.Imaging;
using PixelAniMaker.Core.Project;

namespace PixelAniMaker.App.Services;

/// <summary>New / open / save of .dotchar projects and sprite sheet / GIF export.</summary>
public sealed partial class ProjectService : ObservableObject
{
    public static readonly FileType ProjectType = new("PixelAniMaker 프로젝트", ProjectFile.Extension);
    public static readonly FileType PngType = new("PNG 이미지", ".png");
    public static readonly FileType GifType = new("GIF 애니메이션", ".gif");
    public static readonly FileType ImageType = new("이미지", ".png", ".jpg", ".jpeg", ".bmp");
    public static readonly FileType PaletteType = new("팔레트", [.. PaletteFile.Extensions]);

    private readonly EditorSession _editor;
    private readonly AnimationSession _animation;

    [ObservableProperty] private string? _currentPath;

    /// <summary>The document came from an autosave after a crash; it stays unsaved until saved.</summary>
    [ObservableProperty] private bool _isRecovered;

    public ProjectService(EditorSession editor, AnimationSession animation, AppSettings settings)
    {
        _editor = editor;
        _animation = animation;
        Settings = settings;
    }

    public AppSettings Settings { get; }

    public string DocumentName => CurrentPath is null ? "제목 없음" : Path.GetFileNameWithoutExtension(CurrentPath);

    public bool IsDirty => IsRecovered || _editor.Character.History.IsDirty || _animation.HasUnsavedClipChanges;

    /// <summary>Raised when the document is replaced (new, open) or saved — autosave copies are then obsolete.</summary>
    public event EventHandler? DocumentReset;

    /// <param name="jointDiscs">False starts from the plain mannequin without ball-joint circles.</param>
    /// <param name="clips">
    /// The clips to start with (default: every default clip). An empty list gives one blank clip,
    /// since the timeline always keeps at least one.
    /// </param>
    /// <param name="threeQuarter">Start with the template's 3/4 views (saved as format 2).</param>
    public void New(bool jointDiscs = true, IReadOnlyList<AnimationClip>? clips = null, bool threeQuarter = false)
    {
        _editor.LoadCharacter(TemplateLoader.LoadChibi96(jointDiscs, threeQuarter));
        clips ??= TemplateLoader.LoadDefaultAnimations(threeQuarter);
        _animation.SetClips(clips.Count > 0 ? clips : [new AnimationClip(Localizer.T("새 동작"), 8)]);
        CurrentPath = null;
        IsRecovered = false;
        DocumentReset?.Invoke(this, EventArgs.Empty);
    }

    /// <param name="rememberRecent">False for batch runs, which must not touch the user's recent files.</param>
    public void Open(string path, bool rememberRecent = true)
    {
        Load(path);
        CurrentPath = path;
        IsRecovered = false;
        if (rememberRecent)
            Settings.AddRecent(path);
        DocumentReset?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Opens an autosave copy as unsaved changes to <paramref name="originalPath"/> (null = untitled).</summary>
    public void OpenRecovery(string autosavePath, string? originalPath)
    {
        Load(autosavePath);
        CurrentPath = originalPath;
        IsRecovered = true;
    }

    public void Save(string path)
    {
        WriteCopy(path);
        _editor.Character.History.MarkSaved();
        _animation.MarkSaved();
        CurrentPath = path;
        IsRecovered = false;
        Settings.AddRecent(path);
        DocumentReset?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Writes the project without changing the document state (used by autosave). Goes through a
    /// temporary file so a failed write never destroys the previous file.
    /// </summary>
    public void WriteCopy(string path)
    {
        string temp = path + ".tmp";
        using (var stream = File.Create(temp))
            ProjectFile.Save(new ProjectData(_editor.Character, _animation.Clips.ToList()), stream, AvaloniaImageCodec.Instance);
        File.Move(temp, path, overwrite: true);
    }

    private void Load(string path)
    {
        ProjectData data;
        using (var stream = File.OpenRead(path))
            data = ProjectFile.Load(stream, AvaloniaImageCodec.Instance);
        _editor.LoadCharacter(data.Character);
        _animation.SetClips(data.Clips);
    }

    /// <summary>Sheet PNG of the current clip or of all clips, plus a .json description when enabled.</summary>
    public void ExportSheet(string path, bool allClips)
    {
        var clips = allClips ? _animation.Clips.ToList() : _animation.CurrentClip is { } c ? [c] : [];
        if (clips.Count == 0)
            throw new InvalidOperationException("내보낼 동작이 없습니다.");
        var sheet = SpriteSheet.Build(_editor.Character, clips, _editor.Compositor, ExportDirections);
        File.WriteAllBytes(path, AvaloniaImageCodec.Instance.EncodePng(sheet.Image));
        if (Settings.WriteSheetMetadata)
            File.WriteAllText(Path.ChangeExtension(path, ".json"), sheet.MetadataJson());
    }

    /// <summary>The animation clips of another project file.</summary>
    public IReadOnlyList<AnimationClip> ReadClips(string path)
    {
        using var stream = File.OpenRead(path);
        return ProjectFile.Load(stream, AvaloniaImageCodec.Instance).Clips;
    }

    public IReadOnlyList<Rgba> ReadPalette(string path)
    {
        using var stream = File.OpenRead(path);
        return PaletteFile.Read(path, stream, AvaloniaImageCodec.Instance);
    }

    /// <summary>Writes the palette without the transparent entry, in the format named by the extension.</summary>
    public void ExportPalette(string path)
    {
        using var stream = File.Create(path);
        PaletteFile.Write(path, stream, _editor.Character.Palette.Colors.Skip(1).ToList(), AvaloniaImageCodec.Instance, DocumentName);
    }

    public void ExportGif(string path)
    {
        if (_animation.CurrentClip is not { } clip)
            throw new InvalidOperationException("내보낼 동작이 없습니다.");
        using var stream = File.Create(path);
        AnimationGif.Write(stream, _editor.Character, clip, _editor.Compositor, Settings.GifScale, ExportDirections);
    }

    /// <summary>Rows of exported sheets and GIFs: the classic four, or eight with 3/4 views when the setting is on.</summary>
    private IReadOnlyList<Core.Rigging.Direction> ExportDirections => _editor.Character.ExportDirections(Settings.ExportThreeQuarter);

    partial void OnCurrentPathChanged(string? value) => OnPropertyChanged(nameof(DocumentName));

    partial void OnIsRecoveredChanged(bool value) => OnPropertyChanged(nameof(IsDirty));
}
