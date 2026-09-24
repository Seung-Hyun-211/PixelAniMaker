using System.ComponentModel;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using PixelAniMaker.Core.Rigging;

namespace PixelAniMaker.App.Services;

/// <summary>A reference picture placed on the canvas (canvas pixels; scale in percent).</summary>
public sealed partial class ReferenceImage(Bitmap bitmap, decimal scale) : ObservableObject
{
    public Bitmap Bitmap { get; } = bitmap;

    [ObservableProperty] private decimal _x;
    [ObservableProperty] private decimal _y;
    [ObservableProperty] private decimal _scale = scale;
}

/// <summary>
/// Semi-transparent reference pictures for drawing over (one per direction, as the view is shown).
/// A working aid only: not saved with the project and never part of exported frames.
/// </summary>
public sealed partial class ReferenceLayer : ObservableObject
{
    private readonly Dictionary<Direction, ReferenceImage> _images = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Current), nameof(HasImage))]
    private Direction _direction;

    [ObservableProperty] private double _opacity = 0.4;
    [ObservableProperty] private bool _visible = true;

    /// <summary>Draw over the character instead of under it.</summary>
    [ObservableProperty] private bool _onTop;

    public ReferenceImage? Current => _images.GetValueOrDefault(Direction);

    public bool HasImage => Current is not null;

    /// <summary>Raised when anything that changes how the reference is drawn changes.</summary>
    public event EventHandler? Changed;

    /// <summary>Loads a picture for the current direction, scaled to fit the canvas and centred.</summary>
    public void Load(string path, int canvasWidth, int canvasHeight)
    {
        Bitmap bitmap;
        try
        {
            bitmap = new Bitmap(path);
        }
        catch (Exception ex) when (ex is not IOException and not UnauthorizedAccessException)
        {
            throw new InvalidDataException("이미지를 읽을 수 없습니다.", ex); // decoder errors vary by platform
        }
        var size = bitmap.PixelSize;
        double fit = Math.Min((double)canvasWidth / size.Width, (double)canvasHeight / size.Height);
        var image = new ReferenceImage(bitmap, Math.Round((decimal)fit * 100, 1))
        {
            X = Math.Round((decimal)(canvasWidth - size.Width * fit) / 2),
            Y = Math.Round((decimal)(canvasHeight - size.Height * fit) / 2),
        };
        Clear();
        image.PropertyChanged += OnImageChanged;
        _images[Direction] = image;
        Visible = true;
        NotifyCurrentChanged();
    }

    public void Clear()
    {
        if (!_images.Remove(Direction, out var old))
            return;
        old.PropertyChanged -= OnImageChanged;
        old.Bitmap.Dispose();
        NotifyCurrentChanged();
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void NotifyCurrentChanged()
    {
        OnPropertyChanged(nameof(Current));
        OnPropertyChanged(nameof(HasImage));
    }

    private void OnImageChanged(object? sender, PropertyChangedEventArgs e) => Changed?.Invoke(this, EventArgs.Empty);
}
