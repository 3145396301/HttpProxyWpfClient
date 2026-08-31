using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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
        private readonly SearchHighlightRenderer _highlightRenderer = new();

        /// <summary>
        /// 供设置页读取当前生效的代理配置，用于打开设置页时回填输入框
        /// </summary>
        public ProxyConnect ProxyConnect => proxyConnect;
        public ObservableCollection<RequestVo> Sessions { get; } = new ObservableCollection<RequestVo>();
        private readonly ICollectionView _sessionsView;
        private RequestVo _selectedSession;
        public ObservableCollection<RuleGroup> Groups { get; } = new ObservableCollection<RuleGroup>();

        /// <summary>
        /// 尚未确定是否命中拦截规则、暂缓加入 Sessions 的会话（配合"直接丢弃非拦截请求"子开关使用）。
        /// 在 BeforeResponse 阶段完成最终判定后：命中拦截则补加入列表，否则连同其消息一起被丢弃（正常转发但不渲染）。
        /// </summary>
        private readonly Dictionary<SessionEventArgs, RequestVo> _pendingSessions = new();
        private readonly object _pendingSessionsLock = new();

        private bool _onlyShowIntercepted;
        /// <summary>
        /// 主开关：只展示拦截请求。开启后过滤视图仅显示 Intercepted=true 的会话；
        /// 关闭后立即恢复显示全部已收集的会话（软过滤，不影响底层 Sessions 集合内容）
        /// </summary>
        public bool OnlyShowIntercepted
        {
            get => _onlyShowIntercepted;
            set
            {
                if (SetField(ref _onlyShowIntercepted, value))
                {
                    _sessionsView.Refresh();
                }
            }
        }

        private bool _discardNonIntercepted;
        /// <summary>
        /// 子开关：直接丢弃非拦截请求。开启后，非拦截会话不会被加入 Sessions（正常转发但不渲染），
        /// 关闭后不会找回之前已丢弃的会话。仅在 OnlyShowIntercepted 开启时生效。
        /// </summary>
        public bool DiscardNonIntercepted
        {
            get => _discardNonIntercepted;
            set => SetField(ref _discardNonIntercepted, value);
        }

        #region 搜索

        private bool _isSearchPanelOpen;
        public bool IsSearchPanelOpen
        {
            get => _isSearchPanelOpen;
            set => SetField(ref _isSearchPanelOpen, value);
        }

        private string _searchKeyword = "";
        public string SearchKeyword
        {
            get => _searchKeyword;
            set
            {
                if (SetField(ref _searchKeyword, value))
                {
                    RunSearch();
                }
            }
        }

        private bool _searchUseRegex;
        public bool SearchUseRegex
        {
            get => _searchUseRegex;
            set
            {
                if (SetField(ref _searchUseRegex, value))
                {
                    RunSearch();
                }
            }
        }

        private bool _searchFieldHost = true;
        public bool SearchFieldHost { get => _searchFieldHost; set { if (SetField(ref _searchFieldHost, value)) RunSearch(); } }

        private bool _searchFieldUrl = true;
        public bool SearchFieldUrl { get => _searchFieldUrl; set { if (SetField(ref _searchFieldUrl, value)) RunSearch(); } }

        private bool _searchFieldMethod = true;
        public bool SearchFieldMethod { get => _searchFieldMethod; set { if (SetField(ref _searchFieldMethod, value)) RunSearch(); } }

        private bool _searchFieldStatusCode = true;
        public bool SearchFieldStatusCode { get => _searchFieldStatusCode; set { if (SetField(ref _searchFieldStatusCode, value)) RunSearch(); } }

        private bool _searchFieldRequestHeaders = true;
        public bool SearchFieldRequestHeaders { get => _searchFieldRequestHeaders; set { if (SetField(ref _searchFieldRequestHeaders, value)) RunSearch(); } }

        private bool _searchFieldRequestBody = true;
        public bool SearchFieldRequestBody { get => _searchFieldRequestBody; set { if (SetField(ref _searchFieldRequestBody, value)) RunSearch(); } }

        private bool _searchFieldResponseHeaders = true;
        public bool SearchFieldResponseHeaders { get => _searchFieldResponseHeaders; set { if (SetField(ref _searchFieldResponseHeaders, value)) RunSearch(); } }

        private bool _searchFieldResponseBody = true;
        public bool SearchFieldResponseBody { get => _searchFieldResponseBody; set { if (SetField(ref _searchFieldResponseBody, value)) RunSearch(); } }

        public ObservableCollection<SearchResultItem> SearchResults { get; } = new ObservableCollection<SearchResultItem>();

        private SearchOptions BuildSearchOptions()
        {
            var options = new SearchOptions { Keyword = SearchKeyword, UseRegex = SearchUseRegex };
            options.Fields.Clear();
            if (SearchFieldHost) options.Fields.Add(SearchField.Host);
            if (SearchFieldUrl) options.Fields.Add(SearchField.Url);
            if (SearchFieldMethod) options.Fields.Add(SearchField.Method);
            if (SearchFieldStatusCode) options.Fields.Add(SearchField.StatusCode);
            if (SearchFieldRequestHeaders) options.Fields.Add(SearchField.RequestHeaders);
            if (SearchFieldRequestBody) options.Fields.Add(SearchField.RequestBody);
            if (SearchFieldResponseHeaders) options.Fields.Add(SearchField.ResponseHeaders);
            if (SearchFieldResponseBody) options.Fields.Add(SearchField.ResponseBody);
            return options;
        }

        /// <summary>
        /// 按当前搜索条件遍历会话列表，重建搜索结果集合。会话数较多且逐字段做正则时可能耗时，
        /// 但通常在千级以内会话规模下仍在可接受范围，暂不做额外的异步/节流处理。
        /// </summary>
        private void RunSearch()
        {
            SearchResults.Clear();
            if (string.IsNullOrEmpty(SearchKeyword)) return;

            SearchOptions options = BuildSearchOptions();
            if (options.Fields.Count == 0) return;

            foreach (RequestVo requestVo in Sessions)
            {
                foreach (SearchField field in options.Fields)
                {
                    string text = requestVo.GetSearchableText(field);
                    foreach (var (start, length) in SearchEngine.FindMatches(text, options))
                    {
                        string matchedText = text.Substring(start, length);
                        string snippet = SearchEngine.BuildSnippet(text, start, length);
                        SearchResults.Add(new SearchResultItem(requestVo, field, matchedText, snippet));
                    }
                }
            }
        }

        /// <summary>
        /// 打开/关闭搜索侧边栏。打开时聚焦关键字输入框；关闭时清空搜索结果与高亮
        /// </summary>
        public void ToggleSearchPanel(bool open)
        {
            IsSearchPanelOpen = open;
            if (!open)
            {
                SearchKeyword = "";
                ClearHighlight();
            }
            else
            {
                this.Dispatcher.InvokeAsync(() => SearchKeywordTextBox.Focus());
            }
        }

        /// <summary>
        /// 跳转到指定搜索结果：切换选中会话（触发消息重建），随后在对应编辑器中定位并高亮命中文本
        /// </summary>
        public void JumpToSearchResult(SearchResultItem item)
        {
            if (SelectedSession != item.RequestVo)
            {
                SelectedSession = item.RequestVo;
            }

            // 消息对象刚重建，等界面完成一轮渲染后再定位，确保 TextEditor.Text 已同步为最新内容
            this.Dispatcher.InvokeAsync(() => HighlightAndScrollTo(item), DispatcherPriority.Loaded);
        }

        private void HighlightAndScrollTo(SearchResultItem item)
        {
            var editor = item.IsRequestSide ? RequestEditor : ResponseEditor;
            string text = editor.Text ?? "";
            int index = text.IndexOf(item.MatchedText, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                // 命中位置落在超大内容被截断的部分（编辑框只显示前 MaxDisplayLength 字符），无法在当前视图中定位。
                // 提示用户改用"编辑完整请求体/响应体"弹窗查看完整内容，而不是静默无反应。
                MessageBox.Show("命中内容位于超长文本被截断的部分，无法在当前视图中定位。\n请使用下方“编辑完整请求体/响应体”按钮查看完整内容。",
                    "无法定位", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _highlightRenderer.SetHighlight(editor, index, item.MatchedText.Length);
            editor.ScrollToLine(editor.Document.GetLineByOffset(index).LineNumber);
            editor.Select(index, item.MatchedText.Length);
        }

        /// <summary>
        /// 清空当前的常驻高亮标记（关闭搜索面板时调用）
        /// </summary>
        private void ClearHighlight()
        {
            _highlightRenderer.SetHighlight(RequestEditor, -1, 0);
            _highlightRenderer.SetHighlight(ResponseEditor, -1, 0);
        }

        private void Main_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
            {
                ToggleSearchPanel(true);
                e.Handled = true;
            }
        }

        private void Search_OnClick(object sender, RoutedEventArgs e)
        {
            ToggleSearchPanel(!IsSearchPanelOpen);
        }

        private void CloseSearch_OnClick(object sender, RoutedEventArgs e)
        {
            ToggleSearchPanel(false);
        }

        private void SearchKeywordTextBox_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                ToggleSearchPanel(false);
                e.Handled = true;
            }
        }

        private void SearchResultsListView_OnMouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (SearchResultsListView.SelectedItem is SearchResultItem item)
            {
                JumpToSearchResult(item);
            }
        }

        #endregion

        /// <summary>
        /// 按分组启用状态展平出参与匹配的规则（分组未启用时其下规则整体跳过）
        /// </summary>
        private IEnumerable<RequestMatch> EnabledRequestMatches
        {
            get
            {
                foreach (var group in Groups)
                {
                    if (!group.Enabled) continue;
                    foreach (var rule in group.Rules)
                    {
                        yield return rule;
                    }
                }
            }
        }

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

        /// <summary>
        /// 若给定 session 正是当前选中会话，则用最新数据重建请求/响应展示对象，用于拦截放行、响应完成后的界面刷新
        /// </summary>
        private void RefreshMessagesIfSelected(SessionEventArgs session)
        {
            this.Dispatcher.Invoke(() =>
            {
                if (SelectedSession != null && SelectedSession.Session == session)
                {
                    this.RequestMessage = new RequestMessage(session);
                    this.ResponseMessage = new ResponseMessage(session);
                }
            });
        }

        private RequestMessage _requestMessage;
        private ResponseMessage _responseMessage;


        public RequestMessage RequestMessage
        {
            get => _requestMessage;
            set
            {
                SetField(ref _requestMessage, value);
                SyncRequestEditorText();
            }
        }

        public ResponseMessage ResponseMessage
        {
            get => _responseMessage;
            set
            {
                SetField(ref _responseMessage, value);
                SyncResponseEditorText();
            }
        }

        /// <summary>
        /// AvalonEdit TextEditor 的 Text 不是标准依赖属性、不支持 XAML 双向绑定，需要在内容变化时手动同步。
        /// </summary>
        private void SyncRequestEditorText()
        {
            RequestEditor.Text = RequestMessage?.DisplayMessage ?? "";
        }

        private void SyncResponseEditorText()
        {
            ResponseEditor.Text = ResponseMessage?.DisplayMessage ?? "";
        }


        public Main()
        {
            InitializeComponent();

            _sessionsView = CollectionViewSource.GetDefaultView(Sessions);
            _sessionsView.Filter = SessionFilter;

            this.PreviewKeyDown += Main_OnPreviewKeyDown;

            AppConfig config = ConfigService.Load();
            LoadConfig(config);

            proxyConnect = new ProxyConnect()
            {
                ProxyHost = config.LocalProxyHost,
                ProxyPort = config.LocalProxyPort,
                UpstreamIp = config.UpstreamHost,
                UpstreamPort = config.UpstreamPort ?? -1,
                UpstreamUser = config.UpstreamUser,
                UpstreamPass = config.UpstreamPass,
                UpstreamEnabled = config.UpstreamEnabled
            };

            proxyConnect.AddBeforeRequestTask("URL 打印", 1, session =>
            {
                RequestVo requestVo = new RequestVo(session);
                if (OnlyShowIntercepted && DiscardNonIntercepted)
                {
                    // 硬丢弃模式：先暂缓加入列表，等请求+响应两阶段拦截判定都完成后再决定是否补加入
                    lock (_pendingSessionsLock)
                    {
                        _pendingSessions[session] = requestVo;
                    }
                }
                else
                {
                    this.Dispatcher.Invoke(() => { this.Sessions.Add(requestVo); });
                }

                Console.WriteLine($"{session.HttpClient.Request.Method} {session.HttpClient.Request.Url}");
                return true;
            });

            proxyConnect.AddBeforeRequestTask("请求拦截", 2, session =>
            {
                Request httpClientRequest = session.HttpClient.Request;
                foreach (RequestMatch requestMatch in EnabledRequestMatches)
                {
                    if (!RequestMatch.MatchingRules(httpClientRequest, requestMatch)) continue;

                    MarkIntercepted(session);

                    if (requestMatch.InterceptRequest)
                    {
                        lock (session)
                        {
                            RequestVo requestVo = FindOrAdoptRequestVo(session);
                            this.Dispatcher.Invoke(() => { requestVo.BlockingRequest = true; });
                            Monitor.Wait(session);
                            this.Dispatcher.Invoke(() => { requestVo.BlockingRequest = false; });
                            RefreshMessagesIfSelected(session);
                            break;
                        }
                    }
                }

                return true;
            });

            proxyConnect.AddBeforeResponseTask("刷新详情界面", 0, session =>
            {
                // 此时响应体已通过 GetResponseBody 完整读取并保留（KeepBody=true），是刷新界面最安全可靠的时机；
                // AfterResponse 阶段响应体可能已发送给客户端并被释放，不适合在此读取
                RefreshMessagesIfSelected(session);
                return true;
            });

            proxyConnect.AddBeforeResponseTask("响应拦截", 1, session =>
            {
                Request httpClientRequest = session.HttpClient.Request;
                Response httpClientResponse = session.HttpClient.Response;
                foreach (RequestMatch requestMatch in EnabledRequestMatches)
                {
                    if (!RequestMatch.MatchingRules(httpClientRequest, requestMatch)) continue;

                    MarkIntercepted(session);

                    if (requestMatch.InterceptResponse)
                    {
                        lock (session)
                        {
                            //查找到对应的 RequestVo
                            RequestVo requestVo = FindOrAdoptRequestVo(session);
                            this.Dispatcher.Invoke(() => { requestVo.Blocking = true; });
                            // ProxyConnect.SemaphoreDict[session].Semaphore.Wait();
                            Monitor.Wait(session);
                            this.Dispatcher.Invoke(() => { requestVo.Blocking = false; });
                            RefreshMessagesIfSelected(session);
                            return true;
                        }
                    }
                }

                // 响应阶段判定结束：若该会话既未命中任何拦截规则、也从未被加入列表（硬丢弃模式暂缓中），
                // 则维持丢弃状态（正常转发但不渲染）；FindOrAdoptRequestVo 已在命中时机负责补加入
                lock (_pendingSessionsLock)
                {
                    _pendingSessions.Remove(session);
                }

                return true;
            });

            proxyConnect.AddAfterResponseTask("刷新详情界面", 1, session =>
            {
                RefreshMessagesIfSelected(session);
                return true;
            });

            proxyConnect.AddAfterResponseTask("清理暂缓会话", 2, session =>
            {
                // 兜底清理：若 BeforeResponse 阶段因异常等原因未能移除，避免 _pendingSessions 内存泄漏
                lock (_pendingSessionsLock)
                {
                    _pendingSessions.Remove(session);
                }
                return true;
            });

            proxyConnect.CreateProxyServer();
            proxyConnect.StartProxy();
            proxyConnect.SettingSystemProxy();
        }

        /// <summary>
        /// 判断当前是否已把 Sessions 加入过滤视图（"只展示拦截请求"过滤谓词）
        /// </summary>
        private bool SessionFilter(object obj)
        {
            if (!OnlyShowIntercepted) return true;
            return obj is RequestVo requestVo && requestVo.Intercepted;
        }

        /// <summary>
        /// 将会话标记为已命中拦截规则；若该会话此前因"硬丢弃"暂缓未加入列表，则在此刻补加入
        /// </summary>
        private void MarkIntercepted(SessionEventArgs session)
        {
            RequestVo pendingVo = null;
            lock (_pendingSessionsLock)
            {
                if (_pendingSessions.TryGetValue(session, out RequestVo vo))
                {
                    pendingVo = vo;
                    _pendingSessions.Remove(session);
                }
            }

            this.Dispatcher.Invoke(() =>
            {
                if (pendingVo != null && !this.Sessions.Contains(pendingVo))
                {
                    pendingVo.Intercepted = true;
                    this.Sessions.Add(pendingVo); // Add 触发 CollectionChanged，过滤视图会自动重新求值该项
                }
                else
                {
                    RequestVo requestVo = this.Sessions.FirstOrDefault(x => x.Session == session);
                    if (requestVo != null && !requestVo.Intercepted)
                    {
                        requestVo.Intercepted = true;
                        // 该会话已在列表中，仅属性变化不会被 ICollectionView 自动感知，需手动刷新过滤视图
                        _sessionsView.Refresh();
                    }
                }
            });
        }

        /// <summary>
        /// 拦截阻塞前查找对应 RequestVo；若因"硬丢弃"暂缓尚未加入列表，则先补加入（此刻必然已命中拦截规则）
        /// </summary>
        private RequestVo FindOrAdoptRequestVo(SessionEventArgs session)
        {
            RequestVo result = null;
            this.Dispatcher.Invoke(() =>
            {
                result = this.Sessions.FirstOrDefault(x => x.Session == session);
                if (result == null)
                {
                    lock (_pendingSessionsLock)
                    {
                        if (_pendingSessions.TryGetValue(session, out RequestVo pendingVo))
                        {
                            _pendingSessions.Remove(session);
                            pendingVo.Intercepted = true;
                            this.Sessions.Add(pendingVo);
                            result = pendingVo;
                        }
                    }
                }
            });
            return result;
        }

        /// <summary>
        /// 将读取到的配置应用到界面绑定的分组集合
        /// </summary>
        private void LoadConfig(AppConfig config)
        {
            Groups.Clear();
            foreach (var group in config.Groups)
            {
                Groups.Add(group);
            }
        }

        /// <summary>
        /// 将当前本地/上游代理设置与拦截规则保存到本地配置文件
        /// </summary>
        public void SaveConfig()
        {
            var config = new AppConfig
            {
                LocalProxyHost = proxyConnect.ProxyHost,
                LocalProxyPort = proxyConnect.ProxyPort,
                UpstreamEnabled = proxyConnect.UpstreamEnabled,
                UpstreamHost = proxyConnect.UpstreamIp,
                UpstreamPort = proxyConnect.UpstreamPort == -1 ? null : proxyConnect.UpstreamPort,
                UpstreamUser = proxyConnect.UpstreamUser,
                UpstreamPass = proxyConnect.UpstreamPass,
                Groups = Groups.ToList()
            };
            ConfigService.Save(config);
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
            BlockingSettingControl blockingSettingControl = new BlockingSettingControl(Groups);
            DialogHelper.ShowDialogAsync<bool>("添加拦截规则",blockingSettingControl,true,900D,600D,onClose: w =>
            {
                blockingSettingControl.UpdateGroups();
                SaveConfig();
                Console.WriteLine("拦截规则保存成功");
            });
        }

        private void discharged_OnClick(object sender, RoutedEventArgs e)
        {
            foreach (RequestVo requestVo in Sessions)
            {
                lock (requestVo.Session)
                {
                    Monitor.PulseAll(requestVo.Session);
                }
            }
        }

        /// <summary>
        /// 上行（请求阶段）拦截放行：将编辑后的请求内容解析回真实 Request 对象后放行
        /// </summary>
        private void PassRequest_OnClick(object sender, RoutedEventArgs e)
        {
            if (SelectedSession == null)
            {
                return;
            }

            Request request = SelectedSession.Session.HttpClient.Request;
            try
            {
                // 内容过大截断展示时编辑框为只读，此时用户只能通过"编辑完整请求体"弹窗修改 ReqBody，主编辑框内容与 AllMessage 始终一致
                string textToParse = RequestMessage != null && RequestMessage.IsTruncated
                    ? RequestMessage.AllMessage
                    : this.RequestEditor.Text;
                ParseHttpRequestText(textToParse, request);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("解析请求出错: " + ex);
            }
            finally
            {
                lock (SelectedSession.Session)
                {
                    Monitor.Pulse(SelectedSession.Session);
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
            if (SelectedSession==null)
            {
                return;
            }
            // 内容过大截断展示时编辑框为只读，此时用户只能通过"编辑完整响应体"弹窗修改 RespBody，主编辑框内容与 AllMessage 始终一致
            string responseText = ResponseMessage != null && ResponseMessage.IsTruncated
                ? ResponseMessage.AllMessage
                : ResponseEditor.Text;
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

        /// <summary>
        /// 将编辑框中的原始请求文本解析回 Request 对象（首行 METHOD URL HTTP/x.x，随后 headers，空行后为 body）
        /// </summary>
        private void ParseHttpRequestText(string requestText, Request request)
        {
            if (requestText == null) requestText = string.Empty;

            int headerBodySep = requestText.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            string sepToken = "\r\n\r\n";
            if (headerBodySep == -1)
            {
                headerBodySep = requestText.IndexOf("\n\n", StringComparison.Ordinal);
                sepToken = "\n\n";
            }

            int firstLineEnd = requestText.IndexOf("\r\n", StringComparison.Ordinal);
            int firstLineNewlineLen = 2;
            if (firstLineEnd == -1)
            {
                firstLineEnd = requestText.IndexOf("\n", StringComparison.Ordinal);
                firstLineNewlineLen = 1;
            }

            if (firstLineEnd == -1)
            {
                firstLineEnd = Math.Min(requestText.Length, headerBodySep >= 0 ? headerBodySep : requestText.Length);
                firstLineNewlineLen = 0;
            }

            string requestLine = requestText.Substring(0, Math.Min(requestText.Length, firstLineEnd));

            if (!string.IsNullOrWhiteSpace(requestLine))
            {
                var parts = requestLine.Split(new[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
                try
                {
                    if (parts.Length >= 1)
                    {
                        request.Method = parts[0];
                    }

                    if (parts.Length >= 2)
                    {
                        string url = parts[1];
                        if (Uri.TryCreate(url, UriKind.Absolute, out Uri absoluteUri))
                        {
                            request.RequestUri = absoluteUri;
                        }
                        else if (Uri.TryCreate(request.RequestUri, url, out Uri combinedUri))
                        {
                            request.RequestUri = combinedUri;
                        }
                    }
                }
                catch
                {
                    // 忽略解析错误，保持已有 request 字段
                }
            }

            string headersText = string.Empty;
            string bodyText = string.Empty;
            if (headerBodySep >= 0)
            {
                int headersStart = firstLineEnd + firstLineNewlineLen;
                headersText = headersStart < headerBodySep
                    ? requestText.Substring(headersStart, headerBodySep - headersStart)
                    : string.Empty;

                int bodyStart = headerBodySep + sepToken.Length;
                bodyText = bodyStart < requestText.Length ? requestText.Substring(bodyStart) : string.Empty;
            }
            else if (firstLineEnd < requestText.Length)
            {
                int restStart = firstLineEnd + firstLineNewlineLen;
                if (restStart < requestText.Length)
                {
                    string rest = requestText.Substring(restStart);
                    if (rest.Contains(":"))
                    {
                        headersText = rest;
                    }
                    else
                    {
                        bodyText = rest;
                    }
                }
            }

            if (!string.IsNullOrEmpty(headersText))
            {
                try
                {
                    var clearMethod = request.Headers.GetType().GetMethod("Clear");
                    clearMethod?.Invoke(request.Headers, null);
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
                        try
                        {
                            request.Headers.AddHeader(key, value);
                        }
                        catch
                        {
                            // 容忍单条 header 添加失败，不影响其余解析
                        }
                    }
                }
            }

            if (request.HasBody || !string.IsNullOrEmpty(bodyText))
            {
                try
                {
                    SelectedSession.Session.SetRequestBodyString(bodyText);
                }
                catch
                {
                    // 如果 SetRequestBodyString 失败，不要抛出到上层
                }
            }
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
            string? result = ShowBodyEditDialog("输入请求体", "");
            if (result != null)
            {
                ResponseMessage.RespBody = result;
                SyncResponseEditorText();
            }
        }

        /// <summary>
        /// 请求体过大被截断展示时，弹窗编辑完整请求体（避免在主编辑框里渲染超大文本导致卡顿）
        /// </summary>
        private void EditRequestBody_OnClick(object sender, RoutedEventArgs e)
        {
            if (RequestMessage == null) return;

            string? result = ShowBodyEditDialog("编辑完整请求体", RequestMessage.ReqBody ?? "");
            if (result != null)
            {
                RequestMessage.ReqBody = result;
                SyncRequestEditorText();
            }
        }

        /// <summary>
        /// 响应体过大被截断展示时，弹窗编辑完整响应体（避免在主编辑框里渲染超大文本导致卡顿）
        /// </summary>
        private void EditResponseBody_OnClick(object sender, RoutedEventArgs e)
        {
            if (ResponseMessage == null) return;

            string? result = ShowBodyEditDialog("编辑完整响应体", ResponseMessage.RespBody ?? "");
            if (result != null)
            {
                ResponseMessage.RespBody = result;
                SyncResponseEditorText();
            }
        }

        /// <summary>
        /// 弹出一个大文本框窗口用于编辑完整 body，确定返回编辑后的文本，取消返回 null
        /// </summary>
        private string? ShowBodyEditDialog(string title, string initialText)
        {
            Window dialog = new Window
            {
                Title = title,
                Width = 700,
                Height = 500,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this), // 当前窗口作为父窗口
                ResizeMode = ResizeMode.CanResize
            };

            // 布局
            Grid grid = new Grid { Margin = new Thickness(10) };
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // 输入框
            TextBox textBox = new TextBox
            {
                Text = initialText,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.NoWrap,
                FontFamily = new FontFamily("Consolas")
            };
            ScrollViewer scrollViewer = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = textBox
            };
            Grid.SetRow(scrollViewer, 0);
            grid.Children.Add(scrollViewer);

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

            return dialog.ShowDialog() == true ? textBox.Text : null;
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

            SaveConfig();

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

        /// <summary>
        /// 由 BlockingSettingControl 在规则分组编辑完成后回写，替换当前生效的分组集合
        /// </summary>
        public void ResetGroups(List<RuleGroup> groups)
        {
            Groups.Clear();
            foreach (var group in groups)
            {
                Groups.Add(group);
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