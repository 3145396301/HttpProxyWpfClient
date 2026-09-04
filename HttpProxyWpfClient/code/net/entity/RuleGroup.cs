using HttpProxyWpfClient.code.@base;

namespace HttpProxyWpfClient.code.net.entity
{
    /// <summary>
    /// 拦截规则分组：包含分组名称、是否启用，以及分组下的多条规则
    /// </summary>
    public class RuleGroup : BindableBase
    {
        string _name;
        bool _enabled = true;
        List<RequestMatch> _rules = new List<RequestMatch>();

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        /// <summary>
        /// 分组是否启用，未启用时该分组下所有规则都不参与匹配
        /// </summary>
        public bool Enabled
        {
            get => _enabled;
            set => SetProperty(ref _enabled, value);
        }

        public List<RequestMatch> Rules
        {
            get => _rules;
            set => SetProperty(ref _rules, value);
        }

        public RuleGroup()
        {
        }

        public RuleGroup(string name)
        {
            Name = name;
        }
    }
}
