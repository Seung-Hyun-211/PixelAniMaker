using CommunityToolkit.Mvvm.ComponentModel;
using PixelAniMaker.App.Services;

namespace PixelAniMaker.App.ViewModels;

/// <summary>One row of the layer list: show/hide and rename write back through the session (undoable).</summary>
public sealed partial class LayerItem : ObservableObject
{
    private readonly EditorSession _session;
    private readonly bool _ready;

    [ObservableProperty] private string _name;
    [ObservableProperty] private bool _visible;

    public LayerItem(EditorSession session, int index, string name, bool visible)
    {
        _session = session;
        Index = index;
        _name = Localizer.T(name);   // the default "기본" is shown in the UI language
        _visible = visible;
        _ready = true;
    }

    /// <summary>Position in the view's layer list (0 = bottom).</summary>
    public int Index { get; }

    partial void OnNameChanged(string value)
    {
        if (_ready && value.Trim().Length > 0 && value.Trim() != Localizer.T(_session.ActiveLayers[Index].Name))
            _session.RenameLayer(Index, value.Trim());
    }

    partial void OnVisibleChanged(bool value)
    {
        if (_ready)
            _session.SetLayerVisible(Index, value);
    }
}
