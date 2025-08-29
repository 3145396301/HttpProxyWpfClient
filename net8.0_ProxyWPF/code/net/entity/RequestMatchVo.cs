using System.Collections.ObjectModel;
using net8._0_ProxyWPF.code.@base;

namespace net8._0_ProxyWPF.code.net.entity;

public class RequestMatchVo :BindableBase
{
    private string _naem;
    private string _url;
    private string _method;
    private ObservableCollection<HeaderVo> _headers;

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

    public ObservableCollection<HeaderVo> Headers
    {
        get => _headers;
        set => SetProperty(ref _headers, value);
    }

    public string Name
    {
        get => _naem;
        set => SetProperty(ref _naem, value);
    }

    public RequestMatchVo()
    {
        Headers = new ObservableCollection<HeaderVo>();
    }

}

public class HeaderVo:BindableBase
{
    private string _name;
    private string _value;

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }
    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }

}