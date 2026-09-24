using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using PixelAniMaker.Core.Editing;
using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.App.Services;

/// <summary>
/// State shared by every panel: the character, the part being edited, the current tool and mode,
/// and the composited bitmaps shown by the canvas and the previews.
/// </summary>
public sealed partial class EditorSession : ObservableObject
{
    public static readonly int[] ZoomSteps = [1, 2, 3, 4, 6, 8, 12, 16, 24, 32, 48];

    /// <summary>Opacity (0-255) of the parts that are not being edited, in draw mode.</summary>
    private const byte DimAlpha = 70;

    private readonly Compositor _compositor = new();
    private uint[] _canvasPixels = [];
    private uint[] _previewPixels = [];

    [ObservableProperty] private Character _character = null!;
    [ObservableProperty] private Part _activePart = null!;
    [ObservableProperty] private EditorDocument _activeDocument = null!;
    [ObservableProperty] private WriteableBitmap _canvasBitmap = null!;
    [ObservableProperty] private WriteableBitmap _previewBitmap = null!;
    [ObservableProperty] private ToolItem _currentTool = ToolCatalog.Pencil;
    [ObservableProperty] private bool _poseMode;
    [ObservableProperty] private bool _dimOtherParts = true;
    [ObservableProperty] private int _zoom = 4;
    [ObservableProperty] private bool _showGrid = true;
    [ObservableProperty] private string _cursorText = "";

    public EditorSession() => NewCharacter();

    /// <summary>Latest composite (colours + which part owns each pixel).</summary>
    public CompositeResult Composite { get; private set; } = null!;

    /// <summary>Canvas placement of each part for the current pose.</summary>
    public IReadOnlyDictionary<Part, PartTransform> Transforms { get; private set; } = null!;

    public PartTransform ActiveTransform => Transforms[ActivePart];

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
            Character.Pose.Changed -= OnContentChanged;
        }

        var character = TemplateLoader.LoadChibi64();
        character.History.Changed += OnHistoryChanged;
        character.Palette.Changed += OnContentChanged;
        character.Pose.Changed += OnContentChanged;

        var size = new PixelSize(character.Width, character.Height);
        CanvasBitmap = CreateBitmap(size);
        PreviewBitmap = CreateBitmap(size);
        _canvasPixels = new uint[character.Width * character.Height];
        _previewPixels = new uint[_canvasPixels.Length];

        Character = character;
        ActivePart = character.Find("chest") ?? character.Root;
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ZoomBy(int steps)
    {
        int i = Array.FindLastIndex(ZoomSteps, z => z <= Zoom);
        Zoom = ZoomSteps[Math.Clamp(i + steps, 0, ZoomSteps.Length - 1)];
    }

    /// <summary>Rotates a joint as one undoable step.</summary>
    public void SetRotation(Part part, double degrees) =>
        PoseChange.Apply(Character.Pose, Character.History, part.Name, degrees);

    public void Undo() => ActiveDocument.Undo();

    public void Redo() => ActiveDocument.Redo();

    public int IndexOf(Part part)
    {
        for (int i = 0; i < Character.Parts.Count; i++)
            if (Character.Parts[i] == part)
                return i;
        return -1;
    }

    partial void OnActivePartChanged(Part? oldValue, Part newValue)
    {
        if (ActiveDocument is not null)
        {
            ActiveDocument.EndStroke();
            ActiveDocument.PixelsChanged -= OnContentChanged;
        }
        ActiveDocument = Character.CreateDocument(newValue);
        ActiveDocument.PixelsChanged += OnContentChanged;
        Refresh();
    }

    /// <summary>Picks a drawing tool; always leaves pose mode, even when the tool is already selected.</summary>
    public void SelectTool(ToolItem tool)
    {
        CurrentTool = tool;
        PoseMode = false;
    }

    partial void OnCurrentToolChanged(ToolItem value) => PoseMode = false;

    partial void OnPoseModeChanged(bool value) => Refresh();

    partial void OnDimOtherPartsChanged(bool value) => Refresh();

    private void OnHistoryChanged(object? sender, EventArgs e)
    {
        Refresh(); // undo/redo may have changed any part's pixels
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnContentChanged(object? sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        if (Character is null || ActivePart is null)
            return;
        Transforms = Character.ComputeTransforms();
        Composite = _compositor.Compose(Character);

        int active = IndexOf(ActivePart);
        bool dim = DimOtherParts && !PoseMode;
        for (int i = 0; i < Composite.Indices.Length; i++)
        {
            var c = Character.Palette[Composite.Indices[i]];
            _previewPixels[i] = c.ToBgra32();
            short owner = Composite.Owners[i];
            _canvasPixels[i] = dim && owner != active && owner != CompositeResult.NoPart
                ? (c with { A = DimAlpha }).ToBgra32()
                : _previewPixels[i];
        }
        Copy(_canvasPixels, CanvasBitmap);
        Copy(_previewPixels, PreviewBitmap);
        ImageUpdated?.Invoke(this, EventArgs.Empty);
    }

    private static WriteableBitmap CreateBitmap(PixelSize size) =>
        new(size, new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);

    private static unsafe void Copy(uint[] pixels, WriteableBitmap bitmap)
    {
        using var fb = bitmap.Lock();
        int width = fb.Size.Width;
        fixed (uint* src = pixels)
        {
            for (int y = 0; y < fb.Size.Height; y++)
                Buffer.MemoryCopy(src + y * width, (byte*)fb.Address + y * fb.RowBytes, fb.RowBytes, width * 4);
        }
    }
}
