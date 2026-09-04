using System.Globalization;
using System.Windows;

namespace HttpProxyWpfClient.code.Loc
{
    public static class LocalizationManager
    {
        /// <summary>
        /// 按当前语言取字符串资源，key 缺失时返回 key 本身（便于在界面上发现遗漏词条）
        /// </summary>
        public static string GetString(string key) =>
            Application.Current?.TryFindResource(key) as string ?? key;

        public static event Action LanguageChanged;

        public static void SetLanguage(string cultureName)
        {
            var dict = new ResourceDictionary
            {
                Source = new Uri($"/Resources/String.{cultureName}.xaml", UriKind.Relative)
            };

            var oldDict = Application.Current.Resources.MergedDictionaries
                .FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("String."));
            if (oldDict != null)
                Application.Current.Resources.MergedDictionaries.Remove(oldDict);

            Application.Current.Resources.MergedDictionaries.Add(dict);

            Thread.CurrentThread.CurrentCulture = new CultureInfo(cultureName);
            Thread.CurrentThread.CurrentUICulture = new CultureInfo(cultureName);

            LanguageChanged?.Invoke();
        }
    }
}