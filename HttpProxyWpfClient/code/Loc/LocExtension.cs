using System.ComponentModel;
using System.Windows;
using System.Windows.Markup;

namespace HttpProxyWpfClient.code.Loc
{
    public class LocBinding : INotifyPropertyChanged
    {
        public static LocBinding Instance { get; } = new LocBinding();

        public string this[string key] =>
            Application.Current.TryFindResource(key)?.ToString() ?? key;

        public event PropertyChangedEventHandler PropertyChanged;

        public void Refresh()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
        }
    }

    [MarkupExtensionReturnType(typeof(string))]
    public class LocExtension : MarkupExtension
    {
        public string Key { get; set; }
        public LocExtension(string key) => Key = key;

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            // 绑定到 LocBinding 的索引器
            var binding = new System.Windows.Data.Binding($"[{Key}]")
            {
                Source = LocBinding.Instance,
                Mode = System.Windows.Data.BindingMode.OneWay
            };
            return binding.ProvideValue(serviceProvider);
        }

        static LocExtension()
        {
            LocalizationManager.LanguageChanged += () => LocBinding.Instance.Refresh();
        }
    }
}