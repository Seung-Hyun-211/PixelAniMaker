using System.Numerics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.App.ViewModels;

/// <summary>One attachment point row: rename, move (image pixels) or remove; every change is one undo step.</summary>
public sealed partial class AttachmentItem : ObservableObject
{
    private readonly Action<Action<AttachmentPoints>> _edit;
    private readonly bool _ready;

    [ObservableProperty] private string _name;
    [ObservableProperty] private decimal? _x;
    [ObservableProperty] private decimal? _y;

    /// <param name="edit">Applies an undoable edit to the part view's points.</param>
    public AttachmentItem(string name, Vector2 position, Action<Action<AttachmentPoints>> edit)
    {
        _edit = edit;
        _name = name;
        Key = name;
        Show(position);
        _ready = true;
    }

    /// <summary>The stored name (what <see cref="Name"/> was before an edit).</summary>
    public string Key { get; }

    /// <summary>Updates the numbers without writing back (after undo/redo).</summary>
    public void Show(Vector2 position)
    {
        _x = (decimal)Math.Round(position.X, 1);
        _y = (decimal)Math.Round(position.Y, 1);
        OnPropertyChanged(nameof(X));
        OnPropertyChanged(nameof(Y));
    }

    partial void OnXChanged(decimal? value) => Move();

    partial void OnYChanged(decimal? value) => Move();

    partial void OnNameChanged(string value)
    {
        string name = value.Trim();
        if (!_ready || name.Length == 0 || name == Key)
            return;
        _edit(points =>
        {
            if (points.All.ContainsKey(name) || !points.All.TryGetValue(Key, out var p))
                return;                                   // taken names are ignored
            points.Remove(Key);
            points.Set(name, p);
        });
    }

    private void Move()
    {
        if (_ready && X is { } x && Y is { } y)
            _edit(points => points.Set(Key, new Vector2((float)x, (float)y)));
    }

    [RelayCommand]
    private void Remove() => _edit(points => points.Remove(Key));
}
