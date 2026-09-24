using System.Numerics;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using PixelAniMaker.Core.Editing;
using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.App.Services;

/// <summary>
/// State shared by every panel: the character, the direction and part being edited, the current tool
/// and mode, and the composited bitmaps shown by the canvas and the previews.
/// </summary>
/// <remarks>
/// Geometry (transforms, hit-testing) is kept in the stored source direction; the Right view is the
/// Left view mirrored, so canvas input and overlays go through <see cref="ToSource"/>/<see cref="FromSource"/>.
/// </remarks>
public sealed partial class EditorSession : ObservableObject
{
    public static readonly int[] ZoomSteps = [1, 2, 3, 4, 6, 8, 12, 16, 24, 32, 48];

    private readonly Dictionary<Direction, WriteableBitmap> _previewBitmaps = [];

    [ObservableProperty] private Character _character = null!;
    [ObservableProperty] private Direction _direction = Direction.Front;
    [ObservableProperty] private Part _activePart = null!;
    [ObservableProperty] private EditorDocument _activeDocument = null!;
    [ObservableProperty] private WriteableBitmap _canvasBitmap = null!;
    [ObservableProperty] private ToolItem _currentTool = ToolCatalog.Pencil;
    [ObservableProperty] private bool _poseMode;
    [ObservableProperty] private bool _dimOtherParts = true;
    [ObservableProperty] private int _zoom = 4;
    [ObservableProperty] private bool _showGrid = true;
    [ObservableProperty] private string _cursorText = "";

    public EditorSession() => NewCharacter();

    /// <summary>Composite of the current direction's source view (for hit-testing in source coordinates).</summary>
    public CompositeResult SourceComposite { get; private set; } = null!;

    /// <summary>Placement of each part for the current direction, in source coordinates.</summary>
    public IReadOnlyDictionary<Part, PartTransform> Transforms { get; private set; } = null!;

    public PartTransform ActiveTransform => Transforms[ActivePart];

    public Pose CurrentPose => Character.PoseFor(Direction);

    /// <summary>Automatic 1px outline on composites (off: parts are used exactly as drawn).</summary>
    public bool AutoOutline
    {
        get => Character.Outline.Enabled;
        set
        {
            Character.Outline.Enabled = value;
            OnPropertyChanged();
        }
    }

    public WriteableBitmap PreviewBitmap(Direction direction) => _previewBitmaps[direction];

    /// <summary>Shared compositor (keeps the rotation caches warm for the canvas and animation frames).</summary>
    public Compositor Compositor { get; } = new();

    /// <summary>Raised after the bitmaps have been refreshed.</summary>
    public event EventHandler? ImageUpdated;

    /// <summary>Raised when the undo history or dirty state changes.</summary>
    public event EventHandler? HistoryChanged;

    /// <summary>Raised when anything that changes rendered frames changes: pixels, palette, outline, undo/redo.</summary>
    public event EventHandler? ContentChanged;

    public void NewCharacter()
    {
        if (Character is not null)
        {
            Character.History.Changed -= OnHistoryChanged;
            Character.Palette.Changed -= OnContentChanged;
            Character.Outline.Changed -= OnContentChanged;
            Character.PoseChanged -= OnPoseChanged;
        }

        var character = TemplateLoader.LoadChibi96();
        character.History.Changed += OnHistoryChanged;
        character.Palette.Changed += OnContentChanged;
        character.Outline.Changed += OnContentChanged;
        character.PoseChanged += OnPoseChanged;

        CanvasBitmap = CompositeBitmap.Create(character.Width, character.Height);
        foreach (var d in DirectionExtensions.All)
            _previewBitmaps[d] = CompositeBitmap.Create(character.Width, character.Height);

        Character = character;
        OnPropertyChanged(nameof(AutoOutline));
        ActivePart = character.Find("chest") ?? character.Root;
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ZoomBy(int steps)
    {
        int i = Array.FindLastIndex(ZoomSteps, z => z <= Zoom);
        Zoom = ZoomSteps[Math.Clamp(i + steps, 0, ZoomSteps.Length - 1)];
    }

    /// <summary>Rotates a joint in the current direction as one undoable step.</summary>
    public void SetRotation(Part part, double degrees) =>
        PoseChange.Apply(CurrentPose, Character.History, p => p.Set(part.Name, degrees));

    /// <summary>Moves the whole body (root offset, whole pixels) as one undoable step.</summary>
    public void SetOffset(Vector2 offset) =>
        PoseChange.Apply(CurrentPose, Character.History, p => p.Offset = offset, "몸 위치");

    public void ResetPose() =>
        PoseChange.Apply(CurrentPose, Character.History, p => p.Restore(PoseData.Rest), "포즈 초기화");

    /// <summary>Picks a drawing tool; always leaves pose mode, even when the tool is already selected.</summary>
    public void SelectTool(ToolItem tool)
    {
        CurrentTool = tool;
        PoseMode = false;
    }

    public void Undo() => ActiveDocument.Undo();

    public void Redo() => ActiveDocument.Redo();

    /// <summary>Display canvas point → source-direction point (mirrors x in the Right view).</summary>
    public Vector2 ToSource(double x, double y) =>
        new((float)(Direction.IsMirrored() ? Character.Width - x : x), (float)y);

    /// <summary>Source-direction point → display canvas point.</summary>
    public Vector2 FromSource(Vector2 p) => Direction.IsMirrored() ? p with { X = Character.Width - p.X } : p;

    partial void OnActivePartChanged(Part value) => ReopenDocument();

    partial void OnDirectionChanged(Direction value) => ReopenDocument();

    partial void OnCurrentToolChanged(ToolItem value) => PoseMode = false;

    partial void OnPoseModeChanged(bool value) => Refresh();

    partial void OnDimOtherPartsChanged(bool value) => Refresh();

    /// <summary>Points drawing at the active part's image for the current direction.</summary>
    private void ReopenDocument()
    {
        if (ActivePart is null)
            return;
        if (ActiveDocument is not null)
        {
            ActiveDocument.EndStroke();
            ActiveDocument.PixelsChanged -= OnContentChanged;
        }
        ActiveDocument = Character.CreateDocument(ActivePart, Direction);
        ActiveDocument.PixelsChanged += OnContentChanged;
        Refresh();
    }

    private void OnHistoryChanged(object? sender, EventArgs e)
    {
        Refresh(); // undo/redo may have changed any part's pixels or pose
        HistoryChanged?.Invoke(this, EventArgs.Empty);
        ContentChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnContentChanged(object? sender, EventArgs e)
    {
        Refresh();
        ContentChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnPoseChanged(object? sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        if (Character is null || ActivePart is null)
            return;
        Transforms = Character.ComputeTransforms(Direction);
        var composites = DirectionExtensions.All.ToDictionary(d => d, d => Compositor.Compose(Character, d));
        SourceComposite = composites[Direction.Source()];

        foreach (var (d, composite) in composites)
            CompositeBitmap.Write(composite, Character.Palette, _previewBitmaps[d]);
        CompositeBitmap.Write(composites[Direction], Character.Palette, CanvasBitmap,
            DimOtherParts && !PoseMode ? Character.IndexOf(ActivePart) : null);
        ImageUpdated?.Invoke(this, EventArgs.Empty);
    }
}
