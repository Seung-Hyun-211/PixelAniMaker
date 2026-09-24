using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;
using PixelAniMaker.App.Services;

namespace PixelAniMaker.App.ViewModels;

/// <summary>
/// Builds the default window layout:
/// tools + parts | canvas + timeline | preview + animation + palette.
/// </summary>
public sealed class DockFactory(EditorSession session, AnimationSession animation, TouchupSession touchup, ShortcutMap shortcuts)
    : Factory
{
    public override IRootDock CreateLayout()
    {
        var canvas = new CanvasDocumentViewModel(session, animation, touchup);
        var documents = new DocumentDock
        {
            Id = "Documents",
            Proportion = 0.7,
            IsCollapsable = false,
            CanCreateDocument = false,
            VisibleDockables = CreateList<IDockable>(canvas),
            ActiveDockable = canvas,
        };

        var left = Column("LeftColumn", 0.18,
            Panel("LeftTopDock", 0.55, Alignment.Left, new ToolboxViewModel(session, shortcuts)),
            Panel("LeftBottomDock", 0.45, Alignment.Left, new PartsViewModel(session)));
        var center = Column("CenterColumn", 0.56,
            documents,
            Panel("BottomDock", 0.3, Alignment.Bottom, new TimelineViewModel(session, animation, touchup, shortcuts)));
        var right = Column("RightColumn", 0.26,
            Panel("RightTopDock", 0.36, Alignment.Right, new PreviewViewModel(session)),
            Panel("RightMiddleDock", 0.36, Alignment.Right, new AnimationPreviewViewModel(animation)),
            Panel("RightBottomDock", 0.28, Alignment.Right, new PaletteViewModel(session)));

        var main = Split("MainLayout", Orientation.Horizontal, left, center, right);

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

    private ToolDock Panel(string id, double proportion, Alignment alignment, Tool tool) => new()
    {
        Id = id,
        Proportion = proportion,
        Alignment = alignment,
        VisibleDockables = CreateList<IDockable>(tool),
        ActiveDockable = tool,
    };

    private ProportionalDock Column(string id, double proportion, params IDockable[] children)
    {
        var column = Split(id, Orientation.Vertical, children);
        column.Proportion = proportion;
        return column;
    }

    /// <summary>A proportional dock with splitters between the children.</summary>
    private ProportionalDock Split(string id, Orientation orientation, params IDockable[] children)
    {
        var items = new List<IDockable>();
        foreach (var child in children)
        {
            if (items.Count > 0)
                items.Add(new ProportionalDockSplitter());
            items.Add(child);
        }
        return new ProportionalDock
        {
            Id = id,
            Orientation = orientation,
            VisibleDockables = CreateList(items.ToArray()),
        };
    }
}
