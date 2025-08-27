using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using net8._0_ProxyWPF.code.net;
using net8._0_ProxyWPF.code.net.entity;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Http;

namespace net8._0_ProxyWPF.code.Pages
{
    public partial class Main : Page, INotifyPropertyChanged
    {
        private ProxyConnect proxyConnect;
        public ObservableCollection<RequestVo> Sessions { get; } = new ObservableCollection<RequestVo>();
        private RequestVo _selectedSession;
        public ObservableCollection<RequestMatch> RequestMatches { get; } = new ObservableCollection<RequestMatch>();

        public RequestVo SelectedSession
        {
            get => _selectedSession;
            set
            {
                SetField(ref _selectedSession, value);
                this.RequestMessage = new RequestMessage(value.Session);
                this.ResponseMessage = new ResponseMessage(value.Session);
            }
        }

        private RequestMessage _requestMessage;
        private ResponseMessage _responseMessage;


        public RequestMessage RequestMessage
        {
            get => _requestMessage;
            set => SetField(ref _requestMessage, value);
        }

        public ResponseMessage ResponseMessage
        {
            get => _responseMessage;
            set => SetField(ref _responseMessage, value);
        }


        public Main()
        {
            InitializeComponent();

            proxyConnect = new ProxyConnect()
                { ProxyHost = "0.0.0.0", ProxyPort = 8000, UpstreamIp = "127.0.0.1", UpstreamPort = 10808 };


                // { ProxyHost = "0.0.0.0", ProxyPort = 8000};
            proxyConnect.AddBeforeRequestTask("URL 打印", 1, session =>
            {
                this.Dispatcher.Invoke(() => { this.Sessions.Add(new RequestVo(session)); });

                Console.WriteLine($"1{session.HttpClient.Request.Method} {session.HttpClient.Request.Url}");
                return true;
            });

            proxyConnect.AddBeforeRequestTask("URL 打印", 2, session =>
            {
                this.Dispatcher.Invoke(() => { this.Sessions.Add(new RequestVo(session)); });

                Console.WriteLine($"2{session.HttpClient.Request.Method} {session.HttpClient.Request.Url}");
                return true;
            });

            proxyConnect.AddBeforeResponseTask("响应拦截", 1, session =>
            {
                Request httpClientRequest = session.HttpClient.Request;
                Response httpClientResponse = session.HttpClient.Response;
                foreach (RequestMatch requestMatch in RequestMatches)
                {
                    if (RequestMatch.MatchingRules(httpClientRequest, requestMatch))
                    {
                        ProxyConnect.SemaphoreDict[session].Semaphore.Wait();
                        return true;
                    }
                }
                return true;
            });
            // proxyConnect.AddBeforeResponseTask("响应拦截", 2, session =>
            // {
            //     Request httpClientRequest = session.HttpClient.Request;
            //     Response httpClientResponse = session.HttpClient.Response;
            //     if (RequestMatch.MatchingRules(httpClientRequest, new RequestMatch() { All = true }))
            //     {
            //         Console.WriteLine("响应拦截: 协议版本：" + httpClientResponse.HttpVersion);
            //     }
            //
            //     return true;
            // });

            proxyConnect.CreateProxyServer();
            proxyConnect.StartProxy();
            proxyConnect.SettingSystemProxy();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private void Selector_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RequestVo selectedSession = (RequestVo)(sender as ListView).SelectedValue;
            this.SelectedSession = selectedSession;
        }

        private void block_OnClick(object sender, RoutedEventArgs e)
        {
            RequestMatches.Add(new RequestMatch() { All = true });
        }

        private void discharged_OnClick(object sender, RoutedEventArgs e)
        {
            RequestMatches.Clear();
            foreach (KeyValuePair<SessionEventArgs, SessionInfo> keyValuePair in ProxyConnect.SemaphoreDict)
            {
                keyValuePair.Value.Semaphore.Release();
            }
        }

        private void Pass_OnClick(object sender, RoutedEventArgs e)
        {
            string responseText = Response.Text;
            //解析 responseText 分割响应报文为3段 第一段位置至第一个/r/n 第二段位置至第一个（空行处）/r/n/r/n 第三段位置至末尾
            Response response = SelectedSession.Session.HttpClient.Response;
            // 1. 解析状态行
            int firstLineEnd = responseText.IndexOf("\r\n");
            string statusLine = firstLineEnd >= 0
                ? responseText.Substring(0, firstLineEnd)
                : string.Empty;

            string protocol = string.Empty;
            string statusCode = string.Empty;
            string statusDescription = string.Empty;

            if (!string.IsNullOrWhiteSpace(statusLine))
            {
                var parts = statusLine.Split(new[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 1)
                {
                    protocol = parts[0];
                    response.HttpVersion = new Version(protocol);
                }

                if (parts.Length >= 2)
                {
                    statusCode = parts[1];
                    response.StatusCode = int.Parse(statusCode);
                }

                if (parts.Length >= 3)
                {
                    statusDescription = parts[2];
                    response.StatusDescription = statusDescription;
                }
            }


            // 2. 解析响应头
            int headerEnd = responseText.IndexOf("\r\n\r\n");
            string headers = string.Empty;
            string body = string.Empty;

            if (headerEnd >= 0)
            {
                headers = responseText.Substring(firstLineEnd + 2, headerEnd - (firstLineEnd + 2));
                body = responseText.Substring(headerEnd + 4);
            }

            // 3. 拆分 Header 成 K-V
            var headerLines = headers.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in headerLines)
            {
                int colonIndex = line.IndexOf(":");
                if (colonIndex > 0)
                {
                    string key = line.Substring(0, colonIndex).Trim();
                    string value = line.Substring(colonIndex + 1).Trim();

                    // 添加到 Response.Headers
                    response.Headers.AddHeader(key, value);
                }
            }

            if (response.HasBody)
            {
                if (response.BodyString != body)
                {
                    SelectedSession.Session.SetResponseBody(System.Text.Encoding.UTF8.GetBytes(body));
                }
            }

            //放行
            ProxyConnect.SemaphoreDict[SelectedSession.Session].Semaphore.Release();
            // 测试输出
            // MessageBox.Show($"状态行:\n{statusLine}\n\n响应头:\n{headers}\n\n响应体:\n{body}");
        }
    }
}