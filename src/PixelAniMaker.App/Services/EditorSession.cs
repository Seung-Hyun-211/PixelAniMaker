using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using PixelAniMaker.Core.Editing;
using PixelAniMaker.Core.Imaging;

namespace PixelAniMaker.App.Services;

/// <summary>
/// State shared by every panel: the open document, its display bitmap, the current tool and view options.
/// </summary>
public sealed partial class EditorSession : ObservableObject
{
    public static readonly int[] ZoomSteps = [1, 2, 3, 4, 6, 8, 12, 16, 24, 32, 48];

    private uint[] _scratch = [];

    [ObservableProperty] private EditorDocument _document = null!;
    [ObservableProperty] private WriteableBitmap _bitmap = null!;
    [ObservableProperty] private ToolItem _currentTool = ToolCatalog.Pencil;
    [ObservableProperty] private int _zoom = 4;
    [ObservableProperty] private bool _showGrid = true;
    [ObservableProperty] private bool _showTemplate = true;
    [ObservableProperty] private string _cursorText = "";

    public EditorSession()
    {
        Template = TemplateLoader.LoadFront64x128();
        NewDocument(64, 128);
    }

    /// <summary>Semi-transparent guide drawn behind the canvas (the 4-head mannequin).</summary>
    public Bitmap? Template { get; }

    /// <summary>Raised after the display bitmap has been refreshed.</summary>
    public event EventHandler? ImageUpdated;

    /// <summary>Raised when the undo history or dirty state changes.</summary>
    public event EventHandler? HistoryChanged;

    public void NewDocument(int width, int height)
    {
        if (Document is not null)
        {
            Document.PixelsChanged -= OnPixelsChanged;
            Document.Palette.Changed -= OnPixelsChanged;
            Document.History.Changed -= OnHistoryChanged;
        }

        var doc = EditorDocument.CreateBlank(width, height);
        TemplateLoader.SeedPalette(doc.Palette);
        doc.PixelsChanged += OnPixelsChanged;
        doc.Palette.Changed += OnPixelsChanged; // replacing a colour recolours its pixels
        doc.History.Changed += OnHistoryChanged;

        Bitmap = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96),
            PixelFormat.Bgra8888, AlphaFormat.Unpremul);
        _scratch = new uint[width * height];
        Document = doc;
        RefreshBitmap();
        OnHistoryChanged(this, EventArgs.Empty);
    }

    public void ZoomBy(int steps)
    {
        int i = Array.IndexOf(ZoomSteps, Zoom);
        if (i < 0)
            i = Array.FindLastIndex(ZoomSteps, z => z <= Zoom);
        Zoom = ZoomSteps[Math.Clamp(i + steps, 0, ZoomSteps.Length - 1)];
    }

    private void OnPixelsChanged(object? sender, EventArgs e) => RefreshBitmap();

    private void OnHistoryChanged(object? sender, EventArgs e) => HistoryChanged?.Invoke(this, EventArgs.Empty);

    private unsafe void RefreshBitmap()
    {
        var image = Document.Image;
        image.ToBgra32(Document.Palette, _scratch);
        using (var fb = Bitmap.Lock())
        {
            fixed (uint* src = _scratch)
            {
                for (int y = 0; y < image.Height; y++)
                {
                    Buffer.MemoryCopy(src + y * image.Width, (byte*)fb.Address + y * fb.RowBytes,
                        fb.RowBytes, image.Width * 4);
                }
            }
        }
        ImageUpdated?.Invoke(this, EventArgs.Empty);
    }
}
