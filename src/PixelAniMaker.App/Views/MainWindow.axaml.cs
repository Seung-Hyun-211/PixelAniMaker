using Avalonia.Controls;
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
                vm.Dialogs = this;
        };
    }

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (_closeConfirmed || DataContext is not MainWindowViewModel vm)
            return;
        e.Cancel = true; // decide asynchronously, then close again
        if (await vm.CanCloseAsync())
        {
            _closeConfirmed = true;
            vm.OnClosed();
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

    public async Task<bool> ConfirmRecoveryAsync(string message) =>
        await MessageDialog.ShowAsync(this, "작업 복구", message, "복구", "버리기") == 0;

    private static FilePickerFileType ToFilter(FileType type) => new(type.Name) { Patterns = ["*" + type.Extension] };
}
