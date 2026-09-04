using System.Text.RegularExpressions;
using HttpProxyWpfClient.code.@base;
using Titanium.Web.Proxy.Http;
using Titanium.Web.Proxy.Models;

namespace HttpProxyWpfClient.code.net.entity
{
    /// <summary>
    /// 单条请求头匹配规则：key/value 各自独立设置是否使用正则表达式
    /// </summary>
    public class HeaderMatchRule : BindableBase
    {
        string _key;
        string _value;
        bool _keyUseRegex;
        bool _valueUseRegex;

        public string Key
        {
            get => _key;
            set => SetProperty(ref _key, value);
        }
        public string Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }
        public bool KeyUseRegex
        {
            get => _keyUseRegex;
            set => SetProperty(ref _keyUseRegex, value);
        }
        public bool ValueUseRegex
        {
            get => _valueUseRegex;
            set => SetProperty(ref _valueUseRegex, value);
        }

        public HeaderMatchRule()
        {
        }

        public HeaderMatchRule(string key, string value, bool keyUseRegex = false, bool valueUseRegex = false)
        {
            Key = key;
            Value = value;
            KeyUseRegex = keyUseRegex;
            ValueUseRegex = valueUseRegex;
        }
    }

    public class RequestMatch : BindableBase
    {
        string _name;
        bool _enabled = true;
        bool _all;
        string _domain;
        bool _domainUseRegex;
        string _url;
        bool _urlUseRegex;
        string _method;
        List<HeaderMatchRule> _headers = new List<HeaderMatchRule>();
        bool _interceptRequest;
        bool _interceptResponse = true;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        /// <summary>
        /// 规则是否启用，未启用的规则不参与匹配
        /// </summary>
        public bool Enabled
        {
            get => _enabled;
            set => SetProperty(ref _enabled, value);
        }

        public bool All
        {
            get => _all;
            set => SetProperty(ref _all, value);
        }

        /// <summary>
        /// 域名匹配（对应 Request.RequestUri.Host）
        /// </summary>
        public string Domain
        {
            get => _domain;
            set => SetProperty(ref _domain, value);
        }
        public bool DomainUseRegex
        {
            get => _domainUseRegex;
            set => SetProperty(ref _domainUseRegex, value);
        }

        /// <summary>
        /// URL 匹配（对应 Request.RequestUri.AbsoluteUri，允许与域名规则重复匹配）
        /// </summary>
        public string Url
        {
            get => _url;
            set => SetProperty(ref _url, value);
        }
        public bool UrlUseRegex
        {
            get => _urlUseRegex;
            set => SetProperty(ref _urlUseRegex, value);
        }

        /// <summary>
        /// 请求方式匹配（GET/POST 等，忽略大小写、精确匹配）
        /// </summary>
        public string Method
        {
            get => _method;
            set => SetProperty(ref _method, value);
        }

        /// <summary>
        /// 请求头匹配规则集合
        /// </summary>
        public List<HeaderMatchRule> Headers
        {
            get => _headers;
            set => SetProperty(ref _headers, value);
        }

        /// <summary>
        /// 是否在请求阶段（上行）拦截
        /// </summary>
        public bool InterceptRequest
        {
            get => _interceptRequest;
            set => SetProperty(ref _interceptRequest, value);
        }

        /// <summary>
        /// 是否在响应阶段（下行）拦截
        /// </summary>
        public bool InterceptResponse
        {
            get => _interceptResponse;
            set => SetProperty(ref _interceptResponse, value);
        }

        public RequestMatch()
        {
        }

        public RequestMatch(string url, string method, Dictionary<string, string> headers)
        {
            Url = url;
            Method = method;
            if (headers != null)
            {
                foreach (var kv in headers)
                {
                    Headers.Add(new HeaderMatchRule(kv.Key, kv.Value));
                }
            }
        }

        /// <summary>
        /// 按字段的正则开关，用正则或"包含"匹配单个文本
        /// </summary>
        private static bool MatchText(string pattern, string input, bool useRegex)
        {
            if (string.IsNullOrEmpty(pattern)) return true;
            input ??= string.Empty;

            if (useRegex)
            {
                try
                {
                    return Regex.IsMatch(input, pattern);
                }
                catch (ArgumentException)
                {
                    // 正则表达式非法时，视为不匹配，避免抛出异常影响代理转发
                    return false;
                }
            }

            return input.Contains(pattern, StringComparison.OrdinalIgnoreCase);
        }

        public static bool MatchingRules(Request request, RequestMatch requestMatch)
        {
            if (!requestMatch.Enabled) return false;
            if (requestMatch.All) return true;

            // 域名匹配
            if (!MatchText(requestMatch.Domain, request.RequestUri.Host, requestMatch.DomainUseRegex))
                return false;

            // URL 匹配（完整 URL，允许与域名规则重复）
            if (!MatchText(requestMatch.Url, request.RequestUri.AbsoluteUri, requestMatch.UrlUseRegex))
                return false;

            // 方法匹配（忽略大小写、精确匹配）
            if (!string.IsNullOrEmpty(requestMatch.Method) &&
                !request.Method.Equals(requestMatch.Method, StringComparison.OrdinalIgnoreCase))
                return false;

            // 请求头匹配
            if (requestMatch.Headers != null && requestMatch.Headers.Count > 0)
            {
                List<HttpHeader> httpHeaders = request.Headers.GetAllHeaders();
                foreach (var headerRule in requestMatch.Headers)
                {
                    if (string.IsNullOrEmpty(headerRule.Key)) continue;

                    HttpHeader targetHeader = null;
                    foreach (var h in httpHeaders)
                    {
                        if (MatchText(headerRule.Key, h.Name, headerRule.KeyUseRegex))
                        {
                            targetHeader = h;
                            break;
                        }
                    }

                    if (targetHeader == null) return false; // 缺少匹配的 key

                    if (!MatchText(headerRule.Value, targetHeader.Value, headerRule.ValueUseRegex))
                        return false;
                }
            }

            return true;
        }

        public RequestMatchVo ToRequestMatchVo()
        {
            RequestMatchVo requestMatchVo = new RequestMatchVo();
            requestMatchVo.Name = Name;
            requestMatchVo.Enabled = Enabled;
            requestMatchVo.Domain = Domain;
            requestMatchVo.DomainUseRegex = DomainUseRegex;
            requestMatchVo.Url = Url;
            requestMatchVo.UrlUseRegex = UrlUseRegex;
            requestMatchVo.Method = Method;
            requestMatchVo.InterceptRequest = InterceptRequest;
            requestMatchVo.InterceptResponse = InterceptResponse;
            foreach (var header in Headers)
            {
                requestMatchVo.AddHeader(header.Key, header.Value, header.KeyUseRegex, header.ValueUseRegex);
            }
            return requestMatchVo;
        }
    }
}
