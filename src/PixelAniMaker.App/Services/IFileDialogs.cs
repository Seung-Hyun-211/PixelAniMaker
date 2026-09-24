namespace PixelAniMaker.App.Services;

public enum UnsavedChoice
{
    Save,
    Discard,
    Cancel,
}

/// <summary>A file type offered in save/open dialogs.</summary>
public sealed record FileType(string Name, string Extension);

/// <summary>Dialogs the view layer provides to view models.</summary>
public interface IFileDialogs
{
    Task<string?> PickOpenFileAsync(string title, FileType type);
    Task<string?> PickSaveFileAsync(string title, FileType type, string suggestedName);

    /// <summary>Asks what to do with unsaved changes before they would be lost.</summary>
    Task<UnsavedChoice> ConfirmUnsavedAsync(string documentName);

    Task ShowErrorAsync(string message);

    /// <summary>True to recover the autosaved work, false to discard it.</summary>
    Task<bool> ConfirmRecoveryAsync(string message);
}
