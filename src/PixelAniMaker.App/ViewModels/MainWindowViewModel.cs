using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Controls;
using PixelAniMaker.App.Services;
using PixelAniMaker.Core.Editing;
using PixelAniMaker.Core.Imaging;
using PixelAniMaker.Core.Rigging;
using PixelAniMaker.Core.Rigging.Body;

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
        Touchup = new TouchupSession(Session, Animation);
        Project = new ProjectService(Session, Animation, AppSettings.Load());
        Autosave = new AutosaveService(Project);
        Shortcuts = new ShortcutMap(Project.Settings.Shortcuts);
        _factory = new DockFactory(Session, Animation, Touchup, Shortcuts);
        CreateLayout(Project.Settings.Layout);

        Session.HistoryChanged += (_, _) => OnDocumentStateChanged();
        Animation.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AnimationSession.HasUnsavedClipChanges))
                OnDocumentStateChanged();
        };
        Project.PropertyChanged += (_, _) => OnDocumentStateChanged();
        Session.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(EditorSession.SecondaryInExport))
                OnPropertyChanged(nameof(SecondaryInExport));
            if (e.PropertyName == nameof(EditorSession.HasUnsavedSettings))
                OnDocumentStateChanged();
            if (e.PropertyName is nameof(EditorSession.Zoom) or nameof(EditorSession.CurrentTool)
                or nameof(EditorSession.PoseMode) or nameof(EditorSession.TouchupMode)
                or nameof(EditorSession.ActivePart) or nameof(EditorSession.Direction) or nameof(EditorSession.CanvasSize))
                OnPropertyChanged(nameof(StatusText));
        };
        RefreshRecentFiles();
        OnDocumentStateChanged();
    }

    public EditorSession Session { get; }
    public AnimationSession Animation { get; }
    public TouchupSession Touchup { get; }
    public ProjectService Project { get; }
    public AutosaveService Autosave { get; }
    public ShortcutMap Shortcuts { get; }

    /// <summary>What each shortcut id runs (command + parameter).</summary>
    public IReadOnlyDictionary<string, (System.Windows.Input.ICommand Command, object? Parameter)> ShortcutCommands =>
        _shortcutCommands ??= new Dictionary<string, (System.Windows.Input.ICommand, object?)>
        {
            ["New"] = (NewDocumentCommand, null),
            ["Open"] = (OpenCommand, null),
            ["Save"] = (SaveCommand, null),
            ["SaveAs"] = (SaveAsCommand, null),
            ["ExportSheet"] = (ExportCurrentSheetCommand, null),
            ["Undo"] = (UndoCommand, null),
            ["Redo"] = (RedoCommand, null),
            ["RedoAlt"] = (RedoCommand, null),
            ["Tool.Pencil"] = (SelectToolCommand, ToolCatalog.Pencil),
            ["Tool.Eraser"] = (SelectToolCommand, ToolCatalog.Eraser),
            ["Tool.Fill"] = (SelectToolCommand, ToolCatalog.Fill),
            ["Tool.Eyedropper"] = (SelectToolCommand, ToolCatalog.Eyedropper),
            ["Tool.Line"] = (SelectToolCommand, ToolCatalog.Line),
            ["Tool.Rectangle"] = (SelectToolCommand, ToolCatalog.Rectangle),
            ["Tool.Ellipse"] = (SelectToolCommand, ToolCatalog.Ellipse),
            ["Tool.Select"] = (SelectToolCommand, ToolCatalog.Select),
            ["ToggleSymmetry"] = (ToggleSymmetryCommand, null),
            ["DeleteSelection"] = (DeleteSelectionCommand, null),
            ["Deselect"] = (DeselectCommand, null),
            ["Direction.Front"] = (SetDirectionCommand, Direction.Front),
            ["Direction.Left"] = (SetDirectionCommand, Direction.Left),
            ["Direction.Right"] = (SetDirectionCommand, Direction.Right),
            ["Direction.Back"] = (SetDirectionCommand, Direction.Back),
            ["Direction.FrontLeft"] = (SetDirectionCommand, Direction.FrontLeft),
            ["Direction.FrontRight"] = (SetDirectionCommand, Direction.FrontRight),
            ["Direction.BackLeft"] = (SetDirectionCommand, Direction.BackLeft),
            ["Direction.BackRight"] = (SetDirectionCommand, Direction.BackRight),
            ["ToggleGrid"] = (ToggleGridCommand, null),
            ["ToggleDim"] = (ToggleDimOtherPartsCommand, null),
            ["TogglePose"] = (TogglePoseModeCommand, null),
            ["ToggleSkeleton"] = (ToggleSkeletonEditCommand, null),
            ["ZoomIn"] = (ZoomInCommand, null),
            ["ZoomOut"] = (ZoomOutCommand, null),
            ["SaveKey"] = (SaveKeyCommand, null),
            ["PreviousFrame"] = (PreviousFrameCommand, null),
            ["NextFrame"] = (NextFrameCommand, null),
            ["ToggleOnion"] = (ToggleOnionSkinCommand, null),
            ["ToggleTouchup"] = (ToggleTouchupModeCommand, null),
        };

    private Dictionary<string, (System.Windows.Input.ICommand, object?)>? _shortcutCommands;

    public bool IsKorean => Project.Settings.Language != Localizer.English;

    public bool IsEnglish => Project.Settings.Language == Localizer.English;

    /// <summary>Saves the UI language; the running program keeps its language until restarted.</summary>
    [RelayCommand]
    private async Task SetLanguage(string language)
    {
        if (Project.Settings.Language == language)
            return;
        Project.Settings.Language = language;
        Project.Settings.Save();
        OnPropertyChanged(nameof(IsKorean));
        OnPropertyChanged(nameof(IsEnglish));
        if (Dialogs is not null)
            await Dialogs.ShowMessageAsync("언어를 바꿨습니다. 프로그램을 다시 시작하면 적용됩니다.");
    }

    [RelayCommand]
    private async Task EditShortcuts()
    {
        if (Dialogs is null)
            return;
        await Dialogs.EditShortcutsAsync(Shortcuts);
        Project.Settings.Shortcuts = Shortcuts.Overrides();
        Project.Settings.Save();
    }

    /// <summary>Set by the window; dialogs are a view concern.</summary>
    public IFileDialogs? Dialogs { get; set; }

    public ObservableCollection<string> RecentFiles { get; } = [];

    /// <summary>Project setting: exports include secondary motion.</summary>
    public bool SecondaryInExport
    {
        get => Session.SecondaryInExport;
        set
        {
            Session.SecondaryInExport = value;
            OnPropertyChanged();
        }
    }

    public bool ExportThreeQuarter
    {
        get => Project.Settings.ExportThreeQuarter;
        set
        {
            Project.Settings.ExportThreeQuarter = value;
            Project.Settings.Save();
            OnPropertyChanged();
        }
    }

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
        $"{Session.Direction.Label()}   ·   {Mode}   ·   {(Session.TouchupMode ? "완성 프레임" : Session.ActivePart.Label)}   ·   " +
        $"{Session.Character.Width}×{Session.Character.Height}   ·   ×{Session.Zoom}";

    private string Mode =>
        Session.PoseMode ? "포즈" : Session.TouchupMode ? $"손보기 · {Session.CurrentTool.Label}" : Session.CurrentTool.Label;

    // ------------------------------------------------------------------ file

    [RelayCommand]
    private Task NewDocument() => NewAsync(jointDiscs: true);

    [RelayCommand]
    private Task NewPlainDocument() => NewAsync(jointDiscs: false);

    /// <summary>Starts a new project from the mannequin with the default clips the user ticks.</summary>
    private async Task NewAsync(bool jointDiscs)
    {
        if (!await ConfirmDiscardAsync())
            return;
        double heads = TemplateLoader.ChibiHeads;
        var shape = BodyShape.Standard;
        if (Dialogs is not null)
        {
            var bodies = TemplateLoader.BodyTypes;
            var bodyLabels = bodies.Select(BodyTypeLabel).ToList();
            if (await Dialogs.PickOneAsync("새로 만들기", "키 비율(등신)을 고르세요. 파츠 구성과 기본 동작은 모두 같습니다.",
                    bodyLabels, "다음", bodies.ToList().IndexOf(TemplateLoader.ChibiHeads)) is not { } body)
                return;
            heads = bodies[body];

            var shapes = TemplateLoader.BodyShapes;
            if (await Dialogs.PickOneAsync("새로 만들기", "체형을 고르세요. 어깨·허리·골반과 팔다리 굵기가 달라지고, 키와 기본 동작은 같습니다.",
                    shapes.Select(BodyShapeLabel).ToList(), "다음", 0) is not { } picked)
                return;
            shape = shapes[picked];
        }
        var clips = TemplateLoader.LoadDefaultAnimations(threeQuarter: true, heads, library: true);
        bool threeQuarter = false;
        if (Dialogs is not null)
        {
            // the last row is an option, off by default: 3/4 views make the file format 2
            var labels = clips.Select(c => $"{c.Name}  ({c.FrameCount}프레임 · {c.Fps} fps)").ToList();
            labels.Add("반측면 포함 (앞·뒤 반측면 마네킹과 동작, 이전 버전에서는 열리지 않음)");
            var ticked = clips.Select(TemplateLoader.IsBaseClip).Append(false).ToList();
            if (await Dialogs.PickItemsAsync("새로 만들기", "만들 동작을 고르세요. 기본 동작 6개가 골라져 있습니다.\n" +
                    "고르지 않은 동작은 나중에 파일 → 기본 동작 가져오기로 추가하거나 새로 만들 수 있습니다.",
                    labels, "만들기", ticked) is not { } picked)
                return;
            threeQuarter = picked.Contains(clips.Count);
            clips = picked.Where(i => i < clips.Count).Select(i => clips[i]).ToList();
        }
        if (!threeQuarter)
            foreach (var clip in clips)
                Core.Rigging.ThreeQuarterViews.StripFrom(clip);
        Project.New(jointDiscs, clips, threeQuarter, heads, shape);
    }

    private static string BodyShapeLabel(BodyShape shape) => shape switch   // translated on screen
    {
        BodyShape.Feminine => "여성형  (좁은 어깨 · 가는 허리 · 넓은 골반)",
        BodyShape.Masculine => "남성형  (넓은 어깨 · 곧은 허리 · 좁은 골반)",
        BodyShape.Slim => "마른형  (전체적으로 가늘게)",
        BodyShape.Chubby => "통통형  (둥근 배 · 굵은 팔다리)",
        BodyShape.Muscular => "근육형  (넓은 어깨 · 두꺼운 가슴 · 굵은 팔다리)",
        _ => "기본  (지금까지의 마네킹)",
    };

    private static string BodyTypeLabel(double heads)
    {
        var p = Core.Rigging.Body.BodyProportions.For(heads);
        string label = $"{heads}등신  (머리 {p.HeadPixels}px · 키 {Math.Round(p.BodyHeightPixels)}px)";   // translated on screen
        return heads == TemplateLoader.ChibiHeads ? label + " · 치비, 손으로 그린 기본 마네킹" : label;
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
        $"{Project.DocumentName}_{Animation.CurrentClip?.Name}", path => Project.ExportSheet(path, allClips: false), allClips: false);

    [RelayCommand]
    private Task ExportAllSheet() => ExportAsync("전체 동작 시트 내보내기", ProjectService.PngType,
        $"{Project.DocumentName}_all", path => Project.ExportSheet(path, allClips: true), allClips: true);

    [RelayCommand]
    private Task ExportCurrentFrames() => ExportFramesAsync(allClips: false);

    [RelayCommand]
    private Task ExportAllFrames() => ExportFramesAsync(allClips: true);

    /// <summary>Frame PNGs go next to the picked file name, which starts each frame's name.</summary>
    private async Task ExportFramesAsync(bool allClips)
    {
        if (Dialogs is null || await Dialogs.PickSaveFileAsync("프레임별 PNG 내보내기 (이름 앞부분과 폴더 고르기)",
                ProjectService.PngType, Project.DocumentName) is not { } path)
            return;
        int count = 0;
        if (await TryAsync(() => count = Project.ExportFrames(path, allClips), "내보내지 못했습니다"))
            await Dialogs.ShowMessageAsync($"PNG {count}개를 저장했습니다.\n{Path.GetDirectoryName(path)}"
                + (Project.EdgeWarning(allClips) is { } warning ? "\n\n" + warning : ""));
    }

    [RelayCommand]
    private Task ExportGif() => ExportAsync("GIF 내보내기", ProjectService.GifType,
        $"{Project.DocumentName}_{Animation.CurrentClip?.Name}", Project.ExportGif, allClips: false);

    /// <summary>
    /// First thing after the window opens: offer to recover work left by a crash, otherwise open the
    /// file given on the command line.
    /// </summary>
    public async Task StartAsync(string? openPath)
    {
        if (await TryRecoverAsync())
            return;
        if (openPath is not null)
            await OpenPathAsync(openPath);
    }

    /// <summary>Called when the window closes normally: keep the layout, drop autosave copies.</summary>
    public void OnClosed(WindowPlacement placement)
    {
        Autosave.DiscardCurrent();
        if (Layout is not null)
            Project.Settings.Layout = LayoutState.Capture(Layout);
        Project.Settings.Window = placement;
        Project.Settings.Save();
    }

    private async Task<bool> TryRecoverAsync()
    {
        var found = AutosaveService.FindRecoveries();
        if (found.Count == 0 || Dialogs is null)
            return false;
        var latest = found[0];
        string what = latest.OriginalPath is null ? "제목 없는 문서" : $"'{Path.GetFileName(latest.OriginalPath)}'";
        bool recover = await Dialogs.ConfirmRecoveryAsync(
            $"지난번에 프로그램이 정상적으로 닫히지 않았습니다.\n{what}의 저장되지 않은 작업({latest.SavedAt:yyyy-MM-dd HH:mm})을 복구할까요?");
        bool ok = recover && await TryAsync(() => Project.OpenRecovery(latest.File, latest.OriginalPath), "복구하지 못했습니다");
        foreach (var r in found)
            AutosaveService.Discard(r);
        OnPropertyChanged(nameof(StatusText));
        return ok;
    }

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

    [RelayCommand]
    private async Task LoadReference()
    {
        if (Dialogs is not null && await Dialogs.PickOpenFileAsync("참고 이미지 불러오기", ProjectService.ImageType) is { } path)
            await TryAsync(() => Session.Reference.Load(path, Session.Character.Width, Session.Character.Height),
                "참고 이미지를 불러오지 못했습니다");
    }

    [RelayCommand]
    private void ClearReference() => Session.Reference.Clear();

    /// <summary>Copies chosen animation clips from another project (keys only; touch-ups stay behind).</summary>
    [RelayCommand]
    private async Task ImportClips()
    {
        if (Dialogs is null || await Dialogs.PickOpenFileAsync("동작 가져오기", ProjectService.ProjectType) is not { } path)
            return;
        IReadOnlyList<Core.Animation.AnimationClip> clips = [];
        if (!await TryAsync(() => clips = Project.ReadClips(path), $"'{Path.GetFileName(path)}'을(를) 읽을 수 없습니다"))
            return;
        if (clips.Count == 0)
        {
            await Dialogs.ShowErrorAsync("가져올 동작이 없습니다.");
            return;
        }
        await PickAndImportAsync(clips, $"'{Path.GetFileName(path)}'에서 가져올 동작을 고르세요.\n" +
            "키프레임만 가져오며, 손본 픽셀은 그 캐릭터 전용이라 가져오지 않습니다. 같은 이름은 뒤에 번호가 붙습니다.");
    }

    /// <summary>Adds chosen clips of the built-in library (variants, actions, poses), fitted to this character's height.</summary>
    [RelayCommand]
    private async Task ImportLibraryClips()
    {
        if (Dialogs is null)
            return;
        var clips = TemplateLoader.LibraryClipsFor(Session.Character, Session.Compositor);
        await PickAndImportAsync(clips, "추가할 기본 동작을 고르세요. 같은 동작의 여러 버전(대기·걷기·달리기·점프·공격·피격), " +
            "그 밖의 동작, 한 프레임짜리 자세가 있습니다. 이동 거리는 이 캐릭터의 키에 맞춥니다. 같은 이름은 뒤에 번호가 붙습니다.");
    }

    private async Task PickAndImportAsync(IReadOnlyList<Core.Animation.AnimationClip> clips, string message)
    {
        var labels = clips.Select(c => $"{c.Name}  ({c.FrameCount}프레임 · {c.Fps} fps)").ToList();
        if (await Dialogs!.PickItemsAsync("동작 가져오기", message, labels, "가져오기") is not { Count: > 0 } picked)
            return;

        var result = Core.Animation.ClipImport.Import(picked.Select(i => clips[i]), Animation.Clips.Select(c => c.Name), Session.Character);
        Core.Rigging.WeaponHold.FitUnrecorded(Session.Character, result.Clips);   // weapon poses: hands on this body's gun or sword
        foreach (var clip in result.Clips)
            Animation.AddClip(clip);
        if (result.UnknownParts.Count > 0)
            await Dialogs.ShowErrorAsync("이 캐릭터에 없는 파츠의 회전은 무시됩니다: " + string.Join(", ", result.UnknownParts));
        if (result.DroppedThreeQuarter)
            await Dialogs.ShowMessageAsync("이 캐릭터에는 반측면이 없어서 반측면 키와 손본 픽셀은 가져오지 않았습니다.");
    }

    [RelayCommand]
    private Task ExportPalette() => ExportAsync("팔레트 내보내기", ProjectService.PaletteType, Project.DocumentName, Project.ExportPalette);

    /// <summary>Reads a palette file, previews the character with it, then swaps or merges it (undoable).</summary>
    [RelayCommand]
    private async Task ImportPalette()
    {
        if (Dialogs is null || await Dialogs.PickOpenFileAsync("팔레트 불러오기", ProjectService.PaletteType) is not { } path)
            return;
        IReadOnlyList<Rgba> colors = [];
        if (!await TryAsync(() => colors = Project.ReadPalette(path), "팔레트를 읽지 못했습니다"))
            return;
        if (colors.Count == 0)
        {
            await Dialogs.ShowErrorAsync("팔레트에 색이 없습니다.");
            return;
        }

        var character = Session.Character;
        var palette = character.Palette;
        var composite = Session.Compositor.Compose(character, Session.Direction);
        string message = $"'{Path.GetFileName(path)}'에서 {colors.Count}색을 읽었습니다 (지금 팔레트 {palette.Count - 1}색).\n" +
                         "색 번호대로 교체: 1번 색부터 차례로 바꿉니다 — 그림은 그대로, 색만 바뀐 변형 캐릭터가 됩니다.\n" +
                         "새 색으로 추가: 팔레트에 없는 색만 뒤에 붙입니다.";
        var choice = await Dialogs.ConfirmPaletteImportAsync(message,
            CompositeBitmap.From(composite, palette), CompositeBitmap.From(composite, PaletteSwap.Preview(palette, colors)));
        if (choice == PaletteImportChoice.Swap)
            PaletteSwap.Swap(palette, character.History, colors);
        else if (choice == PaletteImportChoice.Merge)
            PaletteSwap.Merge(palette, character.History, colors);
    }

    /// <param name="allClips">Set for sheet and GIF exports: after writing, warns when their frames touch the canvas edge.</param>
    private async Task ExportAsync(string title, FileType type, string suggestedName, Action<string> export, bool? allClips = null)
    {
        if (Dialogs is not null && await Dialogs.PickSaveFileAsync(title, type, suggestedName) is { } path
            && await TryAsync(() => export(path), "내보내지 못했습니다")
            && allClips is { } all && Project.EdgeWarning(all) is { } warning)
            await Dialogs.ShowMessageAsync(warning);
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

    /// <summary>Back to the default window layout; the saved layout is forgotten.</summary>
    [RelayCommand]
    private void ResetLayout()
    {
        Project.Settings.Layout = null;
        Project.Settings.Save();
        CreateLayout(null);
    }

    private void CreateLayout(LayoutState? saved)
    {
        var layout = _factory.CreateLayout();
        _factory.InitLayout(layout);
        saved?.ApplyTo(layout, _factory);
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
    private void SetDirection(Direction direction)
    {
        if (Session.Directions.Contains(direction))   // 3/4 shortcuts do nothing until 3/4 views are on
            Session.Direction = direction;
    }

    [RelayCommand]
    private void AddThreeQuarter() => Session.AddThreeQuarter();

    /// <summary>Fills every clip's empty 3/4 tracks from its front/back and side keys (one undo step).</summary>
    [RelayCommand]
    private async Task DraftThreeQuarter()
    {
        if (!Core.Animation.ThreeQuarterDraft.Apply(Animation.Clips, Session.Character.History) && Dialogs is not null)
            await Dialogs.ShowMessageAsync("채울 반측면 트랙이 없습니다. 반측면 키가 이미 있거나, 정면·측면 키가 없는 동작만 있습니다.");
    }

    /// <summary>Asks for margins and resizes the canvas (undoable); explains when the size is out of range.</summary>
    [RelayCommand]
    private async Task ResizeCanvas()
    {
        if (Dialogs is null || await Dialogs.PickCanvasMarginsAsync(Session.Character.Width, Session.Character.Height) is not { } margins)
            return;
        if (!Session.ResizeCanvas(margins, Animation.Clips))
            await Dialogs.ShowErrorAsync($"캔버스는 가로·세로 {Core.Rigging.CanvasResize.MinSize}~{Core.Rigging.CanvasResize.MaxSize} px이어야 합니다.");
    }

    /// <summary>Asks how to enlarge the pixels and doubles the resolution (undoable).</summary>
    [RelayCommand]
    private async Task ScaleResolution()
    {
        if (Dialogs is null)
            return;
        var (w, h) = (Session.Character.Width, Session.Character.Height);
        if (!Core.Rigging.ResolutionScale.CanApply(Session.Character))
        {
            await Dialogs.ShowErrorAsync($"캔버스는 가로·세로 {Core.Rigging.CanvasResize.MaxSize} px까지입니다 (지금 {w}×{h}).");
            return;
        }
        if (await Dialogs.PickOneAsync("해상도 2배", $"캔버스와 모든 그림을 가로·세로 2배로 키웁니다 ({w}×{h} → {w * 2}×{h * 2}).\n" +
                "관절·장착점·동작의 몸 이동·손본 픽셀도 함께 커지고, 되돌리기 한 번으로 돌아갑니다.",
                ["그대로 키우기 (픽셀 하나 → 2×2, 모양 그대로)", "부드럽게 키우기 (Scale2x, 대각선 계단을 메움)"], "키우기") is not { } choice)
            return;
        Session.ScaleResolution(choice == 0 ? Core.Rigging.UpscaleMethod.Nearest : Core.Rigging.UpscaleMethod.Smooth, Animation.Clips);
    }

    /// <summary>Removes the 3/4 views and the clips' 3/4 keys and touch-ups (undoable).</summary>
    [RelayCommand]
    private void RemoveThreeQuarter() => Session.RemoveThreeQuarter(Animation.Clips);

    [RelayCommand] private void SaveKey() => Animation.SaveKey();
    [RelayCommand] private void DeleteKey() => Animation.DeleteKey();
    [RelayCommand] private void PreviousFrame() => Animation.Step(-1);
    [RelayCommand] private void NextFrame() => Animation.Step(1);
    [RelayCommand] private void ToggleOnionSkin() => Animation.OnionSkin = !Animation.OnionSkin;
    [RelayCommand] private void ToggleTouchupMode() => Session.TouchupMode = !Session.TouchupMode;
    [RelayCommand] private void ClearTouchup() => Animation.ClearTouchup();

    [RelayCommand]
    private void ToggleGrid() => Session.ShowGrid = !Session.ShowGrid;

    [RelayCommand]
    private void TogglePoseMode() => Session.PoseMode = !Session.PoseMode;

    [RelayCommand]
    private void ToggleSkeletonEdit() => Session.SkeletonEdit = !Session.SkeletonEdit;

    [RelayCommand]
    private void ToggleDimOtherParts() => Session.DimOtherParts = !Session.DimOtherParts;

    [RelayCommand]
    private void ToggleSymmetry() => Session.Symmetric = !Session.Symmetric;

    /// <summary>The image the selection tool works on: the finished frame in touch-up mode, else the part.</summary>
    private EditorDocument? SelectionDocument => Session.TouchupMode ? Touchup.Document : Session.ActiveDocument;

    [RelayCommand]
    private void DeleteSelection()
    {
        if (Session.TouchupMode || Session.CanDrawActivePart)
            SelectionDocument?.DeleteSelection();
    }

    [RelayCommand]
    private void Deselect()
    {
        if (SelectionDocument is { } d)
            d.Selection = null;
    }

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
