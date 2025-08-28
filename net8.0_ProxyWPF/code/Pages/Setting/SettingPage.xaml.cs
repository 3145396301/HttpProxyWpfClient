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
        string upstreamIp = UpstreamProxyAddressTextBox.Text;
        int upstreamPort = int.Parse(UpstreamProxyPortTextBox.Text);
        Main page = MainWindow.pages["Main"] as  Main;
        page?.ResetProxy(localIp, localPort, upstreamIp, upstreamPort, null,null);
    }
}