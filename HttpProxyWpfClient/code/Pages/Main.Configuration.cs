using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Data;
using HttpProxyWpfClient.code.net;

namespace HttpProxyWpfClient.code.Pages;

public partial class Main
{
    public void SaveConfig()
    {
        var config = new AppConfig
        {
            LocalProxyHost = proxyConnect.ProxyHost,
            LocalProxyPort = proxyConnect.ProxyPort,
            UpstreamEnabled = proxyConnect.UpstreamEnabled,
            UpstreamHost = proxyConnect.UpstreamIp,
            UpstreamPort = proxyConnect.UpstreamPort == -1 ? null : proxyConnect.UpstreamPort,
            UpstreamUser = proxyConnect.UpstreamUser,
            UpstreamPass = proxyConnect.UpstreamPass,
            RequestContentFontSize = _requestContentFontSize,
            ResponseContentFontSize = _responseContentFontSize,
            EditBodyFontSize = _editBodyFontSize,
            Groups = Groups.ToList()
        };
        SaveSessionColumnLayout(config);
        ConfigService.Save(config);
    }

    private void LoadSessionColumnLayout(AppConfig config)
    {
        foreach (var column in _sessionColumns)
        {
            var setting = config.SessionColumns.FirstOrDefault(item => item.Key == column.Key);
            if (setting == null) continue;
            column.Visible = setting.Visible;
            if (setting.Width > 0 && !double.IsNaN(setting.Width))
                column.Column.Width = setting.Width;
        }
        ApplySessionColumnLayout();
    }

    private void ApplySessionColumnLayout()
    {
        if (SessionListView.View is not GridView gridView) return;
        gridView.Columns.Clear();
        foreach (var column in _sessionColumns)
        {
            if (column.Visible) gridView.Columns.Add(column.Column);
        }
    }

    private void SaveSessionColumnLayout(AppConfig config)
    {
        config.SessionColumns.Clear();
        foreach (var column in _sessionColumns)
        {
            double width = double.IsNaN(column.Column.Width) ? column.DefaultWidth : column.Column.Width;
            config.SessionColumns.Add(new SessionColumnSetting
            {
                Key = column.Key,
                Visible = column.Visible,
                Width = width
            });
        }
    }

    private void ApplyRequestFontSize(double fontSize)
    {
        _requestContentFontSize = Math.Clamp(fontSize, ContentFontSizeMin, ContentFontSizeMax);
        RequestEditor.FontSize = _requestContentFontSize;
    }

    private void ApplyResponseFontSize(double fontSize)
    {
        _responseContentFontSize = Math.Clamp(fontSize, ContentFontSizeMin, ContentFontSizeMax);
        ResponseEditor.FontSize = _responseContentFontSize;
    }

    private void ContentEditor_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control) return;
        e.Handled = true;

        if (sender == RequestEditor)
        {
            ApplyRequestFontSize(_requestContentFontSize + (e.Delta > 0 ? 1 : -1));
        }
        else if (sender == ResponseEditor)
        {
            ApplyResponseFontSize(_responseContentFontSize + (e.Delta > 0 ? 1 : -1));
        }
        else return;

        SaveConfig();
    }

    private void EditBodyTextBox_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control) return;
        e.Handled = true;
        double newSize = _editBodyFontSize + (e.Delta > 0 ? 1 : -1);
        if (newSize < ContentFontSizeMin || newSize > ContentFontSizeMax) return;
        _editBodyFontSize = newSize;
        ((TextBox)sender).FontSize = newSize;
        SaveConfig();
    }
}
