namespace PixelAniMaker.App.Services;

public enum UnsavedChoice
{
    Save,
    Discard,
    Cancel,
}

public enum PaletteImportChoice
{
    /// <summary>Replace colours by index (colour variant).</summary>
    Swap,

    /// <summary>Add the colours that are missing.</summary>
    Merge,
    Cancel,
}

/// <summary>A file type offered in save/open dialogs; the first extension is the default.</summary>
public sealed record FileType(string Name, params string[] Extensions)
{
    public string Extension => Extensions[0];
}

/// <summary>Dialogs the view layer provides to view models.</summary>
public interface IFileDialogs
{
    Task<string?> PickOpenFileAsync(string title, FileType type);
    Task<string?> PickSaveFileAsync(string title, FileType type, string suggestedName);

    /// <summary>Asks what to do with unsaved changes before they would be lost.</summary>
    Task<UnsavedChoice> ConfirmUnsavedAsync(string documentName);

    Task ShowErrorAsync(string message);

    /// <summary>Lets the user view and change keyboard shortcuts.</summary>
    Task EditShortcutsAsync(ShortcutMap shortcuts);

    /// <summary>Shows the character now and with the imported palette swapped in, and asks how to import.</summary>
    Task<PaletteImportChoice> ConfirmPaletteImportAsync(string message, Avalonia.Media.IImage before, Avalonia.Media.IImage after);

    /// <summary>Checklist (all ticked at first); the ticked indices, or null when cancelled.</summary>
    Task<IReadOnlyList<int>?> PickItemsAsync(string title, string message, IReadOnlyList<string> items, string okLabel);

    /// <summary>True to recover the autosaved work, false to discard it.</summary>
    Task<bool> ConfirmRecoveryAsync(string message);
}
