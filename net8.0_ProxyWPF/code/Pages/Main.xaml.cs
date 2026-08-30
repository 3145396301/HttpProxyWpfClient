using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using net8._0_ProxyWPF.code.net;
using net8._0_ProxyWPF.code.net.entity;
using net8._0_ProxyWPF.code.net.util;
using net8._0_ProxyWPF.code.Pages.BlockingSetting;
using net8._0_ProxyWPF.code.Pages.Util;
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
                if (value==null)
                {
                    this.RequestMessage = null;
                    this.ResponseMessage = null;
                    return;
                }
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

                Console.WriteLine($"{session.HttpClient.Request.Method} {session.HttpClient.Request.Url}");
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
                        lock (session)
                        {
                            //查找到对应的 RequestVo
                            RequestVo requestVo = this.Sessions.FirstOrDefault(x => x.Session == session);
                            this.Dispatcher.Invoke(() => { requestVo.Blocking = true; });
                            // ProxyConnect.SemaphoreDict[session].Semaphore.Wait();
                            Monitor.Wait(session);
                            this.Dispatcher.Invoke(() => { requestVo.Blocking = false; });
                            return true;
                        }
                    }
                }

                return true;
            });

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
            // RequestMatches.Add(new RequestMatch() { All = true });
            BlockingSettingControl blockingSettingControl = new BlockingSettingControl(RequestMatches);
            DialogHelper.ShowDialogAsync<bool>("添加拦截规则",blockingSettingControl,true,900D,600D,onClose: w =>
            {
                blockingSettingControl.UpdateRequestMatches();
                Console.WriteLine("拦截规则保存成功");
            });
        }

        private void discharged_OnClick(object sender, RoutedEventArgs e)
        {
            foreach (RequestVo requestVo in Sessions)
            {
                lock (requestVo.Session)
                {
                    Monitor.Pulse(requestVo.Session);
                }
            }
        }

        private void SessionList_OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            DependencyObject source = e.OriginalSource as DependencyObject;
            while (source != null && source is not Wpf.Ui.Controls.ListViewItem)
            {
                source = VisualTreeHelper.GetParent(source);
            }

            if (source is Wpf.Ui.Controls.ListViewItem item)
            {
                item.IsSelected = true;
            }
        }

        private void CopyCurlCmd_OnClick(object sender, RoutedEventArgs e)
        {
            CopyCurl(CurlShellType.Cmd);
        }

        private void CopyCurlBash_OnClick(object sender, RoutedEventArgs e)
        {
            CopyCurl(CurlShellType.Bash);
        }

        private void CopyCurlPowerShell_OnClick(object sender, RoutedEventArgs e)
        {
            CopyCurl(CurlShellType.PowerShell);
        }

        private void CopyCurl(CurlShellType shellType)
        {
            if (SelectedSession == null)
            {
                return;
            }

            string curlCommand = CurlCommandGenerator.Generate(SelectedSession.Session.HttpClient.Request, shellType);
            Clipboard.SetText(curlCommand);
        }

        private void ErrorText_OnClick(object sender, MouseButtonEventArgs e)
        {
            string errorText = ResponseMessage?.ErrorText;
            if (string.IsNullOrEmpty(errorText))
            {
                return;
            }

            TextBox textBox = new TextBox
            {
                Text = errorText,
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(10),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            DialogHelper.ShowDialogAsync<bool>("请求未完成", textBox, true, 600D, 400D);
        }

        private void Pass_OnClick(object sender, RoutedEventArgs e)
        {
            string responseText = Response.Text;
            if (SelectedSession==null)
            {
                return;
            }
            Response response = SelectedSession.Session.HttpClient.Response;

            try
            {
                ParseHttpResponseText(responseText, response);
            }
            catch (Exception ex)
            {
                // 根据需要记录错误
                Debug.WriteLine("解析响应出错: " + ex);
            }
            finally
            {
                // 无论如何都释放，避免死锁
                lock (SelectedSession.Session)
                {
                    Monitor.Pulse(SelectedSession.Session);
                }
            }
        }

        private void ParseHttpResponseText(string responseText, Response response)
        {
            if (responseText == null) responseText = string.Empty;

            // 1) 找到头/体分隔符（优先 CRLF CRLF，然后 LF LF，最后其它）
            int headerBodySep = responseText.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            string sepToken = "\r\n\r\n";
            if (headerBodySep == -1)
            {
                headerBodySep = responseText.IndexOf("\n\n", StringComparison.Ordinal);
                sepToken = "\n\n";
            }

            if (headerBodySep == -1)
            {
                headerBodySep = responseText.IndexOf("\r\r", StringComparison.Ordinal);
                sepToken = "\r\r";
            }

            // 2) 找到第一行结束位置（优先 \r\n，再 \n，再认为没有换行）
            int firstLineEnd = responseText.IndexOf("\r\n", StringComparison.Ordinal);
            int firstLineNewlineLen = 2;
            if (firstLineEnd == -1)
            {
                firstLineEnd = responseText.IndexOf("\n", StringComparison.Ordinal);
                firstLineNewlineLen = 1;
            }

            if (firstLineEnd == -1)
            {
                // 没有换行，整段文本可能只有状态行或只有 body
                firstLineEnd = Math.Min(responseText.Length, headerBodySep >= 0 ? headerBodySep : responseText.Length);
                firstLineNewlineLen = 0;
            }

            // 提取 status line（如果存在）
            string statusLine = firstLineEnd > 0
                ? responseText.Substring(0, firstLineEnd)
                : responseText.Substring(0, Math.Min(responseText.Length, firstLineEnd));

            // 解析状态行（容错）
            string protocol = string.Empty;
            string statusCode = string.Empty;
            string statusDescription = string.Empty;
            if (!string.IsNullOrWhiteSpace(statusLine))
            {
                var parts = statusLine.Split(new[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
                try
                {
                    if (parts.Length >= 1)
                    {
                        protocol = parts[0];
                        if (protocol.StartsWith("HTTP/", StringComparison.OrdinalIgnoreCase))
                        {
                            string ver = protocol.Substring("HTTP/".Length);
                            if (Version.TryParse(ver, out Version v)) response.HttpVersion = v;
                        }
                    }

                    if (parts.Length >= 2)
                    {
                        statusCode = parts[1];
                        if (int.TryParse(statusCode, out int sc)) response.StatusCode = sc;
                    }

                    if (parts.Length >= 3)
                    {
                        statusDescription = parts[2];
                        response.StatusDescription = statusDescription;
                    }
                }
                catch
                {
                    // 忽略解析错误，保持已有 response 字段
                }
            }

            // 3) 提取 headers 与 body（支持没有分隔符的情况）
            string headersText = string.Empty;
            string bodyText = string.Empty;
            if (headerBodySep >= 0)
            {
                int headersStart = firstLineEnd + firstLineNewlineLen;
                if (headersStart < headerBodySep)
                    headersText = responseText.Substring(headersStart, headerBodySep - headersStart);
                else
                    headersText = string.Empty;

                int bodyStart = headerBodySep + sepToken.Length;
                if (bodyStart < responseText.Length)
                    bodyText = responseText.Substring(bodyStart);
                else
                    bodyText = string.Empty;
            }
            else
            {
                // 没有明显的空行分隔符：如果 status 之后还有内容，判断剩下部分里是否像 headers（包含 ':'）
                if (firstLineEnd < responseText.Length)
                {
                    int restStart = firstLineEnd + firstLineNewlineLen;
                    if (restStart < responseText.Length)
                    {
                        string rest = responseText.Substring(restStart);
                        if (rest.Contains(":")) // 很可能是 headers（但不能 100% 确认）
                        {
                            headersText = rest;
                            bodyText = string.Empty;
                        }
                        else
                        {
                            // 把剩下当作 body（没有头）
                            headersText = string.Empty;
                            bodyText = rest;
                        }
                    }
                }
                else
                {
                    headersText = string.Empty;
                    bodyText = string.Empty;
                }
            }

            // 4) 拆分 header 行并添加（对不同换行符都支持）
            if (!string.IsNullOrEmpty(headersText))
            {
                // 先尽可能清空已有头（视 Response.Headers API）
                try
                {
                    // 如果有 Clear 方法，则清理；若没有则忽略
                    var clearMethod = response.Headers.GetType().GetMethod("Clear");
                    clearMethod?.Invoke(response.Headers, null);
                }
                catch
                {
                    /* 忽略 */
                }

                string[] headerLines =
                    headersText.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in headerLines)
                {
                    int colonIndex = line.IndexOf(':');
                    if (colonIndex > 0)
                    {
                        string key = line.Substring(0, colonIndex).Trim();
                        string value = line.Substring(colonIndex + 1).Trim();

                        // 兼容添加：优先使用已有的 AddHeader 方法；若抛异常则尝试 Set 或 Append（根据实际对象）
                        try
                        {
                            response.Headers.AddHeader(key, value);
                        }
                        catch
                        {
                            try
                            {
                                // 若有 Set 方法（例如 SetHeader），则调用替换
                                var setMethod = response.Headers.GetType().GetMethod("Set") ??
                                                response.Headers.GetType().GetMethod("SetHeader");
                                if (setMethod != null)
                                {
                                    setMethod.Invoke(response.Headers, new object[] { key, value });
                                }
                                else
                                {
                                    // 尝试 Add 且不抛出
                                    var addMethod = response.Headers.GetType().GetMethod("Add");
                                    addMethod?.Invoke(response.Headers, new object[] { key, value });
                                }
                            }
                            catch
                            {
                                // 最后容忍失败（避免整个解析失败）
                            }
                        }
                    }
                }
            }

            // 5) 处理 body（若需要可按 Content-Length / chunked 进一步处理）
            // 如果是 chunked，需要 decode；此处先做基本判断并写入 response.Body
            bool isChunked = false;
            try
            {
                // 尝试读取 Transfer-Encoding 或 Content-Length
                var te = GetHeaderValue(response.Headers, "Transfer-Encoding");
                if (!string.IsNullOrEmpty(te) && te.IndexOf("chunked", StringComparison.OrdinalIgnoreCase) >= 0)
                    isChunked = true;
            }
            catch
            {
                /* 忽略 */
            }

            byte[] bodyBytes;
            if (isChunked)
            {
                // 简单实现：若 bodyText 是 chunked 文本，则解码；如果你确实需要处理 chunked 二进制，请替换为更健壮的实现
                try
                {
                    bodyBytes = DecodeChunkedBody(bodyText);
                }
                catch
                {
                    bodyBytes = System.Text.Encoding.UTF8.GetBytes(bodyText);
                }
            }
            else
            {
                // 直接使用 UTF8 解码（如果服务器是二进制/其他编码，你可以用 Content-Type 来判断）
                bodyBytes = System.Text.Encoding.UTF8.GetBytes(bodyText);
            }

            // 只有当有 body 且与现有不同才设置
            if (bodyBytes != null && bodyBytes.Length > 0)
            {
                try
                {
                    if (response.HasBody)
                    {
                        // 对比简单字串（如果你使用二进制比较可替换）
                        string existing = response.BodyString ?? string.Empty;
                        string newBodyStr = System.Text.Encoding.UTF8.GetString(bodyBytes);
                        if (existing != newBodyStr)
                        {
                            SelectedSession.Session.SetResponseBody(bodyBytes);
                        }
                    }
                    else
                    {
                        SelectedSession.Session.SetResponseBody(bodyBytes);
                    }
                }
                catch
                {
                    // 如果 SetResponseBody 失败，不要抛出到上层
                }
            }
            else
            {
                // body 为空时：如果响应标记了没有 body，则不设置
            }

            // 本方法结束
        }

        // 小助手：从 headers 集合读取 header 值（尝试多种可能的接口）
        private string GetHeaderValue(object headersObj, string headerName)
        {
            if (headersObj == null) return null;
            try
            {
                // 常见 Header 集合会有索引器或 Get 方法
                var t = headersObj.GetType();
                var indexer = t.GetProperty("Item");
                if (indexer != null)
                {
                    var val = indexer.GetValue(headersObj, new object[] { headerName });
                    return val?.ToString();
                }

                var getMethod = t.GetMethod("Get") ?? t.GetMethod("GetHeader") ?? t.GetMethod("GetValues");
                if (getMethod != null)
                {
                    var val = getMethod.Invoke(headersObj, new object[] { headerName });
                    if (val is string) return (string)val;
                    if (val is IEnumerable<string> seq) return string.Join(", ", seq);
                    return val?.ToString();
                }
            }
            catch
            {
            }

            return null;
        }

        // 简单的 chunked 解码（输入为 chunked 文本形式），返回解码后的字节（只作示范）
        private byte[] DecodeChunkedBody(string chunked)
        {
            if (string.IsNullOrEmpty(chunked)) return Array.Empty<byte>();
            using (var ms = new MemoryStream())
            {
                int pos = 0;
                // 使用 \r\n 或 \n 作为行结束处理
                while (pos < chunked.Length)
                {
                    // 读取行（chunk size）
                    int lineEnd = chunked.IndexOf("\r\n", pos, StringComparison.Ordinal);
                    int newlineLen = 2;
                    if (lineEnd == -1)
                    {
                        lineEnd = chunked.IndexOf("\n", pos, StringComparison.Ordinal);
                        newlineLen = 1;
                    }

                    if (lineEnd == -1) break;
                    string sizeLine = chunked.Substring(pos, lineEnd - pos).Trim();
                    pos = lineEnd + newlineLen;
                    if (string.IsNullOrEmpty(sizeLine)) continue;
                    int chunkSize = 0;
                    try
                    {
                        chunkSize = Convert.ToInt32(sizeLine.Split(';')[0], 16);
                    }
                    catch
                    {
                        break;
                    }

                    if (chunkSize == 0) break; // 结束
                    // 取 chunkSize 个字符（注意：这里是字符数，真实二进制更复杂）
                    if (pos + chunkSize > chunked.Length) chunkSize = chunked.Length - pos;
                    var chunkData = System.Text.Encoding.UTF8.GetBytes(chunked.Substring(pos, chunkSize));
                    ms.Write(chunkData, 0, chunkData.Length);
                    pos += chunkSize;
                    // 跳过后面的 CRLF
                    if (pos + 1 < chunked.Length && chunked[pos] == '\r' && chunked[pos + 1] == '\n') pos += 2;
                    else if (pos < chunked.Length && chunked[pos] == '\n') pos += 1;
                }

                return ms.ToArray();
            }
        }


        private void ModifyRequestBody_OnClick(object sender, RoutedEventArgs e)
        {
            // 弹出一个文本框窗口 ， 获取输入的文本

            Window dialog = new Window
            {
                Title = "输入请求体",
                Width = 400,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this), // 当前窗口作为父窗口
                ResizeMode = ResizeMode.NoResize
            };

            // 布局
            Grid grid = new Grid { Margin = new Thickness(10) };
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // 输入框
            TextBox textBox = new TextBox
            {
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            Grid.SetRow(textBox, 0);
            grid.Children.Add(textBox);

            // 按钮区
            StackPanel panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };

            Button okBtn = new Button { Content = "确定", Width = 75, Margin = new Thickness(5) };
            Button cancelBtn = new Button { Content = "取消", Width = 75, Margin = new Thickness(5) };

            panel.Children.Add(okBtn);
            panel.Children.Add(cancelBtn);

            Grid.SetRow(panel, 1);
            grid.Children.Add(panel);

            dialog.Content = grid;

            // 按钮事件
            okBtn.Click += (s, args) =>
            {
                dialog.DialogResult = true;
                dialog.Close();
            };
            cancelBtn.Click += (s, args) =>
            {
                dialog.DialogResult = false;
                dialog.Close();
            };

            // 显示对话框并取结果
            if (dialog.ShowDialog() == true)
            {
                string input = textBox.Text;

                ResponseMessage.RespBody = input;
            }
        }

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

            // 释放所有还卡在 Monitor.Wait 的会话，避免 ResetProxy 内部 Stop() 时死锁
            foreach (RequestVo requestVo in Sessions)
            {
                lock (requestVo.Session)
                {
                    Monitor.PulseAll(requestVo.Session);
                }
            }

            // ResetProxy/StartProxy 内部会阻塞等待旧连接关闭，放到后台线程执行，避免卡死 UI
            System.Threading.Tasks.Task.Run(() =>
            {
                proxyConnect.ResetProxy();
                proxyConnect.StartProxy();
                proxyConnect.SettingSystemProxy();
            });
        }

        public void ResetRequestMatches(List<RequestMatch> requestMatches)
        {
            RequestMatches.Clear();
            foreach (var requestMatch in requestMatches)
            {
                RequestMatches.Add(requestMatch);
            }
        }

        private void UIElement_OnKeyDown(object sender, KeyEventArgs e)
        {
            ListView? listView = sender as ListView;
            IList listViewSelectedItems = listView.SelectedItems;
            List<RequestVo> selectedItems = new List<RequestVo>();
            foreach (var item in listViewSelectedItems)
            {
                selectedItems.Add(item as RequestVo);
            }
            new Thread((() =>
            {
                if (e.Key == Key.Delete)
                {
                    Console.WriteLine($"数量：{selectedItems.Count}");
                    foreach (var item in selectedItems)
                    {
                        this.Dispatcher.Invoke(() =>
                        {
                            Sessions.Remove(item);
                            item.Session.Dispose();
                        });
                        Console.WriteLine($"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")},删除了");
                    }
                }
            })).Start();
        }

        public void StopProxy()
        {
            // 释放所有还卡在 Monitor.Wait 的会话，避免 Stop() 时死锁
            foreach (RequestVo requestVo in Sessions)
            {
                lock (requestVo.Session)
                {
                    Monitor.PulseAll(requestVo.Session);
                }
            }

            // Stop() 本身是阻塞调用，放到后台线程执行，避免卡死 UI
            System.Threading.Tasks.Task.Run(() =>
            {
                proxyConnect.StopSystemProxy();
                proxyConnect.StopProxy();
            });
        }

        /// <summary>
        /// 程序退出前调用：同步关闭代理，确保系统代理设置在进程结束前已还原
        /// </summary>
        public void ShutdownProxy()
        {
            foreach (RequestVo requestVo in Sessions)
            {
                lock (requestVo.Session)
                {
                    Monitor.PulseAll(requestVo.Session);
                }
            }

            proxyConnect.StopSystemProxy();
            proxyConnect.StopProxy();
        }
    }
}