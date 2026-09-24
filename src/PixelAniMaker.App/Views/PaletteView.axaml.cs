using Avalonia.Controls;
using Avalonia.Input;
using PixelAniMaker.App.ViewModels;

namespace PixelAniMaker.App.Views;

public partial class PaletteView : UserControl
{
    public PaletteView() => InitializeComponent();

    private void OnSwatchPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { DataContext: SwatchViewModel swatch } control || DataContext is not PaletteViewModel vm)
            return;
        var props = e.GetCurrentPoint(control).Properties;
        vm.Select(swatch, secondary: props.IsRightButtonPressed);
        e.Handled = true;
    }
}
