using System.Windows;
using HttpProxyWpfClient.code.Loc;
using HttpProxyWpfClient.code.net;

namespace HttpProxyWpfClient;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private static readonly string[] SupportedLanguages = { "zh-CN", "en-US", "ja-JP", "ko-KR" };

    protected override void OnStartup(StartupEventArgs e)
    {
        // 在窗口创建前恢复上次使用的界面语言，避免先以默认中文渲染再跳变
        AppConfig config = ConfigService.Load();
        if (SupportedLanguages.Contains(config.Language))
        {
            try
            {
                LocalizationManager.SetLanguage(config.Language);
            }
            catch
            {
                // 语言字典加载失败时保持默认中文，不阻断启动
            }
        }

        base.OnStartup(e);
    }
}
