using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using HttpProxyWpfClient.code.net.util;
using HttpProxyWpfClient.code.Pages.Util;

namespace HttpProxyWpfClient.code.Pages;

public partial class Main
{
    private void ShowColumnVisibilityMenu()
    {
        var menu = new ContextMenu();
        foreach (var column in _sessionColumns)
        {
            var item = new MenuItem
            {
                Header = column.Column.Header?.ToString() ?? column.Key,
                IsCheckable = true,
                IsChecked = column.Visible,
                StaysOpenOnClick = true
            };
            item.Click += (_, _) =>
            {
                if (!item.IsChecked && _sessionColumns.Count(c => c.Visible) <= 1)
                {
                    item.IsChecked = true;
                    return;
                }
                column.Visible = item.IsChecked;
                ApplySessionColumnLayout();
                SaveConfig();
            };
            menu.Items.Add(item);
        }
        menu.Placement = PlacementMode.MousePoint;
        menu.IsOpen = true;
    }

    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child != null)
        {
            if (child is T match) return match;
            child = VisualTreeHelper.GetParent(child);
        }
        return null;
    }

    private void CopyCurlCmd_OnClick(object sender, RoutedEventArgs e) => CopyCurl(CurlShellType.Cmd);
    private void CopyCurlBash_OnClick(object sender, RoutedEventArgs e) => CopyCurl(CurlShellType.Bash);
    private void CopyCurlPowerShell_OnClick(object sender, RoutedEventArgs e) => CopyCurl(CurlShellType.PowerShell);

    private void CopyCurl(CurlShellType shellType)
    {
        if (SelectedSession == null) return;
        Clipboard.SetText(CurlCommandGenerator.Generate(SelectedSession.Session.HttpClient.Request, shellType));
    }

    private void ErrorText_OnClick(object sender, MouseButtonEventArgs e)
    {
        string? errorText = ResponseMessage?.ErrorText;
        if (string.IsNullOrEmpty(errorText)) return;
        var textBox = new TextBox
        {
            Text = errorText,
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = true,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(10),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        DialogHelper.ShowDialogAsync<bool>("璇锋眰鏈畬鎴?", textBox, true, 600D, 400D);
    }
}
