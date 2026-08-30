using System.Collections.ObjectModel;
using net8._0_ProxyWPF.code.@base;

namespace net8._0_ProxyWPF.code.net.entity;

public class RequestMatchVo : BindableBase
{
    private string _name;
    private bool _enabled = true;
    private string _domain;
    private bool _domainUseRegex;
    private string _url;
    private bool _urlUseRegex;
    private string _method;
    private bool _interceptRequest;
    private bool _interceptResponse = true;
    private ObservableCollection<HeaderVo> _headers;

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public bool Enabled
    {
        get => _enabled;
        set => SetProperty(ref _enabled, value);
    }

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

    public string Method
    {
        get => _method;
        set => SetProperty(ref _method, value);
    }

    public bool InterceptRequest
    {
        get => _interceptRequest;
        set => SetProperty(ref _interceptRequest, value);
    }

    public bool InterceptResponse
    {
        get => _interceptResponse;
        set => SetProperty(ref _interceptResponse, value);
    }

    public ObservableCollection<HeaderVo> Headers
    {
        get => _headers;
        set => SetProperty(ref _headers, value);
    }

    public RequestMatchVo()
    {
        Headers = new ObservableCollection<HeaderVo>();
    }

    public void AddHeader(string key, string value, bool keyUseRegex = false, bool valueUseRegex = false)
    {
        Headers.Add(new HeaderVo()
        {
            Key = key,
            Value = value,
            KeyUseRegex = keyUseRegex,
            ValueUseRegex = valueUseRegex,
            RequestMatchVo = this
        });
    }

    public void RemoveHeader(HeaderVo header)
    {
        Headers.Remove(header);
        header.RequestMatchVo = null;
    }

    public void ClearHeaders()
    {
        foreach (var header in Headers)
        {
            header.RequestMatchVo = null;
        }
        Headers.Clear();
    }

    public RequestMatch ToRequestMatch()
    {
        var requestMatch = new RequestMatch
        {
            Name = Name,
            Enabled = Enabled,
            Domain = Domain,
            DomainUseRegex = DomainUseRegex,
            Url = Url,
            UrlUseRegex = UrlUseRegex,
            Method = Method,
            InterceptRequest = InterceptRequest,
            InterceptResponse = InterceptResponse
        };
        foreach (var header in Headers)
        {
            requestMatch.Headers.Add(new HeaderMatchRule(header.Key, header.Value, header.KeyUseRegex, header.ValueUseRegex));
        }
        return requestMatch;
    }
}

public class HeaderVo : BindableBase
{
    private string _key;
    private string _value;
    private bool _keyUseRegex;
    private bool _valueUseRegex;
    private RequestMatchVo _requestMatchVo;

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

    public RequestMatchVo RequestMatchVo
    {
        get => _requestMatchVo;
        set => SetProperty(ref _requestMatchVo, value);
    }
}
