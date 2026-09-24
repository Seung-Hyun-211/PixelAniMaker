using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PixelAniMaker.App.Services;

/// <summary>A remappable action and its default key.</summary>
public sealed record ShortcutDefinition(string Id, string Category, string Label, string DefaultGesture);

/// <summary>Every keyboard shortcut of the app — the single place where default keys are defined.</summary>
public static class ShortcutCatalog
{
    public static IReadOnlyList<ShortcutDefinition> All { get; } =
    [
        new("New", "파일", "새로 만들기", "Ctrl+N"),
        new("Open", "파일", "열기", "Ctrl+O"),
        new("Save", "파일", "저장", "Ctrl+S"),
        new("SaveAs", "파일", "다른 이름으로 저장", "Ctrl+Shift+S"),
        new("ExportSheet", "파일", "현재 동작 시트 내보내기", "Ctrl+E"),
        new("Undo", "편집", "되돌리기", "Ctrl+Z"),
        new("Redo", "편집", "다시 하기", "Ctrl+Y"),
        new("RedoAlt", "편집", "다시 하기 (보조)", "Ctrl+Shift+Z"),
        new("Tool.Pencil", "도구", "연필", "B"),
        new("Tool.Eraser", "도구", "지우개", "E"),
        new("Tool.Fill", "도구", "채우기", "G"),
        new("Tool.Eyedropper", "도구", "스포이드", "I"),
        new("Tool.Line", "도구", "선", "L"),
        new("Tool.Rectangle", "도구", "사각형", "U"),
        new("Tool.Ellipse", "도구", "원", "Shift+U"),
        new("Tool.Select", "도구", "선택·이동", "M"),
        new("ToggleSymmetry", "도구", "대칭 그리기", "S"),
        new("DeleteSelection", "편집", "선택 영역 지우기", "Delete"),
        new("Deselect", "편집", "선택 해제", "Escape"),
        new("Direction.Front", "방향", "정면", "D1"),
        new("Direction.Left", "방향", "좌측면", "D2"),
        new("Direction.Right", "방향", "우측면", "D3"),
        new("Direction.Back", "방향", "후면", "D4"),
        new("ToggleGrid", "보기", "픽셀 격자", "Ctrl+OemQuotes"),
        new("ToggleDim", "보기", "다른 파츠 흐리게", "H"),
        new("TogglePose", "보기", "포즈 모드", "P"),
        new("ZoomIn", "보기", "확대", "Ctrl+OemPlus"),
        new("ZoomOut", "보기", "축소", "Ctrl+OemMinus"),
        new("SaveKey", "애니메이션", "키 저장", "K"),
        new("PreviousFrame", "애니메이션", "이전 프레임", "OemComma"),
        new("NextFrame", "애니메이션", "다음 프레임", "OemPeriod"),
        new("ToggleOnion", "애니메이션", "어니언 스킨", "O"),
        new("ToggleTouchup", "애니메이션", "프레임 손보기", "F"),
    ];

    public static ShortcutDefinition? Find(string id) => All.FirstOrDefault(d => d.Id == id);
}

/// <summary>
/// The shortcuts in effect: defaults plus the user's changes. <see cref="Gestures"/> and
/// <see cref="Texts"/> are replaced on every change so bindings like <c>Shortcuts.Texts[Save]</c> refresh.
/// </summary>
public sealed partial class ShortcutMap : ObservableObject
{
    private readonly Dictionary<string, KeyGesture?> _current = [];

    [ObservableProperty] private IReadOnlyDictionary<string, KeyGesture?> _gestures = new Dictionary<string, KeyGesture?>();
    [ObservableProperty] private IReadOnlyDictionary<string, string> _texts = new Dictionary<string, string>();

    /// <param name="overrides">Saved changes: id → gesture text ("" = no shortcut).</param>
    public ShortcutMap(IReadOnlyDictionary<string, string>? overrides = null)
    {
        foreach (var d in ShortcutCatalog.All)
            _current[d.Id] = Parse(overrides?.GetValueOrDefault(d.Id) ?? d.DefaultGesture);
        Publish();
    }

    public KeyGesture? Get(string id) => _current.GetValueOrDefault(id);

    /// <summary>Assigns a gesture (null = none). Another action using the same gesture loses it; its label is returned.</summary>
    public string? Set(string id, KeyGesture? gesture)
    {
        string? taken = null;
        if (gesture is not null)
        {
            var other = _current.FirstOrDefault(kv => kv.Key != id && Equals(kv.Value, gesture));
            if (other.Key is not null)
            {
                _current[other.Key] = null;
                taken = ShortcutCatalog.Find(other.Key)?.Label;
            }
        }
        _current[id] = gesture;
        Publish();
        return taken;
    }

    public void ResetAll()
    {
        foreach (var d in ShortcutCatalog.All)
            _current[d.Id] = Parse(d.DefaultGesture);
        Publish();
    }

    /// <summary>Only the entries that differ from the defaults, for settings.json.</summary>
    public Dictionary<string, string> Overrides() => ShortcutCatalog.All
        .Where(d => !Equals(_current[d.Id], Parse(d.DefaultGesture)))
        .ToDictionary(d => d.Id, d => _current[d.Id]?.ToString() ?? "");

    public static string Display(KeyGesture? gesture) => gesture is null ? "" : gesture.ToString()
        .Replace("OemQuotes", "'").Replace("OemPlus", "=").Replace("OemMinus", "-")
        .Replace("OemComma", ",").Replace("OemPeriod", ".")
        .Replace("D1", "1").Replace("D2", "2").Replace("D3", "3").Replace("D4", "4");

    private static KeyGesture? Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        try
        {
            return KeyGesture.Parse(text);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private void Publish()
    {
        Gestures = new Dictionary<string, KeyGesture?>(_current);
        Texts = _current.ToDictionary(kv => kv.Key, kv => Display(kv.Value));
    }
}
