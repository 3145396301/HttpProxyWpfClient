using System.Collections.ObjectModel;
using HttpProxyWpfClient.code.@base;

namespace HttpProxyWpfClient.code.net.entity;

/// <summary>
/// 分组的 UI 视图模型，供 BlockingSettingControl 双向绑定
/// </summary>
public class RuleGroupVo : BindableBase
{
    private string _name;
    private bool _enabled = true;
    private ObservableCollection<RequestMatchVo> _rules;

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public bool Enabled
    {
        get => _enabled;
        set => SetProperty(ref _enabled, value);
    }

    public ObservableCollection<RequestMatchVo> Rules
    {
        get => _rules;
        set => SetProperty(ref _rules, value);
    }

    public RuleGroupVo()
    {
        Rules = new ObservableCollection<RequestMatchVo>();
    }

    public RuleGroup ToRuleGroup()
    {
        var group = new RuleGroup(Name) { Enabled = Enabled };
        foreach (var rule in Rules)
        {
            group.Rules.Add(rule.ToRequestMatch());
        }
        return group;
    }

    public static RuleGroupVo FromRuleGroup(RuleGroup group)
    {
        var vo = new RuleGroupVo { Name = group.Name, Enabled = group.Enabled };
        foreach (var rule in group.Rules)
        {
            vo.Rules.Add(rule.ToRequestMatchVo());
        }
        return vo;
    }
}
