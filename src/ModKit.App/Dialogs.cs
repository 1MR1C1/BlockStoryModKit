using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;

namespace ModKit.App;

internal static class Dialogs
{
    public static Task<bool> ConfirmAsync(Window owner, string title, string message, string okText = "OK")
    {
        var tcs = new TaskCompletionSource<bool>();

        var ok = new Button { Content = okText, MinWidth = 90, IsDefault = false };
        var cancel = new Button { Content = "Cancel", MinWidth = 90, IsCancel = true };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        buttons.Children.Add(cancel);
        buttons.Children.Add(ok);

        var panel = new StackPanel { Margin = new Avalonia.Thickness(16), Spacing = 14 };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        panel.Children.Add(buttons);

        var dlg = new Window
        {
            Title = title,
            Content = panel,
            Width = 400,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };

        ok.Click += (_, _) => { tcs.TrySetResult(true); dlg.Close(); };
        cancel.Click += (_, _) => { tcs.TrySetResult(false); dlg.Close(); };
        dlg.Closed += (_, _) => tcs.TrySetResult(false);

        dlg.ShowDialog(owner);
        return tcs.Task;
    }
}
