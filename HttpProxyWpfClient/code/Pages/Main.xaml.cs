using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using HttpProxyWpfClient.code.net;
using HttpProxyWpfClient.code.net.entity;
using HttpProxyWpfClient.code.Pages.BlockingSetting;
using HttpProxyWpfClient.code.Pages.Util;
using HttpProxyWpfClient.code.Services;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Http;

namespace HttpProxyWpfClient.code.Pages
{
    public partial class Main : Page, INotifyPropertyChanged
    {
        private ProxyConnect proxyConnect;
        private readonly SearchHighlightRenderer _highlightRenderer = new();

        private const double ContentFontSizeMin = 8;
        private const double ContentFontSizeMax = 40;
        private double _requestContentFontSize = 13;
        private double _responseContentFontSize = 13;
        private double _editBodyFontSize = 13;

        /// <summary>
        /// 会话列表单列：键、对应的 GridViewColumn、当前显隐与默认宽度
        /// </summary>
        private sealed class SessionColumnDef
        {
            public string Key = "";
            public GridViewColumn Column = null!;
            public bool Visible = true;
            public double DefaultWidth = 100;
        }

        private readonly List<SessionColumnDef> _sessionColumns = new();

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
            foreach (SearchResultItem result in SessionSearchService.Search(Sessions, BuildSearchOptions()))
                SearchResults.Add(result);
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

            int index = LocateMatch(item, text);
            if (index >= 0)
            {
                LocateInEditor(editor, index, item.MatchedText.Length);
                return;
            }

            // 命中位置落在超大内容被截断的部分（编辑框只显示前 MaxDisplayLength 字符），无法在当前视图中定位。
            // 提供"强制展示"入口，并在执行前二次确认大文本渲染可能造成的卡顿
            if (!PromptForceDisplay())
            {
                return;
            }

            var confirm = MessageBox.Show("强制展示完整内容可能造成界面卡顿，是否继续？",
                "确认", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            ForceDisplay(item);
        }

        /// <summary>
        /// 在渲染文本中定位命中位置：请求/响应体字段用搜索时记录的精确定位（加上头部长度换算到编辑器坐标），
        /// 其余字段回退到全文 IndexOf；找不到返回 -1。
        /// </summary>
        private int LocateMatch(SearchResultItem item, string text)
        {
            // 体字段的搜索来源是纯 body，而渲染文本是 头+body，需加上头部长度换算
            if (item.Field is SearchField.RequestBody or SearchField.ResponseBody)
            {
                int headerLen = item.IsRequestSide
                    ? (RequestMessage?.ReqRow?.Length ?? 0)
                    : (ResponseMessage?.RespRow?.Length ?? 0);
                int candidate = item.StartOffset + headerLen;
                if (candidate >= 0
                    && candidate + item.MatchedText.Length <= text.Length
                    && string.Compare(text, candidate, item.MatchedText, 0, item.MatchedText.Length, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return candidate;
                }
            }

            return text.IndexOf(item.MatchedText, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 高亮并滚动到编辑器中的命中区间。用 BringCaretToView 兼顾横向滚动，
        /// 避免整段内容为单行（无换行）时 ScrollToLine 无法定位的问题。
        /// </summary>
        private void LocateInEditor(TextEditor editor, int index, int length)
        {
            _highlightRenderer.SetHighlight(editor, index, length);
            editor.Select(index, length);
            var line = editor.Document.GetLineByOffset(index);
            editor.ScrollToLine(line.LineNumber);
            editor.TextArea.Caret.BringCaretToView();
        }

        /// <summary>
        /// 命中内容位于截断部分时的提示弹窗，提供"强制展示"按钮；返回是否点击了"强制展示"
        /// </summary>
        private bool PromptForceDisplay()
        {
            var panel = new StackPanel { Margin = new Thickness(15) };
            panel.Children.Add(new TextBlock
            {
                Text = "命中内容位于超长文本被截断的部分，当前编辑框无法定位到该位置。\n\n" +
                       "点击“强制展示”将在编辑框中加载完整内容并定位到命中位置；\n" +
                       "超大文本渲染可能导致界面卡顿，也可以关闭本提示，改用下方“编辑完整请求体/响应体”按钮查看。",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 15)
            });

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            var cancelBtn = new Button { Content = "取消", Width = 75, Margin = new Thickness(5, 0, 0, 0) };
            var forceBtn = new Button { Content = "强制展示", Width = 90, Margin = new Thickness(5, 0, 0, 0) };
            buttonPanel.Children.Add(cancelBtn);
            buttonPanel.Children.Add(forceBtn);
            panel.Children.Add(buttonPanel);

            forceBtn.Click += (s, e) => DialogHelper.CloseWithResult<bool>(Window.GetWindow(forceBtn), true);
            cancelBtn.Click += (s, e) => DialogHelper.CloseWithResult<bool>(Window.GetWindow(cancelBtn), false);

            var dialogTask = DialogHelper.ShowDialogAsync<bool>("无法定位", panel, true, 500, 240);
            return dialogTask.GetAwaiter().GetResult() == true;
        }

        /// <summary>
        /// 强制在编辑框中加载完整内容并定位到命中位置（超大文本渲染可能卡顿）。
        /// 超大文本的布局是异步完成的，定位操作延迟到渲染结束后再执行，否则会定位不到正确位置。
        /// </summary>
        private void ForceDisplay(SearchResultItem item)
        {
            var editor = item.IsRequestSide ? RequestEditor : ResponseEditor;
            string fullText = item.IsRequestSide ? RequestMessage?.AllMessage : ResponseMessage?.AllMessage;
            if (string.IsNullOrEmpty(fullText))
            {
                return;
            }

            editor.Text = fullText;
            int index = LocateMatch(item, fullText);
            if (index < 0)
            {
                return;
            }

            this.Dispatcher.InvokeAsync(() => LocateInEditor(editor, index, item.MatchedText.Length),
                DispatcherPriority.ApplicationIdle);
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

        private void ReSearch_OnClick(object sender, RoutedEventArgs e)
        {
            // 关键词/选项变化会自动触发搜索，此按钮用于会话列表有新数据后按当前条件重新搜索
            RunSearch();
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
                if (!SetField(ref _selectedSession, value))
                {
                    return;
                }
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
            this.Dispatcher.BeginInvoke(() =>
            {
                if (SelectedSession != null && SelectedSession.Session == session)
                {
                    this.RequestMessage = new RequestMessage(session);
                    this.ResponseMessage = new ResponseMessage(session);
                }
            });
        }

        /// <summary>
        /// 响应完成后填充会话列表的响应类型/长度列（在代理响应回调线程上调用，切回 UI 线程更新）
        /// </summary>
        private void UpdateResponseInfo(SessionEventArgs session)
        {
            this.Dispatcher.BeginInvoke(() =>
            {
                RequestVo requestVo = this.Sessions.FirstOrDefault(x => x.Session == session);
                if (requestVo == null)
                {
                    lock (_pendingSessionsLock)
                    {
                        if (_pendingSessions.TryGetValue(session, out RequestVo pendingVo))
                        {
                            pendingVo.CaptureResponse();
                        }
                    }
                    return;
                }

                requestVo.CaptureResponse();
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

            _sessionColumns.AddRange(new[]
            {
                new SessionColumnDef { Key = "Up", Column = ColUp, DefaultWidth = 40 },
                new SessionColumnDef { Key = "Down", Column = ColDown, DefaultWidth = 40 },
                new SessionColumnDef { Key = "Host", Column = ColHost, DefaultWidth = 120 },
                new SessionColumnDef { Key = "Protocol", Column = ColProtocol, DefaultWidth = 70 },
                new SessionColumnDef { Key = "Method", Column = ColMethod, DefaultWidth = 70 },
                new SessionColumnDef { Key = "Path", Column = ColPath, DefaultWidth = 200 },
                new SessionColumnDef { Key = "ResponseContentType", Column = ColContentType, DefaultWidth = 130 },
                new SessionColumnDef { Key = "ResponseLength", Column = ColContentLength, DefaultWidth = 90 }
            });

            _sessionsView = CollectionViewSource.GetDefaultView(Sessions);
            _sessionsView.Filter = SessionFilter;

            this.PreviewKeyDown += Main_OnPreviewKeyDown;

            AppConfig config = ConfigService.Load();
            LoadConfig(config);
            LoadSessionColumnLayout(config);
            ApplyRequestFontSize(config.RequestContentFontSize);
            ApplyResponseFontSize(config.ResponseContentFontSize);
            _editBodyFontSize = Math.Clamp(config.EditBodyFontSize, ContentFontSizeMin, ContentFontSizeMax);

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

            ConfigureProxyPipeline();
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
            ReleaseBlockedSessions();
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
            // 右键表头：由 ContextMenuOpening 处理列显隐菜单，这里不选中行
            if (FindVisualParent<GridViewColumnHeader>(e.OriginalSource as DependencyObject) != null)
            {
                return;
            }

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

        /// <summary>
        /// 右键表头时取消默认上下文菜单，改为弹出列显隐菜单
        /// </summary>
        private void SessionList_OnContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (FindVisualParent<GridViewColumnHeader>(e.OriginalSource as DependencyObject) == null)
            {
                return;
            }

            e.Handled = true;
            ShowColumnVisibilityMenu();
        }

        /// <summary>
        /// 弹出会话列表列显隐菜单（勾选/取消即展示/隐藏对应列），变更后保存配置
        /// </summary>
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

        private void ParseHttpRequestText(string requestText, Request request)
        {
            ParsedHttpMessage parsed = HttpMessageParser.Parse(requestText);
            HttpMessageParser.ApplyRequest(parsed, request);
            if (!string.IsNullOrEmpty(parsed.BodyText) || request.HasBody)
            {
                SelectedSession?.Session.SetRequestBodyString(parsed.BodyText);
            }
        }

        private void ParseHttpResponseText(string responseText, Response response)
        {
            ParsedHttpMessage parsed = HttpMessageParser.Parse(responseText);
            HttpMessageParser.ApplyResponse(parsed, response);
            if (SelectedSession == null) return;

            byte[] bodyBytes = HttpMessageParser.GetBodyBytes(parsed, response);
            if (bodyBytes.Length > 0)
            {
                SelectedSession.Session.SetResponseBody(bodyBytes);
            }
        }

        private void ModifyRequestBody_OnClick(object sender, RoutedEventArgs e)
        {
            string? result = ShowBodyEditDialog("输入请求体", "");
            if (result != null)
            {
                RequestMessage!.ReqBody = result;
                SyncRequestEditorText();
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
                ResizeMode = ResizeMode.CanResize,
                MinWidth = 520,
                MinHeight = 300
            };

            // 布局：编辑区 / 搜索面板 / 按钮区
            Grid grid = new Grid { Margin = new Thickness(10) };
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // 输入框（用 TextBox 自带滚动，不用外层 ScrollViewer 包裹，
            // 否则 ScrollToLine 操作的是 TextBox 内部滚动条，外层不动导致定位失效）
            TextBox textBox = new TextBox
            {
                Text = initialText,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.NoWrap,
                FontFamily = new FontFamily("Consolas"),
                FontSize = _editBodyFontSize
            };
            ScrollViewer.SetHorizontalScrollBarVisibility(textBox, ScrollBarVisibility.Auto);
            ScrollViewer.SetVerticalScrollBarVisibility(textBox, ScrollBarVisibility.Auto);
            textBox.PreviewMouseWheel += EditBodyTextBox_OnPreviewMouseWheel;
            Grid.SetRow(textBox, 0);
            grid.Children.Add(textBox);

            // Ctrl+F 搜索面板（默认隐藏）
            StackPanel searchPanel = BuildBodySearchPanel(textBox, out TextBox searchInput);
            Grid.SetRow(searchPanel, 1);
            grid.Children.Add(searchPanel);

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

            Grid.SetRow(panel, 2);
            grid.Children.Add(panel);

            dialog.Content = grid;

            // 挂在 dialog 上，无论焦点在编辑框还是搜索框里，Ctrl+F 都能唤起/聚焦搜索
            dialog.PreviewKeyDown += (s, args) =>
            {
                if (args.Key == Key.F && args.KeyboardDevice.Modifiers == ModifierKeys.Control)
                {
                    searchPanel.Visibility = Visibility.Visible;
                    searchInput.Focus();
                    searchInput.SelectAll();
                    args.Handled = true;
                }
            };

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

        /// <summary>
        /// 一次搜索命中的位置信息，Snippet 供结果列表展示
        /// </summary>
        private sealed class BodySearchMatch
        {
            public int Start;
            public int Length;
            public string Snippet = "";

            public override string ToString() => Snippet;
        }

        /// <summary>
        /// 构建"编辑完整响应体"弹窗的搜索面板：包含/正则两种模式，列表展示命中项，点击定位并高亮编辑框内容
        /// </summary>
        private StackPanel BuildBodySearchPanel(TextBox bodyTextBox, out TextBox searchInput)
        {
            TextBox searchInputLocal = new TextBox { Width = 200, VerticalContentAlignment = VerticalAlignment.Center };
            searchInput = searchInputLocal;
            ComboBox modeCombo = new ComboBox { Width = 100, Margin = new Thickness(5, 0, 0, 0) };
            modeCombo.Items.Add("包含");
            modeCombo.Items.Add("正则表达式");
            modeCombo.SelectedIndex = 0;

            Button searchBtn = new Button { Content = "搜索", Width = 60, Margin = new Thickness(5, 0, 0, 0) };
            TextBlock countTextLocal = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
                MaxWidth = 200,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            // Dock 右侧固定显示，避免被左侧内容挤出可视区；Padding 清零防止 × 字形被裁切
            Button closeBtn = new Button
            {
                Content = "×",
                Width = 28,
                Height = 24,
                Padding = new Thickness(0),
                Margin = new Thickness(5, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            StackPanel controlsRow = new StackPanel { Orientation = Orientation.Horizontal };
            controlsRow.Children.Add(searchInputLocal);
            controlsRow.Children.Add(modeCombo);
            controlsRow.Children.Add(searchBtn);
            controlsRow.Children.Add(countTextLocal);

            DockPanel inputRow = new DockPanel();
            DockPanel.SetDock(closeBtn, Dock.Right);
            inputRow.Children.Add(closeBtn);
            inputRow.Children.Add(controlsRow);

            ListBox resultListLocal = new ListBox
            {
                MaxHeight = 150,
                Margin = new Thickness(0, 5, 0, 0),
                FontFamily = new FontFamily("Consolas"),
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };

            StackPanel panel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(0, 8, 0, 0),
                Visibility = Visibility.Collapsed
            };
            panel.Children.Add(inputRow);
            panel.Children.Add(resultListLocal);

            void RunSearch()
            {
                resultListLocal.Items.Clear();
                countTextLocal.Text = "";

                string pattern = searchInputLocal.Text;
                if (pattern.Length == 0) return;

                string text = bodyTextBox.Text;
                List<BodySearchMatch> matches = new List<BodySearchMatch>();
                try
                {
                    if (modeCombo.SelectedIndex == 1)
                    {
                        foreach (Match m in Regex.Matches(text, pattern))
                        {
                            if (m.Length == 0) continue; // 空匹配无限多，跳过
                            matches.Add(new BodySearchMatch { Start = m.Index, Length = m.Length });
                        }
                    }
                    else
                    {
                        int idx = 0;
                        while ((idx = text.IndexOf(pattern, idx, StringComparison.Ordinal)) >= 0)
                        {
                            matches.Add(new BodySearchMatch { Start = idx, Length = pattern.Length });
                            idx += pattern.Length;
                        }
                    }
                }
                catch (ArgumentException)
                {
                    countTextLocal.Text = "正则表达式无效";
                    return;
                }

                foreach (BodySearchMatch m in matches)
                {
                    int contextStart = Math.Max(0, m.Start - 20);
                    int contextLen = Math.Min(text.Length - contextStart, m.Length + 40);
                    string snippet = text.Substring(contextStart, contextLen)
                        .Replace("\r", "").Replace("\n", "⏎").Replace("\t", "⇥");
                    int line = bodyTextBox.GetLineIndexFromCharacterIndex(m.Start);
                    m.Snippet = $"L{(line >= 0 ? line + 1 : "?")}  {snippet}";
                    resultListLocal.Items.Add(m);
                }

                countTextLocal.Text = $"共 {matches.Count} 个结果";
            }

            searchBtn.Click += (s, args) => RunSearch();
            searchInputLocal.KeyDown += (s, args) =>
            {
                if (args.Key == Key.Enter)
                {
                    RunSearch();
                    args.Handled = true;
                }
                else if (args.Key == Key.Escape)
                {
                    panel.Visibility = Visibility.Collapsed;
                    bodyTextBox.Focus();
                    args.Handled = true;
                }
            };
            modeCombo.KeyDown += (s, args) =>
            {
                if (args.Key == Key.Enter) RunSearch();
            };

            // 点击命中项：滚动到对应行并高亮选中，焦点切回编辑框
            void LocateMatch(BodySearchMatch m)
            {
                bodyTextBox.Focus();
                bodyTextBox.Select(m.Start, m.Length);
                int line = bodyTextBox.GetLineIndexFromCharacterIndex(m.Start);
                if (line >= 0) bodyTextBox.ScrollToLine(line);

                // 命中位置可能在横向滚动可视区外，把匹配段起点滚动到左侧留 20px 余量
                Rect rect = bodyTextBox.GetRectFromCharacterIndex(m.Start, false);
                if (rect != Rect.Empty &&
                    bodyTextBox.Template.FindName("PART_ContentHost", bodyTextBox) is ScrollViewer sv)
                {
                    sv.ScrollToHorizontalOffset(Math.Max(0, sv.HorizontalOffset + rect.Left - 20));
                }
            }

            resultListLocal.SelectionChanged += (s, args) =>
            {
                if (resultListLocal.SelectedItem is BodySearchMatch m) LocateMatch(m);
            };
            // 重复点击同一项不会触发 SelectionChanged，用鼠标事件兜底
            resultListLocal.PreviewMouseLeftButtonUp += (s, args) =>
            {
                if (resultListLocal.SelectedItem is BodySearchMatch m) LocateMatch(m);
            };

            closeBtn.Click += (s, args) =>
            {
                panel.Visibility = Visibility.Collapsed;
                bodyTextBox.Focus();
            };

            // 编辑内容变化后旧的命中位置失效，清空结果避免定位到错误位置
            bodyTextBox.TextChanged += (s, args) =>
            {
                resultListLocal.Items.Clear();
                countTextLocal.Text = "";
            };

            return panel;
        }
        
    }
}
