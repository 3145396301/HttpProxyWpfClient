using System.Windows.Controls;
using System.Windows.Input;
using HttpProxyWpfClient.code.net.entity;

namespace HttpProxyWpfClient.code.Pages;

public partial class Main
{
    public void ResetProxy(string proxyHost = null, int? proxyPort = null, string? upstreamIp = null,
        int? upstreamPort = null, string upstreamUser = null, string upstreamPass = null, bool upstreamEnabled = true)
    {
        proxyConnect.ProxyHost = proxyHost;
        if (proxyPort != null) proxyConnect.ProxyPort = proxyPort.Value;
        proxyConnect.UpstreamIp = upstreamIp;
        if (upstreamPort != null) proxyConnect.UpstreamPort = upstreamPort.Value;
        proxyConnect.UpstreamUser = upstreamUser;
        proxyConnect.UpstreamPass = upstreamPass;
        proxyConnect.UpstreamEnabled = upstreamEnabled;
        SaveConfig();
        ReleaseBlockedSessions();
        System.Threading.Tasks.Task.Run(() =>
        {
            proxyConnect.ResetProxy();
            proxyConnect.StartProxy();
            proxyConnect.SettingSystemProxy();
        });
    }

    public void ResetGroups(List<RuleGroup> groups)
    {
        Groups.Clear();
        foreach (RuleGroup group in groups) Groups.Add(group);
    }

    public void StopProxy()
    {
        ReleaseBlockedSessions();
        System.Threading.Tasks.Task.Run(() =>
        {
            proxyConnect.StopSystemProxy();
            proxyConnect.StopProxy();
        });
    }

    public void ShutdownProxy()
    {
        ReleaseBlockedSessions();
        proxyConnect.StopSystemProxy();
        proxyConnect.StopProxy();
    }
}
