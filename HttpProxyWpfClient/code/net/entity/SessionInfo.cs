using System.Collections.Generic;
using System.Threading;
using HttpProxyWpfClient.code.@base;
using Titanium.Web.Proxy.EventArguments;

namespace HttpProxyWpfClient.code.net.entity
{
    public class SessionInfo : BindableBase
    {
        SessionEventArgs _session;
        SemaphoreSlim _semaphore;
        int? _status;
        string _statusDescription;
        Dictionary<string, string> _headers;
        string _body;

        public SessionEventArgs Session
        {
            get=> _session;
            set=> SetProperty(ref _session, value);
        }
        public SemaphoreSlim Semaphore
        {
            get=> _semaphore;
            set=> SetProperty(ref _semaphore, value);
        }
        public int? Status
        {
            get=> _status;
            set=> SetProperty(ref _status, value);
        }

        public string StatusDescription
        {
            get=> _statusDescription;
            set=> SetProperty(ref _statusDescription, value);
        }

        public Dictionary<string, string> Headers
        {
            get=> _headers;
            set=> SetProperty(ref _headers, value);
        }

        public string Body
        {
            get=> _body;
            set=> SetProperty(ref _body, value);
        }

        public SessionInfo(SessionEventArgs session)
        {
            _session = session;
            _semaphore = new SemaphoreSlim(0,1);
        }



    }
}