using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using net8._0_ProxyWPF.code.Pages;
using net8._0_ProxyWPF.code.Pages.Setting;
using Wpf.Ui.Controls;

namespace net8._0_ProxyWPF
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


        public MainWindow()
        {
            InitializeComponent();
            // 加载主页
            RootFrame.Navigate(pages["Main"]);
            Closing += MainWindow_Closing;
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (pages["Main"] is Main mainPage)
            {
                mainPage.ShutdownProxy();
            }
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
                MaxRestoreButton.ToolTip = "还原";
            }
            else
            {
                WindowState = WindowState.Normal;
                MaxIcon.Visibility = Visibility.Visible;
                RestoreIcon.Visibility = Visibility.Collapsed;
                MaxRestoreButton.ToolTip = "最大化";
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

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // LocalizationManager.SetLanguage("ja-JP");
        }
    }
}