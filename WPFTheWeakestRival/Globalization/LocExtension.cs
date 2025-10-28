using System.Windows.Data;
using System.Windows.Markup;

namespace WPFTheWeakestRival.Globalization
{
    [MarkupExtensionReturnType(typeof(string))]
    public sealed class LocExtension : MarkupExtension
    {
        public string Key { get; set; }
        public LocExtension() { }
        public LocExtension(string key) => Key = key;

        public override object ProvideValue(System.IServiceProvider serviceProvider)
        {
            var binding = new Binding($"[{Key}]")
            {
                Source = LocalizationManager.Current,
                Mode = BindingMode.OneWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };
            return binding.ProvideValue(serviceProvider);
        }
    }
}
