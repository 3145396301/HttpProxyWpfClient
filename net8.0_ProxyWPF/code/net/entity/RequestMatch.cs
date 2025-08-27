using net8._0_ProxyWPF.code.@base;
using Titanium.Web.Proxy.Http;
using Titanium.Web.Proxy.Models;

namespace net8._0_ProxyWPF.code.net.entity
{
    public class RequestMatch : BindableBase
    {
        bool _all;
        string _url;
        string _method;
        Dictionary<string, string> _headers;

        public bool All
        {
            get => _all;
            set => SetProperty(ref _all, value);
        }
        public string Url
        {
            get => _url;
            set => SetProperty(ref _url, value);
        }
        public string Method
        {
            get => _method;
            set => SetProperty(ref _method, value);
        }
        public Dictionary<string, string> Headers
        {
            get => _headers;
            set => SetProperty(ref _headers, value);
        }

        public RequestMatch()
        {

        }
        public RequestMatch(string url, string method, Dictionary<string, string> headers)
        {
            Url = url;
            Method = method;
            Headers = headers;
        }

        public static bool MatchingRules(Request request, RequestMatch requestMatch)
        {
            if (requestMatch.All) return true;
            string url = requestMatch.Url;
            string method = requestMatch.Method;
            Dictionary<string, string> headers = requestMatch.Headers;
            // TODO: 匹配规则 匹配请求中的URL、方法、请求头、请求体  等，全部匹配才返回true
            if (url != null && !request.RequestUri.AbsoluteUri.Contains(url))
                return false;
            if (method != null && !request.Method.Equals(method))
                return false;
            // headers 如果 val = null 则只匹配 key
            List<HttpHeader> httpHeaders = request.Headers.GetAllHeaders();
            foreach (var header in httpHeaders)
            {
                if (headers != null && headers.ContainsKey(header.Name))
                {
                    if (headers[header.Name] != null && !header.Value.Contains(headers[header.Name]))
                        return false;
                }
            }
            return true;
        }
    }
}