using System.Globalization;
using System.Windows;

namespace net8._0_ProxyWPF.code.Loc
{
    public static class LocalizationManager
    {
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