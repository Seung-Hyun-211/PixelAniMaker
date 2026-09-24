using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Controls;
using PixelAniMaker.App.Services;
using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.App.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly DockFactory _factory;

    [ObservableProperty] private IRootDock? _layout;
    [ObservableProperty] private string _title = "";

    public MainWindowViewModel()
    {
        Session = new EditorSession();
        Animation = new AnimationSession(Session);
        Project = new ProjectService(Session, Animation, AppSettings.Load());
        _factory = new DockFactory(Session, Animation);
        ResetLayout();

        Session.HistoryChanged += (_, _) => OnDocumentStateChanged();
        Animation.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AnimationSession.HasUnsavedClipChanges))
                OnDocumentStateChanged();
        };
        Project.PropertyChanged += (_, _) => OnDocumentStateChanged();
        Session.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(EditorSession.Zoom) or nameof(EditorSession.CurrentTool)
                or nameof(EditorSession.PoseMode) or nameof(EditorSession.ActivePart) or nameof(EditorSession.Direction))
                OnPropertyChanged(nameof(StatusText));
        };
        RefreshRecentFiles();
        OnDocumentStateChanged();
    }

    public EditorSession Session { get; }
    public AnimationSession Animation { get; }
    public ProjectService Project { get; }

    /// <summary>Set by the window; dialogs are a view concern.</summary>
    public IFileDialogs? Dialogs { get; set; }

    public ObservableCollection<string> RecentFiles { get; } = [];

    public bool WriteSheetMetadata
    {
        get => Project.Settings.WriteSheetMetadata;
        set
        {
            Project.Settings.WriteSheetMetadata = value;
            Project.Settings.Save();
            OnPropertyChanged();
        }
    }

    public string StatusText =>
        $"{Session.Direction.Label()}   ·   {(Session.PoseMode ? "포즈" : Session.CurrentTool.Label)}   ·   {Session.ActivePart.Label}   ·   " +
        $"{Session.Character.Width}×{Session.Character.Height}   ·   ×{Session.Zoom}";

    // ------------------------------------------------------------------ file

    [RelayCommand]
    private async Task NewDocument()
    {
        if (await ConfirmDiscardAsync())
            Project.New();
    }

    [RelayCommand]
    private async Task Open()
    {
        if (Dialogs is null || !await ConfirmDiscardAsync())
            return;
        if (await Dialogs.PickOpenFileAsync("프로젝트 열기", ProjectService.ProjectType) is { } path)
            await OpenPathAsync(path);
    }

    [RelayCommand]
    private async Task OpenRecent(string path)
    {
        if (await ConfirmDiscardAsync())
            await OpenPathAsync(path);
    }

    [RelayCommand]
    private Task<bool> Save() => Project.CurrentPath is { } path ? SaveToAsync(path) : SaveAs();

    [RelayCommand]
    private async Task<bool> SaveAs()
    {
        if (Dialogs is null)
            return false;
        var path = await Dialogs.PickSaveFileAsync("다른 이름으로 저장", ProjectService.ProjectType, Project.DocumentName);
        return path is not null && await SaveToAsync(path);
    }

    [RelayCommand]
    private Task ExportCurrentSheet() => ExportAsync("현재 동작 시트 내보내기", ProjectService.PngType,
        $"{Project.DocumentName}_{Animation.CurrentClip?.Name}", path => Project.ExportSheet(path, allClips: false));

    [RelayCommand]
    private Task ExportAllSheet() => ExportAsync("전체 동작 시트 내보내기", ProjectService.PngType,
        $"{Project.DocumentName}_all", path => Project.ExportSheet(path, allClips: true));

    [RelayCommand]
    private Task ExportGif() => ExportAsync("GIF 내보내기", ProjectService.GifType,
        $"{Project.DocumentName}_{Animation.CurrentClip?.Name}", Project.ExportGif);

    /// <summary>Opens a file given on the command line once the window is up.</summary>
    public Task OpenAtStartupAsync(string path) => OpenPathAsync(path);

    /// <summary>Called by the window before it closes; false keeps it open.</summary>
    public Task<bool> CanCloseAsync() => ConfirmDiscardAsync();

    /// <summary>True when there is nothing unsaved, or the user saved or chose to discard it.</summary>
    private async Task<bool> ConfirmDiscardAsync()
    {
        if (!Project.IsDirty || Dialogs is null)
            return true;
        return await Dialogs.ConfirmUnsavedAsync(Project.DocumentName) switch
        {
            UnsavedChoice.Save => await Save(),
            UnsavedChoice.Discard => true,
            _ => false,
        };
    }

    private async Task OpenPathAsync(string path)
    {
        if (await TryAsync(() => Project.Open(path), $"'{Path.GetFileName(path)}'을(를) 열 수 없습니다"))
            RefreshRecentFiles();
        OnPropertyChanged(nameof(StatusText));
    }

    private async Task<bool> SaveToAsync(string path)
    {
        bool ok = await TryAsync(() => Project.Save(path), "저장하지 못했습니다");
        RefreshRecentFiles();
        return ok;
    }

    private async Task ExportAsync(string title, FileType type, string suggestedName, Action<string> export)
    {
        if (Dialogs is not null && await Dialogs.PickSaveFileAsync(title, type, suggestedName) is { } path)
            await TryAsync(() => export(path), "내보내지 못했습니다");
    }

    private async Task<bool> TryAsync(Action action, string failure)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException
                                       or InvalidOperationException or NotSupportedException or InvalidDataException)
        {
            if (Dialogs is not null)
                await Dialogs.ShowErrorAsync($"{failure}.\n\n{ex.Message}");
            return false;
        }
    }

    private void RefreshRecentFiles()
    {
        RecentFiles.Clear();
        foreach (var path in Project.Settings.RecentFiles)
            RecentFiles.Add(path);
    }

    // ------------------------------------------------------------------ edit & view

    [RelayCommand]
    private void ResetLayout()
    {
        var layout = _factory.CreateLayout();
        _factory.InitLayout(layout);
        Layout = layout;
    }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo() => Session.Undo();

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo() => Session.Redo();

    private bool CanUndo() => Session.Character.History.CanUndo;
    private bool CanRedo() => Session.Character.History.CanRedo;

    [RelayCommand]
    private void SelectTool(ToolItem tool) => Session.SelectTool(tool);

    [RelayCommand]
    private void SetDirection(Direction direction) => Session.Direction = direction;

    [RelayCommand] private void SaveKey() => Animation.SaveKey();
    [RelayCommand] private void DeleteKey() => Animation.DeleteKey();
    [RelayCommand] private void PreviousFrame() => Animation.Step(-1);
    [RelayCommand] private void NextFrame() => Animation.Step(1);
    [RelayCommand] private void ToggleOnionSkin() => Animation.OnionSkin = !Animation.OnionSkin;

    [RelayCommand]
    private void ToggleGrid() => Session.ShowGrid = !Session.ShowGrid;

    [RelayCommand]
    private void TogglePoseMode() => Session.PoseMode = !Session.PoseMode;

    [RelayCommand]
    private void ToggleDimOtherParts() => Session.DimOtherParts = !Session.DimOtherParts;

    [RelayCommand]
    private void ZoomIn() => Session.ZoomBy(1);

    [RelayCommand]
    private void ZoomOut() => Session.ZoomBy(-1);

    private void OnDocumentStateChanged()
    {
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
        Title = $"PixelAniMaker - {Project.DocumentName}{(Project.IsDirty ? " *" : "")}";
        OnPropertyChanged(nameof(StatusText));
    }
}
