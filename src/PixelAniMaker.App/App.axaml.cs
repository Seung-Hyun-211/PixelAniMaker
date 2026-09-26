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
            Localizer.Initialize(AppSettings.Load().Language);
            if (args.IsExport)
                Dispatcher.UIThread.Post(() => desktop.Shutdown(BatchExporter.Run(args.ExportPath!, args.OutputFolder, Console.Out, args.Directions, args.Frames)));
            else
                desktop.MainWindow = CreateMainWindow(args.OpenPath);
        }
        base.OnFrameworkInitializationCompleted();
    }

    private static MainWindow CreateMainWindow(string? openPath)
    {
        var vm = new MainWindowViewModel();
        var window = new MainWindow { DataContext = vm };
        window.Opened += async (_, _) => await vm.StartAsync(openPath);
        return window;
    }
}
