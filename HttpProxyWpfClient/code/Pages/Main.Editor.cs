using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HttpProxyWpfClient.code.Loc;
using HttpProxyWpfClient.code.net.entity;
using HttpProxyWpfClient.code.net.util;
using HttpProxyWpfClient.code.Pages.Util;
using Titanium.Web.Proxy.Http;

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
        DialogHelper.ShowDialogAsync<bool>(LocalizationManager.GetString("RequestIncomplete"), textBox, true, 600D, 400D);
    }

    /// <summary>
    /// 预览选中会话的响应图片。body 在代理 BeforeResponse 阶段已解压缓存（KeepBody），
    /// 拦截中的会话被 Monitor.Wait 冻结、已完成会话 body 不再变更，UI 线程直接读取无并发冲突。
    /// </summary>
    private void PreviewImage_OnClick(object sender, RoutedEventArgs e)
    {
        RequestVo session = SelectedSession;
        if (session == null)
        {
            return;
        }

        Response response = session.Session.HttpClient.Response;
        if (response == null || response.Body is not { Length: > 0 } body)
        {
            return;
        }

        ShowImagePreviewDialog(session, body);
    }

    /// <summary>
    /// 响应阶段拦截中的会话允许用本地图片替换响应体。替换后不自动放行，仍由用户点击"放行"释放拦截。
    /// </summary>
    private void ReplaceImage_OnClick(object sender, RoutedEventArgs e)
    {
        RequestVo session = SelectedSession;
        if (session == null || !session.Blocking)
        {
            return;
        }

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = LocalizationManager.GetString("ReplaceImage"),
            Filter = "PNG|*.png|JPEG|*.jpg;*.jpeg|GIF|*.gif|BMP|*.bmp|TIFF|*.tiff|"
                     + LocalizationManager.GetString("AllSupportedImageFormats") + "|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.tiff"
        };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true)
        {
            return;
        }

        try
        {
            byte[] imageBytes = File.ReadAllBytes(dialog.FileName);
            string? mimeType = GetImageMimeType(Path.GetExtension(dialog.FileName));
            if (mimeType == null || TryCreateBitmap(imageBytes) == null)
            {
                ShowImageError(LocalizationManager.GetString("ImageDecodeFailed"));
                return;
            }

            // 先解码校验文件确实是可用图片，避免把损坏数据写进响应后无法恢复
            session.Session.SetResponseBody(imageBytes);
            Response response = session.Session.HttpClient.Response;
            response.Headers.RemoveHeader("Content-Type");
            response.Headers.AddHeader("Content-Type", mimeType);
            session.ResponseContentType = mimeType;
            session.ResponseLength = imageBytes.Length;
        }
        catch (Exception ex)
        {
            Debug.WriteLine("替换响应图片失败: " + ex);
            ShowImageError(LocalizationManager.GetString("ImageReplaceFailed") + ex.Message);
        }
    }

    /// <summary>
    /// 弹窗展示响应图片，底部显示类型、大小和像素尺寸。解码失败时提示。
    /// </summary>
    private void ShowImagePreviewDialog(RequestVo session, byte[] body)
    {
        BitmapImage? bitmap = TryCreateBitmap(body);
        if (bitmap == null)
        {
            ShowImageError(LocalizationManager.GetString("ImageDecodeFailed"));
            return;
        }

        Window dialog = new Window
        {
            Title = LocalizationManager.GetString("PreviewImage"),
            Width = 800,
            Height = 600,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Window.GetWindow(this),
            ResizeMode = ResizeMode.CanResize,
            MinWidth = 320,
            MinHeight = 240
        };

        Grid grid = new Grid { Margin = new Thickness(10) };
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var viewer = new ScrollViewer
        {
            Content = new Image
            {
                Source = bitmap,
                Stretch = Stretch.Uniform,
                StretchDirection = StretchDirection.Both
            },
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        Grid.SetRow(viewer, 0);
        grid.Children.Add(viewer);

        TextBlock info = new TextBlock
        {
            Text = $"{session.ResponseContentType}   {body.Length:N0} B   {bitmap.PixelWidth} x {bitmap.PixelHeight}",
            Margin = new Thickness(0, 6, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        Grid.SetRow(info, 1);
        grid.Children.Add(info);

        dialog.Content = grid;
        dialog.ShowDialog();
    }

    /// <summary>
    /// 从字节解码图片，失败返回 null。Freeze 后可跨线程使用。
    /// </summary>
    private static BitmapImage? TryCreateBitmap(byte[] body)
    {
        try
        {
            var bitmap = new BitmapImage();
            using (var stream = new MemoryStream(body))
            {
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
            }
            bitmap.Freeze();
            return bitmap.PixelWidth > 0 ? bitmap : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? GetImageMimeType(string extension) => extension.ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".bmp" => "image/bmp",
        ".tiff" or ".tif" => "image/tiff",
        _ => null
    };

    private static void ShowImageError(string message)
    {
        MessageBox.Show(message, LocalizationManager.GetString("PreviewImage"));
    }
}
