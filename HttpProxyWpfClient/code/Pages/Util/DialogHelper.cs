using System.Windows;

namespace HttpProxyWpfClient.code.Pages.Util;

public class DialogHelper
{
    /// <summary>
    /// 弹窗（支持异步等待关闭）
    /// </summary>
    /// <typeparam name="TResult">返回结果的类型</typeparam>
    /// <param name="title">窗口标题</param>
    /// <param name="content">展示的内容（UserControl、Panel、Button 等）</param>
    /// <param name="isModal">是否模态</param>
    /// <param name="width">宽度</param>
    /// <param name="height">高度</param>
    /// <param name="onOpen">窗口打开时的回调</param>
    /// <param name="onClose">窗口关闭时的回调</param>
    /// <returns>Task 异步返回值</returns>
    public static Task<TResult?> ShowDialogAsync<TResult>(
        string title,
        UIElement content,
        bool isModal = true,
        double width = 400,
        double height = 300,
        Action<Window>? onOpen = null,
        Action<Window>? onClose = null)
    {
        var tcs = new TaskCompletionSource<TResult?>();

        var window = new Window
        {
            Title = title,
            Content = content,
            Width = width,
            Height = height,
            WindowStartupLocation = WindowStartupLocation.CenterScreen
        };

        // 打开时回调
        window.Loaded += (s, e) => onOpen?.Invoke(window);

        // 关闭时回调
        window.Closed += (s, e) =>
        {
            onClose?.Invoke(window);
            if (!tcs.Task.IsCompleted)
                tcs.SetResult(default); // 如果没手动传值，返回默认
        };

        // 提供一个给外部设置返回值的方法
        window.Tag = tcs;

        if (isModal)
            window.ShowDialog();
        else
            window.Show();

        return tcs.Task;
    }

    /// <summary>
    /// 设置窗口返回值并关闭
    /// </summary>
    public static void CloseWithResult<TResult>(Window window, TResult result)
    {
        if (window.Tag is TaskCompletionSource<TResult?> tcs && !tcs.Task.IsCompleted)
        {
            tcs.SetResult(result);
        }
        window.Close();
    }
}