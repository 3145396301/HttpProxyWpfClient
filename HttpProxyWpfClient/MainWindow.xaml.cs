using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HttpProxyWpfClient.code.Loc;
using HttpProxyWpfClient.code.Pages;
using HttpProxyWpfClient.code.Pages.Setting;
using Wpf.Ui.Controls;

namespace HttpProxyWpfClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : INotifyPropertyChanged
    {


        public static Dictionary<string, System.Windows.Controls.Page> pages =
            new Dictionary<string, System.Windows.Controls.Page>()
            {
                { "Main", new Main() },
                { "Setting", new SettingPage() }
            };


        private bool _shutdownCompleted;

        public MainWindow()
        {
            InitializeComponent();
            // 加载主页
            RootFrame.Navigate(pages["Main"]);
            Closing += MainWindow_Closing;
        }

        private async void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_shutdownCompleted)
            {
                // 已完成关闭前的清理，本次是我们自己触发的真正关闭，直接放行
                return;
            }

            // ShutdownProxy 内部会调用 Titanium 的阻塞 Stop()，而该调用需要等待仍卡在
            // Monitor.Wait 的会话处理线程通过 Dispatcher.Invoke 回到 UI 线程更新界面。
            // 若在此处同步调用，UI 线程会一直停留在本方法内，无法处理那些 Dispatcher.Invoke，
            // 从而与后台线程互相等待形成死锁，导致关闭窗口时 UI 卡死。
            // 因此先取消本次关闭，把清理工作放到后台线程执行，UI 线程保持消息循环畅通，
            // 待清理完成后再真正关闭窗口。
            e.Cancel = true;

            if (pages["Main"] is Main mainPage)
            {
                // 关闭前保存配置（含会话列表列显隐/列宽、字体大小等），保证拖拽调整在下次启动时恢复
                mainPage.SaveConfig();
                await System.Threading.Tasks.Task.Run(() => mainPage.ShutdownProxy());
            }

            _shutdownCompleted = true;
            Close();
        }

        // 标题栏鼠标按下：单击拖动，双击最大化/还原
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                ToggleMaximizeRestore();
            }
            else
            {
                // 调用内置拖拽
                try
                {
                    DragMove();
                }
                catch
                {
                    /* 拖动中抛异常可忽略 */
                }
            }
        }
        private void Minimize_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState.Minimized;

        private void MaximizeRestore_Click(object sender, RoutedEventArgs e)
            => ToggleMaximizeRestore();

        private void Close_Click(object sender, RoutedEventArgs e)
            => Close();

        private void ToggleMaximizeRestore()
        {
            if (WindowState == WindowState.Normal)
            {
                WindowState = WindowState.Maximized;
                MaxIcon.Visibility = Visibility.Collapsed;
                RestoreIcon.Visibility = Visibility.Visible;
                MaxRestoreButton.ToolTip = LocalizationManager.GetString("Restore");
            }
            else
            {
                WindowState = WindowState.Normal;
                MaxIcon.Visibility = Visibility.Visible;
                RestoreIcon.Visibility = Visibility.Collapsed;
                MaxRestoreButton.ToolTip = LocalizationManager.GetString("Maximize");
            }
        }



        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private void SelectionChanged(object sender, RoutedEventArgs e)
        {
            NavigationViewItem item = sender as NavigationViewItem;
            if (item != null)
            {
                string tag = item.Tag.ToString();
                if (pages.ContainsKey(tag))
                {
                    RootFrame.Navigate(pages[tag]);
                }
            }
        }
    }
}