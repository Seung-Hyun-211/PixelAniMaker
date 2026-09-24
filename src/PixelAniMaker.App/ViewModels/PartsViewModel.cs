using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;
using PixelAniMaker.App.Services;
using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.App.ViewModels;

public sealed record PartItem(Part Part)
{
    public string Display => new string(' ', Part.Depth * 3) + Part.Label;
}

/// <summary>Skeleton hierarchy: pick the part to edit and set its joint rotation in the current direction.</summary>
public sealed partial class PartsViewModel : Tool
{
    private Character? _character;
    private bool _syncing;

    [ObservableProperty] private PartItem? _selected;
    [ObservableProperty] private decimal? _rotation;

    public PartsViewModel(EditorSession session)
    {
        Session = session;
        Id = "Parts";
        Title = "파츠";
        CanClose = false;
        session.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(EditorSession.Character))
                Attach();
            else if (e.PropertyName is nameof(EditorSession.ActivePart) or nameof(EditorSession.Direction))
                Sync();
        };
        Attach();
    }

    public EditorSession Session { get; }

    public ObservableCollection<PartItem> Items { get; } = [];

    partial void OnSelectedChanged(PartItem? value)
    {
        if (!_syncing && value is not null)
            Session.ActivePart = value.Part;
    }

    partial void OnRotationChanged(decimal? value)
    {
        if (!_syncing && value is { } degrees)
            Session.SetRotation(Session.ActivePart, (double)degrees);
    }

    [RelayCommand]
    private void ResetPart() => Session.SetRotation(Session.ActivePart, 0);

    [RelayCommand]
    private void ResetPose() => Session.ResetPose();

    private void Attach()
    {
        if (_character is not null)
            _character.PoseChanged -= OnPoseChanged;
        _character = Session.Character;
        _character.PoseChanged += OnPoseChanged;

        Items.Clear();
        foreach (var part in Session.Character.Hierarchy())
            Items.Add(new PartItem(part));
        Sync();
    }

    private void OnPoseChanged(object? sender, EventArgs e) => Sync();

    /// <summary>Mirrors the session's active part and its rotation without writing back.</summary>
    private void Sync()
    {
        _syncing = true;
        Selected = Items.FirstOrDefault(i => i.Part == Session.ActivePart);
        Rotation = (decimal)Math.Round(Session.CurrentPose.Get(Session.ActivePart.Name), 1);
        _syncing = false;
    }
}
