using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using PixelAniMaker.App.Services;
using PixelAniMaker.App.ViewModels;
using PixelAniMaker.App.Views;

namespace PixelAniMaker.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var args = CommandLine.Parse(desktop.Args ?? []);
            if (args.IsExport)
                Dispatcher.UIThread.Post(() => desktop.Shutdown(BatchExporter.Run(args.ExportPath!, args.OutputFolder, Console.Out)));
            else
                desktop.MainWindow = CreateMainWindow(args.OpenPath);
        }
        base.OnFrameworkInitializationCompleted();
    }

    private static MainWindow CreateMainWindow(string? openPath)
    {
        var vm = new MainWindowViewModel();
        var window = new MainWindow { DataContext = vm };
        if (openPath is not null)
            window.Opened += async (_, _) => await vm.OpenAtStartupAsync(openPath);
        return window;
    }
}
