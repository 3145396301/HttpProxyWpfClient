using System.Windows;
using System.Windows.Controls;
using HttpProxyWpfClient.code.Loc;
using HttpProxyWpfClient.code.net;

namespace HttpProxyWpfClient.code.Pages.Setting;

public partial class SettingPage : Page
{
    public SettingPage()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 每次导航显示设置页时，用当前生效的代理配置回填输入框，
    /// 避免一直显示 XAML 中写死的初始值（如 UpstreamEnabledToggle 硬编码为开启）
    /// </summary>
    private void SettingPage_OnLoaded(object sender, RoutedEventArgs e)
    {
        Main page = MainWindow.pages["Main"] as Main;
        ProxyConnect proxyConnect = page?.ProxyConnect;
        if (proxyConnect == null) return;

        LocalProxyAddressTextBox.Text = proxyConnect.ProxyHost;
        LocalProxyPortTextBox.Text = proxyConnect.ProxyPort.ToString();
        UpstreamEnabledToggle.IsChecked = proxyConnect.UpstreamEnabled;
        UpstreamProxyAddressTextBox.Text = proxyConnect.UpstreamIp ?? "";
        UpstreamProxyPortTextBox.Text = proxyConnect.UpstreamPort == -1 ? "" : proxyConnect.UpstreamPort.ToString();

        BackfillLanguageSelection(page);
    }

    /// <summary>
    /// 按持久化的语言配置回填下拉框。回填触发的 SelectionChanged 里通过 _applyingLanguage
    /// 区分程序赋值与用户选择，避免回填时重复写配置/重设语言
    /// </summary>
    private bool _applyingLanguage;

    private void BackfillLanguageSelection(Main? page)
    {
        string language = page?.GetLanguageSetting() ?? "zh-CN";
        _applyingLanguage = true;
        try
        {
            foreach (ComboBoxItem item in LanguageComboBox.Items)
            {
                if ((string)item.Tag == language)
                {
                    LanguageComboBox.SelectedItem = item;
                    break;
                }
            }
        }
        finally
        {
            _applyingLanguage = false;
        }
    }

    private void LanguageComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_applyingLanguage) return;

        if (LanguageComboBox.SelectedItem is ComboBoxItem item && item.Tag is string culture)
        {
            LocalizationManager.SetLanguage(culture);
            (MainWindow.pages["Main"] as Main)?.ApplyLanguageSetting(culture);
        }
    }

    private void RefreshProxyButton_OnClick(object sender, RoutedEventArgs e)
    {
        string localIp = LocalProxyAddressTextBox.Text;
        int localPort = int.Parse(LocalProxyPortTextBox.Text);
        string? upstreamIp = UpstreamProxyAddressTextBox.Text==""?null:UpstreamProxyAddressTextBox.Text;
        string text = UpstreamProxyPortTextBox.Text;
        int? upstreamPort=null;
        if (text!="")
        {
            upstreamPort = int.Parse(text);
        }

        Main page = MainWindow.pages["Main"] as  Main;
        page?.ResetProxy(localIp, localPort, upstreamIp, upstreamPort, null,null, UpstreamEnabledToggle.IsChecked == true);
    }

    private void CloseProxyButton_OnClick(object sender, RoutedEventArgs e)
    {
        Main page = MainWindow.pages["Main"] as Main;
        page?.StopProxy();
    }
}
