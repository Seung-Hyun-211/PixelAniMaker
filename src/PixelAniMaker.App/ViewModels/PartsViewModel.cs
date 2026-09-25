using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;
using PixelAniMaker.App.Services;
using PixelAniMaker.Core.Animation;
using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.App.ViewModels;

/// <summary>One row of the part tree: show/hide and lock write back to the session (editing aids, not undoable or saved).</summary>
public sealed partial class PartItem : ObservableObject
{
    private readonly EditorSession _session;

    [ObservableProperty] private bool _visible;
    [ObservableProperty] private bool _locked;

    public PartItem(EditorSession session, Part part)
    {
        _session = session;
        Part = part;
        _visible = !session.IsHidden(part);
        _locked = session.IsLocked(part);
    }

    public Part Part { get; }

    public string Display => new string(' ', Part.Depth * 3) + Part.Label;

    partial void OnVisibleChanged(bool value) => _session.SetHidden(Part, !value);

    partial void OnLockedChanged(bool value) => _session.SetLocked(Part, value);
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
    [ObservableProperty] private bool _ownRight;
    [ObservableProperty] private bool _isRightView;
    [ObservableProperty] private bool _isDetailPart;
    [ObservableProperty] private decimal? _detailX;
    [ObservableProperty] private decimal? _detailY;
    [ObservableProperty] private bool _hasSecondary;
    [ObservableProperty] private int _secondaryModeIndex;
    [ObservableProperty] private decimal? _secondaryPeriod;
    [ObservableProperty] private decimal? _secondaryDamping;
    [ObservableProperty] private decimal? _secondaryStrength;
    [ObservableProperty] private decimal? _secondaryMax;
    [ObservableProperty] private bool _hasLimit;
    [ObservableProperty] private decimal? _limitMin;
    [ObservableProperty] private decimal? _limitMax;

    public PartsViewModel(EditorSession session)
    {
        Session = session;
        Id = "Parts";
        Title = "파츠";
        CanClose = false;
        session.HistoryChanged += (_, _) => Sync(); // variants or right views added/removed or undone
        session.PartsChanged += (_, _) => Attach(); // eye parts added/removed or undone
        session.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(EditorSession.Character))
                Attach();
            else if (e.PropertyName is nameof(EditorSession.ActivePart) or nameof(EditorSession.Direction) or nameof(EditorSession.ActiveLayer))
                Sync();
        };
        Attach();
    }

    public EditorSession Session { get; }

    public ObservableCollection<PartItem> Items { get; } = [];

    /// <summary>Layers of the active part, top first (as in other paint programs).</summary>
    public ObservableCollection<LayerItem> Layers { get; } = [];

    [ObservableProperty] private LayerItem? _selectedLayer;

    partial void OnSelectedLayerChanged(LayerItem? value)
    {
        if (!_syncing && value is not null)
            Session.ActiveLayer = value.Index;
    }

    [RelayCommand] private void AddLayer() => Session.AddLayer();

    [RelayCommand] private void RemoveLayer() => Session.RemoveActiveLayer();

    [RelayCommand] private void LayerUp() => Session.MoveActiveLayer(1);

    [RelayCommand] private void LayerDown() => Session.MoveActiveLayer(-1);

    private void SyncLayers()
    {
        Layers.Clear();
        var layers = Session.ActiveLayers;
        for (int i = layers.Count - 1; i >= 0; i--)
            Layers.Add(new LayerItem(Session, i, layers[i].Name, layers[i].Visible));
        SelectedLayer = Layers.FirstOrDefault(l => l.Index == Session.ActiveLayerIndex);
    }

    /// <summary>Attachment points of the active part in the current direction.</summary>
    public ObservableCollection<AttachmentItem> Attachments { get; } = [];

    partial void OnSelectedChanged(PartItem? value)
    {
        if (!_syncing && value is not null)
            Session.ActivePart = value.Part;
    }

    partial void OnRotationChanged(decimal? value)
    {
        if (!_syncing && value is { } degrees)
        {
            Session.SetRotation(Session.ActivePart, (double)degrees);
            // show the clamped angle even when the pose did not change; posted, because the control
            // ignores a new value while it is still handing over the typed one
            Avalonia.Threading.Dispatcher.UIThread.Post(Sync);
        }
    }

    /// <summary>Turning the limit on starts from ±90°, widened to include the current rotation.</summary>
    partial void OnHasLimitChanged(bool value)
    {
        if (_syncing)
            return;
        double current = Session.CurrentPose.Get(Session.ActivePart.Name);
        Session.SetActiveRotationLimit(value ? new RotationLimit(Math.Min(-90, current), Math.Max(90, current)) : null);
    }

    /// <summary>Choices for <see cref="SecondaryModeIndex"/>, in <see cref="SecondaryMode"/> order.</summary>
    public IReadOnlyList<string> SecondaryModes { get; } = ["변형형 (덩어리)", "회전형 (매달림)"];

    /// <summary>Turning sway on starts from the bust preset for detail parts, the hair preset otherwise.</summary>
    partial void OnHasSecondaryChanged(bool value)
    {
        if (!_syncing)
            Session.SetActiveSecondary(value ? (Session.ActivePart.IsDetail ? SecondarySettings.Bust : SecondarySettings.Hair) : null);
    }

    partial void OnSecondaryModeIndexChanged(int value) => ApplySecondary();
    partial void OnSecondaryPeriodChanged(decimal? value) => ApplySecondary();
    partial void OnSecondaryDampingChanged(decimal? value) => ApplySecondary();
    partial void OnSecondaryStrengthChanged(decimal? value) => ApplySecondary();
    partial void OnSecondaryMaxChanged(decimal? value) => ApplySecondary();

    private void ApplySecondary()
    {
        if (_syncing || !HasSecondary || SecondaryPeriod is not { } period || SecondaryDamping is not { } damping
            || SecondaryStrength is not { } strength || SecondaryMax is not { } max)
            return;
        Session.SetActiveSecondary(new SecondarySettings((SecondaryMode)Math.Clamp(SecondaryModeIndex, 0, 1),
            (float)period, (float)damping, (float)strength, (float)max));
        Avalonia.Threading.Dispatcher.UIThread.Post(Sync);   // show the values as clamped
    }

    /// <summary>Parameter: "Bust", "Hair" or "Cloth".</summary>
    [RelayCommand]
    private void ApplySecondaryPreset(string name) => Session.SetActiveSecondary(name switch
    {
        "Hair" => SecondarySettings.Hair,
        "Cloth" => SecondarySettings.Cloth,
        _ => SecondarySettings.Bust,
    });

    partial void OnLimitMinChanged(decimal? value) => ApplyLimit();

    partial void OnLimitMaxChanged(decimal? value) => ApplyLimit();

    private void ApplyLimit()
    {
        if (!_syncing && HasLimit && LimitMin is { } min && LimitMax is { } max)
            Session.SetActiveRotationLimit(new RotationLimit((double)min, (double)max));
    }

    partial void OnOwnRightChanged(bool value)
    {
        if (!_syncing)
            Session.SetOwnRight(value);
    }

    partial void OnDetailXChanged(decimal? value) => ApplyDetailPosition();

    partial void OnDetailYChanged(decimal? value) => ApplyDetailPosition();

    private void ApplyDetailPosition()
    {
        if (!_syncing && DetailX is { } x && DetailY is { } y)
            Session.MoveActiveDetailTo(new System.Numerics.Vector2((float)x, (float)y));
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
    private void AddEyes() => Session.AddEyes();

    [RelayCommand]
    private void RemoveEyes() => Session.RemoveEyes();

    /// <summary>Parameter: "Small", "Medium" or "Large".</summary>
    [RelayCommand]
    private void AddBust(string size) => Session.AddBust(Enum.Parse<BustSize>(size));

    [RelayCommand]
    private void RemoveBust() => Session.RemoveBust();

    [RelayCommand]
    private void ResetPose() => Session.ResetPose();

    /// <summary>Adds a point at the part's joint; move it with the X/Y fields.</summary>
    [RelayCommand]
    private void AddAttachment()
    {
        var view = Session.ActiveTransform.View;
        EditAttachments(points => points.Set(AttachmentChange.FreeName(points), view.LocalPivot));
    }

    private void EditAttachments(Action<AttachmentPoints> edit) =>
        AttachmentChange.Apply(Session.ActiveTransform.View.Attachments, Session.Character.History, edit);

    /// <summary>Rows follow the points; existing rows are updated in place so a field being edited keeps focus.</summary>
    private void SyncAttachments()
    {
        var points = Session.ActiveTransform.View.Attachments.All;
        if (Attachments.Select(a => a.Key).SequenceEqual(points.Keys))
        {
            foreach (var row in Attachments)
                row.Show(points[row.Key]);
            return;
        }
        Attachments.Clear();
        foreach (var (name, p) in points)
            Attachments.Add(new AttachmentItem(name, p, EditAttachments));
    }

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
            Items.Add(new PartItem(Session, part));
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
        IsRightView = Session.Direction.IsMirrored();
        OwnRight = Session.Direction.IsMirrored() && Session.ActivePart.HasOwnView(Session.Direction);
        var detail = Session.ActiveDetailPosition;
        IsDetailPart = detail is not null;
        var sway = Session.ActivePart.Secondary;
        HasSecondary = sway is not null;
        SecondaryModeIndex = (int)(sway?.Mode ?? SecondaryMode.Deform);
        SecondaryPeriod = sway is null ? null : (decimal)sway.Period;
        SecondaryDamping = sway is null ? null : (decimal)Math.Round(sway.Damping, 2);
        SecondaryStrength = sway is null ? null : (decimal)Math.Round(sway.Strength, 2);
        SecondaryMax = sway is null ? null : (decimal)sway.Max;
        var limit = Session.ActivePart.Limit;
        HasLimit = limit is not null;
        LimitMin = limit is { } l ? (decimal)l.Min : null;
        LimitMax = limit is { } m ? (decimal)m.Max : null;
        DetailX = detail is { } d ? (decimal)Math.Round(d.X, 1) : null;
        DetailY = detail is { } e ? (decimal)Math.Round(e.Y, 1) : null;
        UpdateVariantStatus();
        SyncAttachments();
        SyncLayers();
        _syncing = false;
    }
}
