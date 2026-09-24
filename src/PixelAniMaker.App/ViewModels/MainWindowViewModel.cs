using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Controls;
using PixelAniMaker.App.Services;

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
            if (e.PropertyName is nameof(EditorSession.Zoom) or nameof(EditorSession.CurrentTool))
                OnPropertyChanged(nameof(StatusText));
        };
        OnHistoryChanged();
    }

    public EditorSession Session { get; }

    public string StatusText =>
        $"{Session.CurrentTool.Label}   ·   " +
        $"{Session.Document.Image.Width}×{Session.Document.Image.Height}   ·   ×{Session.Zoom}";

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
        Session.NewDocument(64, 128);
        OnPropertyChanged(nameof(StatusText));
    }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo() => Session.Document.Undo();

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo() => Session.Document.Redo();

    private bool CanUndo() => Session.Document.History.CanUndo;
    private bool CanRedo() => Session.Document.History.CanRedo;

    [RelayCommand]
    private void SelectTool(ToolItem tool) => Session.CurrentTool = tool;

    [RelayCommand]
    private void ToggleGrid() => Session.ShowGrid = !Session.ShowGrid;

    [RelayCommand]
    private void ToggleTemplate() => Session.ShowTemplate = !Session.ShowTemplate;

    [RelayCommand]
    private void ZoomIn() => Session.ZoomBy(1);

    [RelayCommand]
    private void ZoomOut() => Session.ZoomBy(-1);

    private void OnHistoryChanged()
    {
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
        Title = $"PixelAniMaker - 제목 없음{(Session.Document.History.IsDirty ? " *" : "")}";
    }
}
