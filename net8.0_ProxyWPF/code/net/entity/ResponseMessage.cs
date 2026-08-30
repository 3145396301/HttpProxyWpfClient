using System;
using net8._0_ProxyWPF.code.@base;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Http;

namespace net8._0_ProxyWPF.code.net.entity
{
    public class ResponseMessage : BindableBase
    {
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

        }
    }
}