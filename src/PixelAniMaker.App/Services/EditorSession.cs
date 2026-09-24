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

    /// <summary>Opacity (0-255) of the parts that are not being edited, in draw mode.</summary>
    private const byte DimAlpha = 70;

    private readonly Compositor _compositor = new();
    private readonly Dictionary<Direction, WriteableBitmap> _previewBitmaps = [];
    private uint[] _pixels = [];

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

    public WriteableBitmap PreviewBitmap(Direction direction) => _previewBitmaps[direction];

    /// <summary>Raised after the bitmaps have been refreshed.</summary>
    public event EventHandler? ImageUpdated;

    /// <summary>Raised when the undo history or dirty state changes.</summary>
    public event EventHandler? HistoryChanged;

    public void NewCharacter()
    {
        if (Character is not null)
        {
            Character.History.Changed -= OnHistoryChanged;
            Character.Palette.Changed -= OnContentChanged;
            Character.PoseChanged -= OnContentChanged;
        }

        var character = TemplateLoader.LoadChibi96();
        character.History.Changed += OnHistoryChanged;
        character.Palette.Changed += OnContentChanged;
        character.PoseChanged += OnContentChanged;

        var size = new PixelSize(character.Width, character.Height);
        CanvasBitmap = CreateBitmap(size);
        foreach (var d in DirectionExtensions.All)
            _previewBitmaps[d] = CreateBitmap(size);
        _pixels = new uint[character.Width * character.Height];

        Character = character;
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
        PoseChange.Apply(CurrentPose, Character.History, part.Name, degrees);

    public void ResetPose() => PoseChange.ResetAll(CurrentPose, Character.History);

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
    }

    private void OnContentChanged(object? sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        if (Character is null || ActivePart is null)
            return;
        Transforms = Character.ComputeTransforms(Direction);
        var composites = DirectionExtensions.All.ToDictionary(d => d, d => _compositor.Compose(Character, d));
        SourceComposite = composites[Direction.Source()];

        foreach (var (d, composite) in composites)
            Render(composite, dimExcept: null, _previewBitmaps[d]);
        Render(composites[Direction], DimOtherParts && !PoseMode ? Character.IndexOf(ActivePart) : null, CanvasBitmap);
        ImageUpdated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Writes a composite into a bitmap, optionally fading every part except one.</summary>
    private unsafe void Render(CompositeResult composite, int? dimExcept, WriteableBitmap bitmap)
    {
        for (int i = 0; i < composite.Indices.Length; i++)
        {
            var c = Character.Palette[composite.Indices[i]];
            short owner = composite.Owners[i];
            bool dim = dimExcept is { } keep && owner != keep && owner != CompositeResult.NoPart;
            _pixels[i] = (dim ? c with { A = DimAlpha } : c).ToBgra32();
        }

        using var fb = bitmap.Lock();
        int width = fb.Size.Width;
        fixed (uint* src = _pixels)
        {
            for (int y = 0; y < fb.Size.Height; y++)
                Buffer.MemoryCopy(src + y * width, (byte*)fb.Address + y * fb.RowBytes, fb.RowBytes, width * 4);
        }
    }

    private static WriteableBitmap CreateBitmap(PixelSize size) =>
        new(size, new Avalonia.Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);
}
