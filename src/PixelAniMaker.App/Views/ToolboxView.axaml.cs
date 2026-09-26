using Avalonia.Controls;
using Avalonia.Input;
using PixelAniMaker.App.Services;
using PixelAniMaker.App.ViewModels;

namespace PixelAniMaker.App.Views;

public partial class ToolboxView : UserControl
{
    public ToolboxView() => InitializeComponent();

    /// <summary>
    /// Clicking a tool always leaves pose mode, also when it is the tool already selected (the selection
    /// then does not change, so the binding alone would not notice).
    /// </summary>
    private void OnToolTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is ToolboxViewModel vm && sender is ListBox { SelectedItem: ToolItem tool })
            vm.Session.SelectTool(tool);
    }
}
