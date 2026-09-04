using HttpProxyWpfClient.code.net.entity;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Http;

namespace HttpProxyWpfClient.code.Pages;

public partial class Main
{
    private void ConfigureProxyPipeline()
    {
        proxyConnect.AddBeforeRequestTask("URL 鎵撳嵃", 1, session =>
        {
            RequestVo requestVo = new RequestVo(session);
            if (OnlyShowIntercepted && DiscardNonIntercepted)
            {
                lock (_pendingSessionsLock)
                {
                    _pendingSessions[session] = requestVo;
                }
            }
            else
            {
                Dispatcher.Invoke(() => Sessions.Add(requestVo));
            }

            Console.WriteLine($"{session.HttpClient.Request.Method} {session.HttpClient.Request.Url}");
            return true;
        });

        proxyConnect.AddBeforeRequestTask("璇锋眰鎷︽埅", 2, session =>
        {
            Request httpClientRequest = session.HttpClient.Request;
            foreach (RequestMatch requestMatch in EnabledRequestMatches)
            {
                if (!RequestMatch.MatchingRules(httpClientRequest, requestMatch)) continue;
                MarkIntercepted(session);
                if (!requestMatch.InterceptRequest) continue;

                lock (session)
                {
                    RequestVo requestVo = FindOrAdoptRequestVo(session);
                    Dispatcher.Invoke(() => requestVo.BlockingRequest = true);
                    Monitor.Wait(session);
                    Dispatcher.Invoke(() => requestVo.BlockingRequest = false);
                    RefreshMessagesIfSelected(session);
                }
                break;
            }
            return true;
        });

        proxyConnect.AddBeforeResponseTask("鍒锋柊璇︽儏鐣岄潰", 0, session =>
        {
            RefreshMessagesIfSelected(session);
            UpdateResponseInfo(session);
            return true;
        });

        proxyConnect.AddBeforeResponseTask("鍝嶅簲鎷︽埅", 1, session =>
        {
            Request httpClientRequest = session.HttpClient.Request;
            foreach (RequestMatch requestMatch in EnabledRequestMatches)
            {
                if (!RequestMatch.MatchingRules(httpClientRequest, requestMatch)) continue;
                MarkIntercepted(session);
                if (!requestMatch.InterceptResponse) continue;

                lock (session)
                {
                    RequestVo requestVo = FindOrAdoptRequestVo(session);
                    Dispatcher.Invoke(() => requestVo.Blocking = true);
                    Monitor.Wait(session);
                    Dispatcher.Invoke(() => requestVo.Blocking = false);
                    RefreshMessagesIfSelected(session);
                    return true;
                }
            }

            lock (_pendingSessionsLock)
            {
                _pendingSessions.Remove(session);
            }
            return true;
        });

        proxyConnect.AddAfterResponseTask("鍒锋柊璇︽儏鐣岄潰", 1, session =>
        {
            RefreshMessagesIfSelected(session);
            return true;
        });

        proxyConnect.AddAfterResponseTask("娓呯悊鏆傜紦浼氳瘽", 2, session =>
        {
            lock (_pendingSessionsLock)
            {
                _pendingSessions.Remove(session);
            }
            return true;
        });
    }
}
