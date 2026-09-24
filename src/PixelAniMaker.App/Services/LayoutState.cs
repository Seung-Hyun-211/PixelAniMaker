using Dock.Model.Controls;
using Dock.Model.Core;

namespace PixelAniMaker.App.Services;

/// <summary>Main window placement.</summary>
public sealed record WindowPlacement(int X, int Y, double Width, double Height, bool Maximized);

/// <summary>
/// The parts of the docking layout worth keeping between sessions: the size of every named area and
/// which area each panel sits in. Areas and panels are matched by their ids, so panels dragged into a
/// brand-new split fall back to their default place.
/// </summary>
public sealed record LayoutState(Dictionary<string, double> Proportions, Dictionary<string, string> PanelOwners)
{
    public static LayoutState Capture(IDockable root)
    {
        var proportions = new Dictionary<string, double>();
        var owners = new Dictionary<string, string>();
        foreach (var d in Walk(root))
        {
            if (string.IsNullOrEmpty(d.Id))
                continue;
            if (d is IDock && !double.IsNaN(d.Proportion))
                proportions[d.Id] = d.Proportion;
            if (d is ITool && d.Owner is IDock { Id: { Length: > 0 } ownerId })
                owners[d.Id] = ownerId;
        }
        return new LayoutState(proportions, owners);
    }

    /// <summary>Applies the saved sizes and panel places to a freshly created default layout.</summary>
    public void ApplyTo(IRootDock root, IFactory factory)
    {
        var byId = Walk(root).Where(d => !string.IsNullOrEmpty(d.Id)).GroupBy(d => d.Id).ToDictionary(g => g.Key, g => g.First());

        foreach (var (panelId, ownerId) in PanelOwners)
        {
            if (byId.GetValueOrDefault(panelId) is ITool panel
                && byId.GetValueOrDefault(ownerId) is IToolDock target
                && panel.Owner != target)
            {
                factory.RemoveDockable(panel, collapse: false);
                factory.AddDockable(target, panel);
                target.ActiveDockable = panel;
            }
        }

        foreach (var (id, proportion) in Proportions)
            if (byId.GetValueOrDefault(id) is IDock dock)
                dock.Proportion = proportion;
    }

    private static IEnumerable<IDockable> Walk(IDockable dockable)
    {
        yield return dockable;
        if (dockable is IDock { VisibleDockables: { } children })
            foreach (var child in children.ToList())
                foreach (var d in Walk(child))
                    yield return d;
    }
}
