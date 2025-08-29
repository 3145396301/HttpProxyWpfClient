using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Windows.Threading;
using net8._0_ProxyWPF.code.@base;
using net8._0_ProxyWPF.code.net.entity;
using net8._0_ProxyWPF.code.Task;
using Titanium.Web.Proxy;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Models;

namespace net8._0_ProxyWPF.code.net
{
    public class ProxyConnect : BindableBase
    {
        private ProxyServer _proxyServer;
        private ExplicitProxyEndPoint _explicitProxyEndPoint;
        // public static ConcurrentDictionary<SessionEventArgs,SessionInfo> SemaphoreDict = new ConcurrentDictionary<SessionEventArgs, SessionInfo>();

        private string _proxyHost;
        private int _proxyPort;
        private string? _upstreamIp;
        private int _upstreamPort;
        private string _upstreamUser;
        private string _upstreamPass;

        private int _clientConnectionCount;
        private int _serverConnectionCount;

        private TaskChain<SessionEventArgs> _beforeRequestTask = new TaskChain<SessionEventArgs>();
        private TaskChain<SessionEventArgs> _beforeResponseTask = new TaskChain<SessionEventArgs>();
        private TaskChain<SessionEventArgs> _afterResponseTask = new TaskChain<SessionEventArgs>();

        public string ProxyHost
        {
            get => _proxyHost;
            set => SetProperty(ref _proxyHost, value);
        }

        public int ProxyPort
        {
            get => _proxyPort;
            set => SetProperty(ref _proxyPort, value);
        }

        public string? UpstreamIp
        {
            get => _upstreamIp;
            set => SetProperty(ref _upstreamIp, value);
        }

        public int UpstreamPort
        {
            get => _upstreamPort;
            set => SetProperty(ref _upstreamPort, value);
        }

        public string UpstreamUser
        {
            get => _upstreamUser;
            set => SetProperty(ref _upstreamUser, value);
        }

        public string UpstreamPass
        {
            get => _upstreamPass;
            set => SetProperty(ref _upstreamPass, value);
        }

        public int ClientConnectionCount
        {
            get => _clientConnectionCount;
            set => SetProperty(ref _clientConnectionCount, value);
        }

        public int ServerConnectionCount
        {
            get => _serverConnectionCount;
            set => SetProperty(ref _serverConnectionCount, value);
        }

        public ProxyConnect()
        {
            ProxyHost = "127.0.0.1";
            ProxyPort = 8000;
            UpstreamIp = "";
            UpstreamPort = -1;
            UpstreamUser = "";
            UpstreamPass = "";
        }

        public ExplicitProxyEndPoint CreateExplicitProxyEndPoint()
        {
            ExplicitProxyEndPoint explicitProxyEndPoint = new ExplicitProxyEndPoint(IPAddress.Parse(ProxyHost), ProxyPort);
            // HTTPS 隧道相关事件
            explicitProxyEndPoint.BeforeTunnelConnectRequest += ProxyServer_BeforeTunnelConnectRequest;
            explicitProxyEndPoint.BeforeTunnelConnectResponse += ProxyServer_BeforeTunnelConnectResponse;
            return explicitProxyEndPoint;
        }

        public void AddBeforeRequestTask(string name, int priority, Func<SessionEventArgs, bool> action)
        {
            _beforeRequestTask.AddTask(name, priority, action);
        }
        public void AddBeforeResponseTask(string name, int priority, Func<SessionEventArgs, bool> action)
        {
            _beforeResponseTask.AddTask(name, priority, action);
        }
        public void AddAfterResponseTask(string name, int priority, Func<SessionEventArgs, bool> action)
        {
            _afterResponseTask.AddTask(name, priority, action);
        }




        public void CreateProxyServer()
        {
            ProxyServer proxyServer = new ProxyServer();
            proxyServer.ForwardToUpstreamGateway = true;
            proxyServer.EnableHttp2 = true;
            _explicitProxyEndPoint = new ExplicitProxyEndPoint(IPAddress.Parse(_proxyHost), _proxyPort);
            proxyServer.AddEndPoint(_explicitProxyEndPoint);
            if (_upstreamIp != null && _upstreamPort != -1)
            {
                proxyServer.UpStreamHttpProxy = new ExternalProxy { HostName = _upstreamIp, Port = _upstreamPort , UserName = _upstreamUser, Password = _upstreamPass} ;
                proxyServer.UpStreamHttpsProxy = new ExternalProxy { HostName = _upstreamIp, Port = _upstreamPort, UserName = _upstreamUser, Password = _upstreamPass };
            }
            // 事件绑定：拦截请求与响应
            proxyServer.BeforeRequest += ProxyServer_BeforeRequest;
            proxyServer.BeforeResponse += ProxyServer_BeforeResponse;
            proxyServer.AfterResponse += ProxyServer_AfterResponse;

            // 客户端连接数变化时更新 UI
            proxyServer.ClientConnectionCountChanged += delegate
            {
                Dispatcher.CurrentDispatcher.Invoke(() => { ClientConnectionCount = proxyServer.ClientConnectionCount; });
            };

            // 服务器连接数变化时更新 UI
            proxyServer.ServerConnectionCountChanged += delegate
            {
                Dispatcher.CurrentDispatcher.Invoke(() => { ServerConnectionCount = proxyServer.ServerConnectionCount; });
            };
            _proxyServer = proxyServer;
        }

        /// <summary>
        /// 请求到达代理前触发
        /// </summary>
        private async System.Threading.Tasks.Task ProxyServer_BeforeRequest(object sender, SessionEventArgs e)
        {
            // if (!SemaphoreDict.ContainsKey(e))
            // {
            //     SemaphoreDict.TryAdd(e, new SessionInfo(e));
            // }
            // 如果请求包含请求体，读取内容
            if (e.HttpClient.Request.HasBody)
            {
                e.HttpClient.Request.KeepBody = true; // 保留请求体以便后续使用
                await e.GetRequestBody();
            }
            _beforeRequestTask.Execute(e);
        }

        /// <summary>
        /// 响应到达代理前触发
        /// </summary>
        private async System.Threading.Tasks.Task ProxyServer_BeforeResponse(object sender, SessionEventArgs e)
        {
            if (e.HttpClient.Response.HasBody)
            {
                e.HttpClient.Response.KeepBody = true;
                await e.GetResponseBody();
            }
            _beforeResponseTask.Execute(e);
        }

        /// <summary>
        /// 响应完成后触发
        /// </summary>
        private async System.Threading.Tasks.Task ProxyServer_AfterResponse(object sender, SessionEventArgs e)
        {
            _afterResponseTask.Execute(e);
        }

        /// <summary>
        /// HTTPS 隧道连接请求前事件（可选择是否解密）
        /// </summary>
        private System.Threading.Tasks.Task ProxyServer_BeforeTunnelConnectRequest(object sender, TunnelConnectSessionEventArgs e)
        {
            var hostname = e.HttpClient.Request.RequestUri.Host;

            // 对特定域名不进行 SSL 解密
            if (hostname.EndsWith("webex.com"))
                e.DecryptSsl = false;
            return null;
        }

        /// <summary>
        /// HTTPS 隧道连接响应前事件
        /// </summary>
        private System.Threading.Tasks.Task ProxyServer_BeforeTunnelConnectResponse(object sender, TunnelConnectSessionEventArgs e)
        {
            return null;
        }

        public void UpdateConfig(string proxyHost = null, int? proxyPort = null, string? upstreamIp = null,
            int? upstreamPort = null, string upstreamUser = null, string upstreamPass = null)
        {
            if (proxyHost != null)
                ProxyHost = proxyHost;
            if (proxyPort != null)
                ProxyPort = proxyPort.Value;
            if (upstreamIp != null)
                UpstreamIp = upstreamIp;
            if (upstreamPort != null)
                UpstreamPort = upstreamPort.Value;
            if (upstreamUser != null)
                UpstreamUser = upstreamUser;
            if (upstreamPass != null)
                UpstreamPass = upstreamPass;
            ResetProxy();
        }

        public void StartProxy()
        {
            if (_proxyServer==null)
            {
                CreateProxyServer();
                _proxyServer?.Start();
            }else if (!_proxyServer.ProxyRunning)
            {
                _proxyServer.Start();
            }
        }


        public void StopProxy()
        {
            if (_proxyServer != null && _proxyServer.ProxyRunning)
            {
                _proxyServer.Stop();
            }
        }

        public void Dispose()
        {
            StopProxy();
            _proxyServer?.Dispose();
            _proxyServer = null;
        }

        public void ResetProxy()
        {
            Dispose();
            CreateProxyServer();
        }

        public bool ProxyStatus()
        {
            return _proxyServer != null && _proxyServer.ProxyRunning;
        }

        public void SettingSystemProxy()
        {
            if (ProxyStatus())
            {
                _proxyServer.SetAsSystemProxy((ExplicitProxyEndPoint)_proxyServer.ProxyEndPoints[0],
                    ProxyProtocolType.AllHttp);
            }
            else
            {
                Console.WriteLine("代理未启动,设置系统代理失败");
            }

        }
        public void StopSystemProxy()
        {
            if (ProxyStatus())
            {
                _proxyServer?.RestoreOriginalProxySettings();
            }
        }

        public static void ModifyDataPacket(SessionEventArgs session, int? status=null, string statusDescription=null,
            Dictionary<string, string> headers=null, byte[] body=null)
        {
            session.HttpClient.Response.StatusCode = status ?? session.HttpClient.Response.StatusCode;
            session.HttpClient.Response.StatusDescription = statusDescription ?? session.HttpClient.Response.StatusDescription;
            if (headers!=null)
            {
                foreach (var keyValuePair in headers)
                {
                    session.HttpClient.Response.Headers.AddHeader(keyValuePair.Key, keyValuePair.Value);
                }
            }

            if (body!=null)
            {
                session.SetResponseBody(body);
            }
        }

        public static void ModifyStringDataPacket(SessionEventArgs session, int? status=null, string statusDescription=null,
            Dictionary<string, string> headers=null, string body=null)
        {
            ModifyDataPacket(session, status, statusDescription, headers, body!=null?Encoding.UTF8.GetBytes(body): null);
        }


    }
}