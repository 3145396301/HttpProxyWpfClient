using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using HttpProxyWpfClient.code.Loc;
using HttpProxyWpfClient.code.net.entity;

namespace HttpProxyWpfClient.code.Pages.BlockingSetting;

public partial class BlockingSettingControl : UserControl
{
    public ObservableCollection<RuleGroupVo> groups { get; } = new ObservableCollection<RuleGroupVo>();

    public BlockingSettingControl()
    {
        InitializeComponent();
    }

    public BlockingSettingControl(ObservableCollection<RuleGroup> ruleGroups)
    {
        InitializeComponent();
        groups.Clear();
        foreach (var ruleGroup in ruleGroups)
        {
            groups.Add(RuleGroupVo.FromRuleGroup(ruleGroup));
        }
    }

    private void NewGroup_OnClick(object sender, RoutedEventArgs e)
    {
        string name = NewGroupName.Text;
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        groups.Add(new RuleGroupVo { Name = name });
        NewGroupName.Text = string.Empty;
    }

    private void DeleteGroup_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: RuleGroupVo groupVo })
        {
            groups.Remove(groupVo);
        }
    }

    private void AddRule_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: RuleGroupVo groupVo })
        {
            groupVo.Rules.Add(new RequestMatchVo { Name = LocalizationManager.GetString("NewRule") });
        }
    }

    private void DeleteRule_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not RequestMatchVo requestMatchVo)
        {
            return;
        }

        foreach (var groupVo in groups)
        {
            if (groupVo.Rules.Contains(requestMatchVo))
            {
                requestMatchVo.ClearHeaders();
                groupVo.Rules.Remove(requestMatchVo);
                break;
            }
        }
    }

    private void AddHeader_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: RequestMatchVo requestMatchVo })
        {
            requestMatchVo.AddHeader("", "");
        }
    }

    private void DeleteHeader_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: HeaderVo headerVo })
        {
            headerVo.RequestMatchVo?.RemoveHeader(headerVo);
        }
    }

    /// <summary>
    /// 将当前 UI 编辑的分组/规则回写到 Main 页面并触发持久化保存
    /// </summary>
    public void UpdateGroups()
    {
        List<RuleGroup> ruleGroups = new List<RuleGroup>();
        foreach (RuleGroupVo groupVo in groups)
        {
            ruleGroups.Add(groupVo.ToRuleGroup());
        }
        Main? page = MainWindow.pages["Main"] as Main;
        page?.ResetGroups(ruleGroups);
    }
}