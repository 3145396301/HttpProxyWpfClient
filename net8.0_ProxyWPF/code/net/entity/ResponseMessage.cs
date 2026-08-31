using System;
using net8._0_ProxyWPF.code.@base;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Http;

namespace net8._0_ProxyWPF.code.net.entity
{
    public class ResponseMessage : BindableBase
    {
        /// <summary>
        /// 用于界面展示的最大字符数，超出后截断显示，避免 WPF TextBox 渲染超大文本导致 UI 卡顿
        /// </summary>
        public const int MaxDisplayLength = 200_000;

        private string _respRow;
        private string _respHeaders;
        private string _respBody;
        private Exception _error;
        public string RespRow
        {
            get => _respRow;
            set => SetProperty(ref _respRow, value);
        }
        public string RespHeaders
        {
            get => _respHeaders;
            set => SetProperty(ref _respHeaders, value);
        }

        public string RespBody
        {
            get => _respBody;
            set
            {
                SetProperty(ref _respBody, value);
                OnPropertyChanged(nameof(AllMessage));
                OnPropertyChanged(nameof(DisplayMessage));
            }
        }

        public Exception Error
        {
            get => _error;
            set
            {
                SetProperty(ref _error, value);
                OnPropertyChanged(nameof(HasError));
                OnPropertyChanged(nameof(ErrorText));
            }
        }

        public bool HasError => Error != null;

        public string ErrorText => Error != null ? $"请求未完成: {Error.Message}" : "";

        /// <summary>
        /// 完整的响应原文（首行+body），用于放行时解析，不用于界面展示
        /// </summary>
        public string AllMessage
        {
            get
            {
                return $"{RespRow}{RespBody??""}";
            }
            set
            {

            }
        }

        /// <summary>
        /// 是否因内容过大而被截断展示
        /// </summary>
        public bool IsTruncated { get; private set; }

        /// <summary>
        /// 界面展示用文本：内容过大时截断，避免 TextBox 一次性渲染超大文本导致卡顿
        /// </summary>
        public string DisplayMessage
        {
            get
            {
                string full = AllMessage;
                if (full.Length <= MaxDisplayLength)
                {
                    IsTruncated = false;
                    return full;
                }

                IsTruncated = true;
                return full.Substring(0, MaxDisplayLength)
                       + $"\r\n\r\n[内容过大，已截断显示（{MaxDisplayLength:N0}/{full.Length:N0} 字符），编辑框已切换为只读，请使用下方“编辑完整响应体”按钮修改]";
            }
        }

        public ResponseMessage(SessionEventArgs session):this(session.HttpClient.Response)
        {
            if (session.Exception != null)
            {
                Error = session.Exception;
            }
        }

        public ResponseMessage(Response resp)
        {
            RespRow = resp.HeaderText;
            if (resp.HasBody)
            {
                try
                {
                    RespBody = resp.BodyString;
                }
                catch (Exception e)
                {
                    Error = new Exception("响应主体尚未接受完整、或尚未解析完成，请稍后切换会话重试。", e);
                }

            }

            if (Error == null && resp.StatusCode == 0)
            {
                Error = new Exception("代理未收到有效响应，连接可能在建立或传输过程中被中断（例如证书验证失败、连接被重置或超时）。");
            }

        }
    }
}