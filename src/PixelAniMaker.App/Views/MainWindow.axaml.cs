using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PixelAniMaker.App.Services;
using PixelAniMaker.App.ViewModels;

namespace PixelAniMaker.App.Views;

public partial class MainWindow : Window, IFileDialogs
{
    private bool _closeConfirmed;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.Dialogs = this;
                RestorePlacement(vm.Project.Settings.Window);
                ApplyShortcuts(vm);
                vm.Shortcuts.PropertyChanged += (_, _) => ApplyShortcuts(vm);
                BuildRecentMenu(vm);
                vm.RecentFiles.CollectionChanged += (_, _) => BuildRecentMenu(vm);
            }
        };
    }

    /// <summary>One menu item per recent file (built in code: a container style would also hit the parent item).</summary>
    private void BuildRecentMenu(MainWindowViewModel vm)
    {
        RecentMenu.Items.Clear();
        foreach (var path in vm.RecentFiles)
            RecentMenu.Items.Add(new MenuItem
            {
                Header = path.Replace("_", "__"), // "_" would otherwise mark an access key and vanish
                Command = vm.OpenRecentCommand,
                CommandParameter = path,
            });
        RecentMenu.IsEnabled = vm.RecentFiles.Count > 0;
    }

    /// <summary>Rebuilds the window's key bindings from the shortcut map.</summary>
    private void ApplyShortcuts(MainWindowViewModel vm)
    {
        KeyBindings.Clear();
        foreach (var (id, (command, parameter)) in vm.ShortcutCommands)
        {
            if (vm.Shortcuts.Get(id) is not { } gesture)
                continue;
            var binding = new KeyBinding { Gesture = gesture, Command = command };
            if (parameter is not null)
                binding.CommandParameter = parameter;
            KeyBindings.Add(binding);
        }
    }

    /// <summary>Puts the window where it was last time (maximized when nothing was saved).</summary>
    private void RestorePlacement(WindowPlacement? p)
    {
        if (p is null || p.Maximized)
        {
            WindowState = WindowState.Maximized;
            return;
        }
        WindowState = WindowState.Normal;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Position = new PixelPoint(p.X, p.Y);
        Width = p.Width;
        Height = p.Height;
    }

    private WindowPlacement CurrentPlacement() =>
        new(Position.X, Position.Y, Width, Height, WindowState == WindowState.Maximized);

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (_closeConfirmed || DataContext is not MainWindowViewModel vm)
            return;
        e.Cancel = true; // decide asynchronously, then close again
        if (await vm.CanCloseAsync())
        {
            _closeConfirmed = true;
            vm.OnClosed(CurrentPlacement());
            Close();
        }
    }

    private void OnExitClick(object? sender, RoutedEventArgs e) => Close();

    // ------------------------------------------------------------------ IFileDialogs

    public async Task<string?> PickOpenFileAsync(string title, FileType type)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = [ToFilter(type)],
        });
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    public async Task<string?> PickSaveFileAsync(string title, FileType type, string suggestedName)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedName + type.Extension,
            DefaultExtension = type.Extension.TrimStart('.'),
            FileTypeChoices = [ToFilter(type)],
            ShowOverwritePrompt = true,
        });
        return file?.TryGetLocalPath();
    }

    public async Task<UnsavedChoice> ConfirmUnsavedAsync(string documentName) =>
        await MessageDialog.ShowAsync(this, "저장하지 않은 변경",
            $"'{documentName}'에 저장하지 않은 변경이 있습니다. 저장할까요?",
            "저장", "저장 안 함", "취소") switch
        {
            0 => UnsavedChoice.Save,
            1 => UnsavedChoice.Discard,
            _ => UnsavedChoice.Cancel,
        };

    public Task ShowErrorAsync(string message) => MessageDialog.ShowAsync(this, "오류", message, "확인");

    public Task EditShortcutsAsync(ShortcutMap shortcuts) => new ShortcutsWindow(shortcuts).ShowDialog(this);

    public async Task<bool> ConfirmRecoveryAsync(string message) =>
        await MessageDialog.ShowAsync(this, "작업 복구", message, "복구", "버리기") == 0;

    private static FilePickerFileType ToFilter(FileType type) => new(type.Name) { Patterns = ["*" + type.Extension] };
}
