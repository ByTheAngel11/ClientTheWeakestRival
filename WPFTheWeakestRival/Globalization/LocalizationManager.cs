using System;
using System.ComponentModel;
using System.Globalization;
using System.Threading;

namespace WPFTheWeakestRival.Globalization
{
    public sealed class LocalizationManager : INotifyPropertyChanged
    {
        private static readonly LocalizationManager current = new LocalizationManager();
        public static LocalizationManager Current => current;

        private CultureInfo culture = new CultureInfo("es");
        public CultureInfo Culture => culture;

        public string this[string key]
        {
            get
            {
                if (string.IsNullOrWhiteSpace(key)) return string.Empty;
                var s = Properties.Langs.Lang.ResourceManager.GetString(key, culture);
                return string.IsNullOrEmpty(s)
                    ? Properties.Langs.Lang.ResourceManager.GetString(key, new CultureInfo("es")) ?? key
                    : s;
            }
        }

        public void SetCulture(string cultureName)
        {
            var ci = new CultureInfo(cultureName);

            if (string.Equals(culture.Name, ci.Name, StringComparison.OrdinalIgnoreCase))
                return;

            culture = ci;

            Thread.CurrentThread.CurrentCulture = ci;
            Thread.CurrentThread.CurrentUICulture = ci;

            Properties.Langs.Lang.Culture = ci;

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
