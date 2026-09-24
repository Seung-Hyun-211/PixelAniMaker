using CommunityToolkit.Mvvm.ComponentModel;
using PixelAniMaker.Core.Export;
using PixelAniMaker.Core.Project;

namespace PixelAniMaker.App.Services;

/// <summary>New / open / save of .dotchar projects and sprite sheet / GIF export.</summary>
public sealed partial class ProjectService : ObservableObject
{
    public static readonly FileType ProjectType = new("PixelAniMaker 프로젝트", ProjectFile.Extension);
    public static readonly FileType PngType = new("PNG 이미지", ".png");
    public static readonly FileType GifType = new("GIF 애니메이션", ".gif");

    private readonly EditorSession _editor;
    private readonly AnimationSession _animation;

    [ObservableProperty] private string? _currentPath;

    public ProjectService(EditorSession editor, AnimationSession animation, AppSettings settings)
    {
        _editor = editor;
        _animation = animation;
        Settings = settings;
    }

    public AppSettings Settings { get; }

    public string DocumentName => CurrentPath is null ? "제목 없음" : Path.GetFileNameWithoutExtension(CurrentPath);

    public bool IsDirty => _editor.Character.History.IsDirty || _animation.HasUnsavedClipChanges;

    public void New()
    {
        _editor.LoadCharacter(TemplateLoader.LoadChibi96());
        _animation.SetClips(TemplateLoader.LoadDefaultAnimations());
        CurrentPath = null;
    }

    /// <param name="rememberRecent">False for batch runs, which must not touch the user's recent files.</param>
    public void Open(string path, bool rememberRecent = true)
    {
        ProjectData data;
        using (var stream = File.OpenRead(path))
            data = ProjectFile.Load(stream, AvaloniaImageCodec.Instance);
        _editor.LoadCharacter(data.Character);
        _animation.SetClips(data.Clips);
        CurrentPath = path;
        if (rememberRecent)
            Settings.AddRecent(path);
    }

    /// <summary>Writes to a temporary file first so a failed save never destroys the previous file.</summary>
    public void Save(string path)
    {
        string temp = path + ".tmp";
        using (var stream = File.Create(temp))
            ProjectFile.Save(new ProjectData(_editor.Character, _animation.Clips.ToList()), stream, AvaloniaImageCodec.Instance);
        File.Move(temp, path, overwrite: true);

        _editor.Character.History.MarkSaved();
        _animation.MarkSaved();
        CurrentPath = path;
        Settings.AddRecent(path);
    }

    /// <summary>Sheet PNG of the current clip or of all clips, plus a .json description when enabled.</summary>
    public void ExportSheet(string path, bool allClips)
    {
        var clips = allClips ? _animation.Clips.ToList() : _animation.CurrentClip is { } c ? [c] : [];
        if (clips.Count == 0)
            throw new InvalidOperationException("내보낼 동작이 없습니다.");
        var sheet = SpriteSheet.Build(_editor.Character, clips, _editor.Compositor);
        File.WriteAllBytes(path, AvaloniaImageCodec.Instance.EncodePng(sheet.Image));
        if (Settings.WriteSheetMetadata)
            File.WriteAllText(Path.ChangeExtension(path, ".json"), sheet.MetadataJson());
    }

    public void ExportGif(string path)
    {
        if (_animation.CurrentClip is not { } clip)
            throw new InvalidOperationException("내보낼 동작이 없습니다.");
        using var stream = File.Create(path);
        AnimationGif.Write(stream, _editor.Character, clip, _editor.Compositor, Settings.GifScale);
    }

    partial void OnCurrentPathChanged(string? value) => OnPropertyChanged(nameof(DocumentName));
}
