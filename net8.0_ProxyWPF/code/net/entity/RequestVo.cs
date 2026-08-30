using System;
using net8._0_ProxyWPF.code.@base;
using Titanium.Web.Proxy.EventArguments;

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


    }
}