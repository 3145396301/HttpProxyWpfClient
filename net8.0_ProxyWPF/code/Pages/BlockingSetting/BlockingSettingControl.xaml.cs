using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using net8._0_ProxyWPF.code.net.entity;

namespace net8._0_ProxyWPF.code.Pages.BlockingSetting;

public partial class BlockingSettingControl : UserControl
{
    public ObservableCollection<RequestMatchVo> requestMatches { get;} = new ObservableCollection<RequestMatchVo>();
    public BlockingSettingControl()
    {
        InitializeComponent();
        RequestMatchVo requestMatchVo = new RequestMatchVo(){Name = "所有"};
        requestMatchVo.Headers.Add(new HeaderVo(){Name = "Host", Value = "*"});
        requestMatches.Add(requestMatchVo);
        requestMatches.Add(new RequestMatchVo(){Name = "百度"});
    }

    private void NewRule_OnClick(object sender, RoutedEventArgs e)
    {
        requestMatches.Add(new RequestMatchVo(){Name = NewRuleName.Text});
    }
}