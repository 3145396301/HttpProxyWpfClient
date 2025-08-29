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
    }

    public BlockingSettingControl(ObservableCollection<RequestMatch> matches)
    {
        InitializeComponent();
        requestMatches.Clear();
        foreach (var requestMatch in matches)
        {
            requestMatches.Add(requestMatch.ToRequestMatchVo());
        }
    }

    private void NewRule_OnClick(object sender, RoutedEventArgs e)
    {
        requestMatches.Add(new RequestMatchVo(){Name = NewRuleName.Text});
    }

    private void AddHeader_Click(object sender, RoutedEventArgs e)
    {
        Button? button = sender as Button;
        if (button != null)
        {
            RequestMatchVo requestMatchVo = button.DataContext as RequestMatchVo;
            if (requestMatchVo != null)
            {
                requestMatchVo.AddHeader("","");
            }
        }
    }

    private void DeleteHeader_Click(object sender, RoutedEventArgs e)
    {
        Button? button = sender as Button;
        if (button != null)
        {
            HeaderVo headerVo = button.DataContext as HeaderVo;
            if (headerVo != null)
            {
                RequestMatchVo requestMatchVo = headerVo.RequestMatchVo;
                requestMatchVo.RemoveHeader(headerVo);
            }
        }
    }

    private void DeleteRule_Click(object sender, RoutedEventArgs e)
    {
        Button? button = sender as Button;
        if (button != null)
        {
            RequestMatchVo requestMatchVo = button.DataContext as RequestMatchVo;
            if (requestMatchVo != null)
            {
                requestMatchVo.ClearHeaders();
                requestMatches.Remove(requestMatchVo);
            }
        }
    }

    public void UpdateRequestMatches()
    {
        List<RequestMatch> requests = new List<RequestMatch>();
        foreach (RequestMatchVo requestMatchVo in requestMatches)
        {
            requests.Add(requestMatchVo.ToRequestMatch());
        }
        Main? page = MainWindow.pages["Main"] as Main;
        page?.ResetRequestMatches(requests);
    }


}