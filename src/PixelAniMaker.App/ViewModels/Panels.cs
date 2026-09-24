using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;
using PixelAniMaker.App.Services;
using PixelAniMaker.Core.Editing;
using PixelAniMaker.Core.Imaging;

namespace PixelAniMaker.App.ViewModels;

/// <summary>Editing canvas (document area).</summary>
public sealed class CanvasDocumentViewModel : Document
{
    public CanvasDocumentViewModel(EditorSession session)
    {
        Session = session;
        Id = "Canvas";
        Title = "캔버스";
        CanClose = false;
        CanFloat = false;
    }

    public EditorSession Session { get; }
}

/// <summary>Tool selection and view toggles.</summary>
public sealed class ToolboxViewModel : Tool
{
    public ToolboxViewModel(EditorSession session)
    {
        Session = session;
        Id = "Toolbox";
        Title = "도구";
        CanClose = false;
    }

    public EditorSession Session { get; }

    public IReadOnlyList<ToolItem> Tools => ToolCatalog.All;
}

public sealed partial class SwatchViewModel : ObservableObject
{
    public SwatchViewModel(int index, Rgba color)
    {
        Index = index;
        Update(color);
    }

    public int Index { get; }

    [ObservableProperty] private IBrush _brush = Brushes.Transparent;
    [ObservableProperty] private string _hex = "";
    [ObservableProperty] private bool _isPrimary;
    [ObservableProperty] private bool _isSecondary;

    public bool IsTransparent => Index == Palette.TransparentIndex;

    public void Update(Rgba c)
    {
        Brush = new SolidColorBrush(Color.FromArgb(c.A, c.R, c.G, c.B));
        Hex = IsTransparent ? "투명" : c.ToString();
    }
}

/// <summary>Palette: pick primary (left click) / secondary (right click), add and edit colours.</summary>
public sealed partial class PaletteViewModel : Tool
{
    private Palette? _palette;
    private EditorDocument? _document;

    [ObservableProperty] private string _hexInput = "#000000";
    [ObservableProperty] private string _message = "";
    [ObservableProperty] private SwatchViewModel? _primary;
    [ObservableProperty] private SwatchViewModel? _secondary;

    public PaletteViewModel(EditorSession session)
    {
        Session = session;
        Id = "Palette";
        Title = "팔레트";
        CanClose = false;
        session.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(EditorSession.Document))
                Attach();
        };
        Attach();
    }

    public EditorSession Session { get; }

    public ObservableCollection<SwatchViewModel> Swatches { get; } = [];

    public void Select(SwatchViewModel swatch, bool secondary)
    {
        if (_document is null)
            return;
        if (secondary)
            _document.SecondaryIndex = swatch.Index;
        else
            _document.PrimaryIndex = swatch.Index;
    }

    [RelayCommand]
    private void AddColor()
    {
        if (TryReadInput(out var color))
            _document!.PrimaryIndex = _document.Palette.GetOrAdd(color);
    }

    /// <summary>Replaces the primary colour; every pixel using it changes too.</summary>
    [RelayCommand]
    private void ReplacePrimary()
    {
        if (!TryReadInput(out var color))
            return;
        if (_document!.PrimaryIndex == Palette.TransparentIndex)
            Message = "투명은 바꿀 수 없습니다";
        else if (_document.Palette.IndexOf(color) >= 0)
            Message = "이미 팔레트에 있는 색입니다";
        else
            _document.Palette.Set(_document.PrimaryIndex, color);
    }

    private void Attach()
    {
        if (_palette is not null)
            _palette.Changed -= OnPaletteChanged;
        if (_document is not null)
            _document.ColorSelectionChanged -= OnSelectionChanged;

        _document = Session.Document;
        _palette = _document.Palette;
        _palette.Changed += OnPaletteChanged;
        _document.ColorSelectionChanged += OnSelectionChanged;
        Rebuild();
    }

    private void OnPaletteChanged(object? sender, EventArgs e) => Rebuild();

    private void OnSelectionChanged(object? sender, EventArgs e) => UpdateSelection();

    private void Rebuild()
    {
        if (_palette is null)
            return;
        for (int i = 0; i < _palette.Count; i++)
        {
            if (i < Swatches.Count)
                Swatches[i].Update(_palette[i]);
            else
                Swatches.Add(new SwatchViewModel(i, _palette[i]));
        }
        while (Swatches.Count > _palette.Count)
            Swatches.RemoveAt(Swatches.Count - 1);
        UpdateSelection();
    }

    private void UpdateSelection()
    {
        if (_document is null)
            return;
        foreach (var s in Swatches)
        {
            s.IsPrimary = s.Index == _document.PrimaryIndex;
            s.IsSecondary = s.Index == _document.SecondaryIndex;
        }
        Primary = Swatches.ElementAtOrDefault(_document.PrimaryIndex);
        Secondary = Swatches.ElementAtOrDefault(_document.SecondaryIndex);
        if (Primary is { IsTransparent: false })
            HexInput = Primary.Hex;
    }

    /// <summary>Parses the hex input box; shows a message and returns false when invalid.</summary>
    private bool TryReadInput(out Rgba color)
    {
        color = default;
        bool ok = _document is not null && Rgba.TryParseHex(HexInput, out color);
        Message = ok ? "" : "#RRGGBB 형식으로 입력하세요";
        return ok;
    }
}

/// <summary>Actual-size previews of the current image.</summary>
public sealed class PreviewViewModel : Tool
{
    public PreviewViewModel(EditorSession session)
    {
        Session = session;
        Id = "Preview";
        Title = "미리보기";
        CanClose = false;
    }

    public EditorSession Session { get; }
}
