using net8._0_ProxyWPF.code.@base;
using Titanium.Web.Proxy.Http;
using Titanium.Web.Proxy.Models;

namespace net8._0_ProxyWPF.code.net.entity
{
    public class RequestMatch : BindableBase
    {
        string _name;
        bool _all;
        string _url;
        string _method;
        Dictionary<string, string> _headers;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }
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

            // URL 部分匹配
            if (url != null && !request.RequestUri.AbsoluteUri.Contains(url))
                return false;

            // 方法匹配（忽略大小写）
            if (method != null && !request.Method.Equals(method, StringComparison.OrdinalIgnoreCase))
                return false;

            // 请求头匹配
            if (headers != null && headers.Count > 0)
            {
                // 转成忽略大小写的字典，便于查找
                var headersIgnoreCase = new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase);

                List<HttpHeader> httpHeaders = request.Headers.GetAllHeaders();
                foreach (var kv in headersIgnoreCase)
                {
                    var targetHeader = httpHeaders.FirstOrDefault(h =>
                        h.Name.Equals(kv.Key, StringComparison.OrdinalIgnoreCase));

                    if (targetHeader == null) return false; // 缺少 key

                    // 如果要求 value 也要匹配
                    if (kv.Value != null && !targetHeader.Value.Contains(kv.Value))
                        return false;
                }
            }

            return true;
        }

        public RequestMatchVo ToRequestMatchVo()
        {
            RequestMatchVo requestMatchVo = new RequestMatchVo();
            requestMatchVo.Name = Name;
            requestMatchVo.Url = Url;
            requestMatchVo.Method = Method;
            foreach (var header in Headers)
            {
                requestMatchVo.AddHeader(header.Key, header.Value);
            }
            return requestMatchVo;
        }
    }
}