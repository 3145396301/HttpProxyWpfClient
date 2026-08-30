using net8._0_ProxyWPF.code.net.entity;

namespace net8._0_ProxyWPF.code.net;

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
}
