using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace PixelAniMaker.App.Views;

/// <summary>A small modal message box with custom buttons; returns the index of the clicked button (-1 if closed).</summary>
public sealed class MessageDialog : Window
{
    private int _result = -1;

    private MessageDialog(string title, string message, Control? extra, IReadOnlyList<string> buttons)
    {
        Title = title;
        SizeToContent = SizeToContent.WidthAndHeight;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        MinWidth = 360;

        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        for (int i = 0; i < buttons.Count; i++)
        {
            int index = i;
            var button = new Button { Content = buttons[i], MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
            button.Click += (_, _) =>
            {
                _result = index;
                Close();
            };
            buttonRow.Children.Add(button);
        }

        var body = new StackPanel { Margin = new Thickness(20), Spacing = 20 };
        body.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, MaxWidth = 460, HorizontalAlignment = HorizontalAlignment.Left });
        if (extra is not null)
            body.Children.Add(extra);
        body.Children.Add(buttonRow);
        Content = body;
    }

    public static Task<int> ShowAsync(Window owner, string title, string message, params string[] buttons) =>
        ShowAsync(owner, title, message, null, buttons);

    /// <param name="extra">Shown between the message and the buttons (e.g. preview images).</param>
    public static async Task<int> ShowAsync(Window owner, string title, string message, Control? extra, params string[] buttons)
    {
        var dialog = new MessageDialog(title, message, extra, buttons);
        await dialog.ShowDialog(owner);
        return dialog._result;
    }
}
