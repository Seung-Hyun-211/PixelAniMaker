using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using PixelAniMaker.App.Services;

namespace PixelAniMaker.App.Views;

/// <summary>
/// Lists every shortcut. "변경" waits for the next key combination (Esc cancels, Delete removes the
/// shortcut); a key already used by another action moves to this one.
/// </summary>
public sealed class ShortcutsWindow : Window
{
    private readonly ShortcutMap _shortcuts;
    private readonly Dictionary<string, TextBlock> _keyTexts = [];
    private readonly TextBlock _message = new() { Foreground = new SolidColorBrush(Color.FromRgb(224, 176, 96)), TextWrapping = TextWrapping.Wrap };
    private string? _capturing;

    public ShortcutsWindow(ShortcutMap shortcuts)
    {
        _shortcuts = shortcuts;
        Title = "단축키 설정";
        Width = 560;
        Height = 680;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var rows = new StackPanel { Spacing = 2 };
        foreach (var group in ShortcutCatalog.All.GroupBy(d => d.Category))
        {
            rows.Children.Add(new TextBlock { Text = group.Key, FontWeight = FontWeight.Bold, Margin = new Thickness(0, 10, 0, 4) });
            foreach (var d in group)
                rows.Children.Add(Row(d));
        }

        var resetAll = new Button { Content = "모두 기본값으로" };
        resetAll.Click += (_, _) =>
        {
            _shortcuts.ResetAll();
            _message.Text = "모든 단축키를 기본값으로 되돌렸습니다.";
            Refresh();
        };
        var close = new Button { Content = "닫기", MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
        close.Click += (_, _) => Close();

        var bottom = new DockPanel { Margin = new Thickness(0, 10, 0, 0) };
        DockPanel.SetDock(close, Avalonia.Controls.Dock.Right);
        bottom.Children.Add(close);
        bottom.Children.Add(resetAll);

        var root = new DockPanel { Margin = new Thickness(16) };
        DockPanel.SetDock(bottom, Avalonia.Controls.Dock.Bottom);
        DockPanel.SetDock(_message, Avalonia.Controls.Dock.Bottom);
        root.Children.Add(bottom);
        root.Children.Add(_message);
        root.Children.Add(new ScrollViewer { Content = rows });
        Content = root;
        Refresh();
    }

    private Control Row(ShortcutDefinition d)
    {
        var key = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Width = 140 };
        _keyTexts[d.Id] = key;

        var change = new Button { Content = "변경" };
        change.Click += (_, _) =>
        {
            _capturing = d.Id;
            _message.Text = $"'{d.Label}'에 쓸 키를 누르세요. (Esc 취소, Delete 단축키 없음)";
            Refresh();
            Focus();
        };
        var reset = new Button { Content = "기본값" };
        reset.Click += (_, _) => Assign(d, string.IsNullOrEmpty(d.DefaultGesture) ? null : KeyGesture.Parse(d.DefaultGesture));

        var row = new DockPanel();
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { key, change, reset } };
        DockPanel.SetDock(buttons, Avalonia.Controls.Dock.Right);
        row.Children.Add(buttons);
        row.Children.Add(new TextBlock { Text = d.Label, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) });
        return row;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (_capturing is null || ShortcutCatalog.Find(_capturing) is not { } d)
        {
            base.OnKeyDown(e);
            return;
        }
        e.Handled = true;
        switch (e.Key)
        {
            case Key.Escape:
                _message.Text = "";
                _capturing = null;
                Refresh();
                return;
            case Key.Delete:
                Assign(d, null);
                return;
            case Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt
                or Key.LWin or Key.RWin:
                return; // wait for the actual key
            default:
                Assign(d, new KeyGesture(e.Key, e.KeyModifiers));
                return;
        }
    }

    private void Assign(ShortcutDefinition d, KeyGesture? gesture)
    {
        var taken = _shortcuts.Set(d.Id, gesture);
        _message.Text = taken is null
            ? $"'{d.Label}': {(gesture is null ? "단축키 없음" : ShortcutMap.Display(gesture))}"
            : $"'{d.Label}'에 {ShortcutMap.Display(gesture)}를 지정했습니다. '{taken}'의 단축키는 비워졌습니다.";
        _capturing = null;
        Refresh();
    }

    private void Refresh()
    {
        foreach (var (id, text) in _keyTexts)
        {
            text.Text = id == _capturing ? "키를 누르세요…" : _shortcuts.Texts.GetValueOrDefault(id) is { Length: > 0 } k ? k : "—";
            text.Foreground = id == _capturing ? new SolidColorBrush(Color.FromRgb(77, 163, 255)) : Brushes.White;
        }
    }
}
