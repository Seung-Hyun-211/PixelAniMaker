using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;
using PixelAniMaker.App.Services;

namespace PixelAniMaker.App.ViewModels;

/// <summary>Builds the default window layout: tools | canvas | palette + preview.</summary>
public sealed class DockFactory : Factory
{
    private readonly EditorSession _session;

    public DockFactory(EditorSession session) => _session = session;

    public override IRootDock CreateLayout()
    {
        var canvas = new CanvasDocumentViewModel(_session);
        var toolbox = new ToolboxViewModel(_session);
        var palette = new PaletteViewModel(_session);
        var preview = new PreviewViewModel(_session);

        var left = new ToolDock
        {
            Id = "LeftDock",
            Proportion = 0.16,
            Alignment = Alignment.Left,
            VisibleDockables = CreateList<IDockable>(toolbox),
            ActiveDockable = toolbox,
        };

        var documents = new DocumentDock
        {
            Id = "Documents",
            IsCollapsable = false,
            CanCreateDocument = false,
            VisibleDockables = CreateList<IDockable>(canvas),
            ActiveDockable = canvas,
        };

        var rightTop = new ToolDock
        {
            Id = "RightTopDock",
            Proportion = 0.45,
            Alignment = Alignment.Right,
            VisibleDockables = CreateList<IDockable>(preview),
            ActiveDockable = preview,
        };

        var rightBottom = new ToolDock
        {
            Id = "RightBottomDock",
            Proportion = 0.55,
            Alignment = Alignment.Right,
            VisibleDockables = CreateList<IDockable>(palette),
            ActiveDockable = palette,
        };

        var right = new ProportionalDock
        {
            Id = "RightColumn",
            Proportion = 0.24,
            Orientation = Orientation.Vertical,
            VisibleDockables = CreateList<IDockable>(rightTop, new ProportionalDockSplitter(), rightBottom),
        };

        var main = new ProportionalDock
        {
            Id = "MainLayout",
            Orientation = Orientation.Horizontal,
            VisibleDockables = CreateList<IDockable>(left, new ProportionalDockSplitter(), documents,
                new ProportionalDockSplitter(), right),
        };

        var root = CreateRootDock();
        root.Id = "Root";
        root.IsCollapsable = false;
        root.VisibleDockables = CreateList<IDockable>(main);
        root.DefaultDockable = main;
        root.ActiveDockable = main;
        return root;
    }

    public override void InitLayout(IDockable layout)
    {
        HostWindowLocator = new Dictionary<string, Func<IHostWindow?>>
        {
            [nameof(IDockWindow)] = () => new HostWindow(),
        };
        base.InitLayout(layout);
    }
}
