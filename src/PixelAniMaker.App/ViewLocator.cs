using Avalonia.Controls;
using Avalonia.Controls.Templates;
using PixelAniMaker.App.ViewModels;
using PixelAniMaker.App.Views;

namespace PixelAniMaker.App;

/// <summary>Maps dock panel view models to their views.</summary>
public sealed class ViewLocator : IDataTemplate
{
    private static readonly Dictionary<Type, Func<Control>> Views = new()
    {
        [typeof(CanvasDocumentViewModel)] = () => new CanvasDocumentView(),
        [typeof(ToolboxViewModel)] = () => new ToolboxView(),
        [typeof(PaletteViewModel)] = () => new PaletteView(),
        [typeof(PreviewViewModel)] = () => new PreviewView(),
        [typeof(PartsViewModel)] = () => new PartsView(),
    };

    public Control? Build(object? data) =>
        data is not null && Views.TryGetValue(data.GetType(), out var create)
            ? create()
            : new TextBlock { Text = $"View not found: {data?.GetType().Name}" };

    public bool Match(object? data) => data is not null && Views.ContainsKey(data.GetType());
}
