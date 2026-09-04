using System.Windows.Controls;
using System.Windows.Input;
using HttpProxyWpfClient.code.net.entity;

namespace HttpProxyWpfClient.code.Pages;

public partial class Main
{
    private void UIElement_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Delete || sender is not ListView listView)
            return;

        RemoveSessions(listView.SelectedItems.OfType<RequestVo>().ToArray());
        e.Handled = true;
    }

    private void ReleaseBlockedSessions()
    {
        foreach (RequestVo request in Sessions)
        {
            lock (request.Session)
            {
                Monitor.PulseAll(request.Session);
            }
        }
    }

    private void RemoveSessions(IEnumerable<RequestVo> sessions)
    {
        foreach (RequestVo request in sessions.ToArray())
        {
            Sessions.Remove(request);
            request.Session.Dispose();
        }
    }
}
