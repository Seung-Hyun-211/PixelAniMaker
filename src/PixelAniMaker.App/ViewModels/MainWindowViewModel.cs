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
        _factory = new DockFactory(Session);
        ResetLayout();
        Session.HistoryChanged += (_, _) => OnHistoryChanged();
        Session.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(EditorSession.Zoom) or nameof(EditorSession.CurrentTool)
                or nameof(EditorSession.PoseMode) or nameof(EditorSession.ActivePart) or nameof(EditorSession.Direction))
                OnPropertyChanged(nameof(StatusText));
        };
        OnHistoryChanged();
    }

    public EditorSession Session { get; }

    public string StatusText =>
        $"{Session.Direction.Label()}   ·   {(Session.PoseMode ? "포즈" : Session.CurrentTool.Label)}   ·   {Session.ActivePart.Label}   ·   " +
        $"{Session.Character.Width}×{Session.Character.Height}   ·   ×{Session.Zoom}";

    [RelayCommand]
    private void ResetLayout()
    {
        var layout = _factory.CreateLayout();
        _factory.InitLayout(layout);
        Layout = layout;
    }

    [RelayCommand]
    private void NewDocument()
    {
        Session.NewCharacter();
        OnPropertyChanged(nameof(StatusText));
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

    private void OnHistoryChanged()
    {
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
        Title = $"PixelAniMaker - 제목 없음{(Session.Character.History.IsDirty ? " *" : "")}";
    }
}
