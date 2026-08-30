using System.Windows;
using System.Windows.Controls;

namespace net8._0_ProxyWPF.code.Pages.Setting;

public partial class SettingPage : Page
{
    public SettingPage()
    {
        InitializeComponent();
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
        Main page = MainWindow.pages["Main"] as  Main;
        page?.StopProxy();
    }
}