using System;
using net8._0_ProxyWPF.code.@base;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Http;
using Titanium.Web.Proxy.Models;

namespace net8._0_ProxyWPF.code.net.entity
{
    public class RequestVo : BindableBase
    {
        SessionEventArgs _session;
        private long? _bodySize;
        private long _clientConnectionId;
        private Exception _exception;
        private string _host;
        private int _processId;
        private string _protocol;
        private long _receivedDataCount;
        private long _sentDataCount;
        private Guid _serverConnectionId;
        private string _statusCode;
        private string _url;
        private string _method;
        private bool _blocking;
        private bool _blockingRequest;
        private bool _intercepted;

        public long? BodySize
        {
            get=> _bodySize;
            set=> SetProperty(ref _bodySize, value);
        }
        public long ClientConnectionId
        {
            get=> _clientConnectionId;
            set=> SetProperty(ref _clientConnectionId, value);
        }
        public Exception Exception
        {
            get=> _exception;
            set=> SetProperty(ref _exception, value);
        }
        public string Host
        {
            get=> _host;
            set=> SetProperty(ref _host, value);
        }
        public int ProcessId
        {
            get=> _processId;
            set=> SetProperty(ref _processId, value);
        }
        public string Protocol
        {
            get=> _protocol;
            set=> SetProperty(ref _protocol, value);
        }
        public long ReceivedDataCount
        {
            get=> _receivedDataCount;
            set => SetProperty(ref _receivedDataCount, value);
        }
        public long SentDataCount
        {
            get=> _sentDataCount;
            set=> SetProperty(ref _sentDataCount, value);
        }
        public Guid ServerConnectionId
        {
            get=> _serverConnectionId;
            set=> SetProperty(ref _serverConnectionId, value);
        }
        public string StatusCode
        {
            get=> _statusCode;
            set=> SetProperty(ref _statusCode, value);
        }
        public string Url
        {
            get=> _url;
            set=> SetProperty(ref _url, value);
        }
        public string Method
        {
            get=> _method;
            set=> SetProperty(ref _method, value);
        }

        public SessionEventArgs Session
        {
            get=> _session;
        }
        public bool Blocking
        {
            get=> _blocking;
            set=> SetProperty(ref _blocking, value);
        }

        /// <summary>
        /// 是否处于请求阶段（上行）拦截暂停中
        /// </summary>
        public bool BlockingRequest
        {
            get=> _blockingRequest;
            set=> SetProperty(ref _blockingRequest, value);
        }

        /// <summary>
        /// 该会话是否命中了任意已启用分组下的启用拦截规则（请求或响应阶段任一命中即为 true）。
        /// 用于"只展示拦截请求"过滤视图的判定依据。
        /// </summary>
        public bool Intercepted
        {
            get=> _intercepted;
            set=> SetProperty(ref _intercepted, value);
        }




        public RequestVo(SessionEventArgs session)
        {
            _session = session;
            if (session.HttpClient.Request.HasBody)
            {
                BodySize = session.HttpClient.Request.Body.Length;
            }else
            {
                BodySize = 0;
            }

            ClientConnectionId = session.ClientConnectionId;
            Exception = session.Exception;
            Host = session.HttpClient.Request.Host;
            if (session.HttpClient.Request.HttpVersion.ToString()==new Version(2,0).ToString())
            {
                Host = session.HttpClient.Request.RequestUri.Authority;
            }
            ProcessId = session.HttpClient.ProcessId.Value;
            Protocol = session.HttpClient.IsHttps?"https":"http";
            Method = session.HttpClient.Request.Method;
            Url = session.HttpClient.Request.RequestUri.AbsolutePath;
        }

        /// <summary>
        /// 取出指定搜索字段对应的原始文本，用于搜索引擎匹配。读取响应体等可能失败的内容时容错返回空字符串，
        /// 不影响其余字段的搜索（例如响应尚未完成解析时）。
        /// </summary>
        public string GetSearchableText(SearchField field)
        {
            try
            {
                switch (field)
                {
                    case SearchField.Host:
                        return Host ?? "";
                    case SearchField.Url:
                        return Session.HttpClient.Request.RequestUri.AbsoluteUri;
                    case SearchField.Method:
                        return Method ?? "";
                    case SearchField.StatusCode:
                        return Session.HttpClient.Response.StatusCode.ToString();
                    case SearchField.RequestHeaders:
                        return HeadersToText(Session.HttpClient.Request.Headers);
                    case SearchField.RequestBody:
                        return Session.HttpClient.Request.HasBody
                            ? System.Text.Encoding.UTF8.GetString(Session.HttpClient.Request.Body)
                            : "";
                    case SearchField.ResponseHeaders:
                        return HeadersToText(Session.HttpClient.Response.Headers);
                    case SearchField.ResponseBody:
                        return Session.HttpClient.Response.HasBody
                            ? ResponseMessage.DecodeResponseBody(Session.HttpClient.Response)
                            : "";
                    default:
                        return "";
                }
            }
            catch
            {
                return "";
            }
        }

        private static string HeadersToText(HeaderCollection headers)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (HttpHeader header in headers)
            {
                sb.Append(header.Name).Append(": ").Append(header.Value).Append("\r\n");
            }
            return sb.ToString();
        }
    }
}
