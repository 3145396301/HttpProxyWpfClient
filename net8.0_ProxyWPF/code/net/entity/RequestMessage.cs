using System.ComponentModel;
using System.Text;
using net8._0_ProxyWPF.code.@base;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Http;
using Titanium.Web.Proxy.Models;

namespace net8._0_ProxyWPF.code.net.entity
{
    public class RequestMessage  : BindableBase
    {
        private string _reqRow;
        private string _reqHeaders;
        private string _reqBody;

        public string ReqRow
        {
            get => _reqRow;
            set => SetProperty(ref _reqRow, value);
        }
        public string ReqHeaders
        {
            get => _reqHeaders;
            set => SetProperty(ref _reqHeaders, value);
        }
        public string ReqBody
        {
            get => _reqBody;
            set => SetProperty(ref _reqBody, value);
        }

        public string AllMessage
        {
            get{ return $"{ReqRow}{ReqBody??""}"; }
            set
            {

            }
        }

        public RequestMessage(SessionEventArgs session):this(session.HttpClient.Request)
        {
        }

        public  RequestMessage (Request req)
        {
            ReqRow = req.HeaderText;
            HeaderCollection headerCollection = req.Headers;
            foreach (HttpHeader httpHeader in headerCollection)
            {
                ReqHeaders += $"{httpHeader.Name}:{httpHeader.Value}\r\n";
            }
            if (!req.HasBody)
            {
                ReqBody = "";
                return;
            }
            ReqBody = Encoding.UTF8.GetString(req.Body);
        }


    }
}