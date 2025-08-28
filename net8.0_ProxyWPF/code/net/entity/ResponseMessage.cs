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
                    RespBody = "响应主体尚未接受完整、或尚未解析完成，请稍后切换会话重试。";
                }

            }

        }
    }
}