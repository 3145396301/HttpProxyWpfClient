using HttpProxyWpfClient.code.net.entity;

namespace HttpProxyWpfClient.code.net;

/// <summary>
/// 本地持久化的应用配置：本地代理设置、上游代理设置、拦截规则分组
/// </summary>
public class AppConfig
{
    public string LocalProxyHost { get; set; } = "0.0.0.0";
    public int LocalProxyPort { get; set; } = 8000;

    public bool UpstreamEnabled { get; set; } = true;
    public string? UpstreamHost { get; set; } = "127.0.0.1";
    public int? UpstreamPort { get; set; } = 10808;
    public string? UpstreamUser { get; set; }
    public string? UpstreamPass { get; set; }

    public List<RuleGroup> Groups { get; set; } = new List<RuleGroup>();

    /// <summary>
    /// 请求内容区字体大小（Ctrl+滚轮可调整）
    /// </summary>
    public double RequestContentFontSize { get; set; } = 13;

    /// <summary>
    /// 响应内容区字体大小（Ctrl+滚轮可调整）
    /// </summary>
    public double ResponseContentFontSize { get; set; } = 13;

    /// <summary>
    /// "编辑完整请求体/响应体"弹窗字体大小（Ctrl+滚轮可调整）
    /// </summary>
    public double EditBodyFontSize { get; set; } = 13;

    /// <summary>
    /// 会话列表各列的显隐与宽度（右键表头可调整）
    /// </summary>
    public List<SessionColumnSetting> SessionColumns { get; set; } = new();
}

/// <summary>
/// 会话列表单列的持久化配置
/// </summary>
public class SessionColumnSetting
{
    public string Key { get; set; } = "";
    public bool Visible { get; set; } = true;
    public double Width { get; set; } = 100;
}
