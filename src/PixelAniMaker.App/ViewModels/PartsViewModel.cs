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
    [ObservableProperty] private decimal? _offsetX;
    [ObservableProperty] private decimal? _offsetY;
    [ObservableProperty] private string _variantStatus = "";
    [ObservableProperty] private string _variantMessage = "";

    public PartsViewModel(EditorSession session)
    {
        Session = session;
        Id = "Parts";
        Title = "파츠";
        CanClose = false;
        session.HistoryChanged += (_, _) => UpdateVariantStatus(); // variants added/removed or undone
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

    partial void OnOffsetXChanged(decimal? value) => ApplyOffset();

    partial void OnOffsetYChanged(decimal? value) => ApplyOffset();

    private void ApplyOffset()
    {
        if (!_syncing && OffsetX is { } x && OffsetY is { } y)
            Session.SetOffset(new System.Numerics.Vector2((float)x, (float)y));
    }

    [RelayCommand]
    private void ResetPart() => Session.SetRotation(Session.ActivePart, 0);

    [RelayCommand]
    private void ResetPose() => Session.ResetPose();

    [RelayCommand]
    private void AddVariant() => VariantMessage = Session.AddVariantAtCurrentAngle() ?? "";

    [RelayCommand]
    private void RemoveVariant()
    {
        Session.RemoveActiveVariant();
        VariantMessage = "";
    }

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

    private void UpdateVariantStatus()
    {
        var variants = Session.ActiveTransform.View.Variants.All.Keys.Select(a => $"{a}°").ToList();
        VariantStatus = (Session.ActiveVariantAngle is { } angle ? $"그리는 이미지: {angle}° 이미지" : "그리는 이미지: 기본")
            + (variants.Count > 0 ? $"  ·  있음: {string.Join(", ", variants)}" : "");
    }

    /// <summary>Mirrors the session's active part and its rotation without writing back.</summary>
    private void Sync()
    {
        _syncing = true;
        Selected = Items.FirstOrDefault(i => i.Part == Session.ActivePart);
        Rotation = (decimal)Math.Round(Session.CurrentPose.Get(Session.ActivePart.Name), 1);
        OffsetX = (decimal)Math.Round(Session.CurrentPose.Offset.X);
        OffsetY = (decimal)Math.Round(Session.CurrentPose.Offset.Y);
        UpdateVariantStatus();
        _syncing = false;
    }
}
