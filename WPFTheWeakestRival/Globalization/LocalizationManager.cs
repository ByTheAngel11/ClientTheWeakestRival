using System;
using System.ComponentModel;
using System.Globalization;
using System.Threading;

namespace WPFTheWeakestRival.Globalization
{
    public sealed class LocalizationManager : INotifyPropertyChanged
    {
        private static readonly LocalizationManager _current = new LocalizationManager();
        public static LocalizationManager Current => _current;

        private CultureInfo _culture = new CultureInfo("es");
        public CultureInfo Culture => _culture;

        // Indexador: permite usar Binding a claves: [Key]
        public string this[string key]
        {
            get
            {
                if (string.IsNullOrWhiteSpace(key)) return string.Empty;
                var s = Properties.Langs.Lang.ResourceManager.GetString(key, _culture);
                return string.IsNullOrEmpty(s)
                    ? Properties.Langs.Lang.ResourceManager.GetString(key, new CultureInfo("es")) ?? key
                    : s;
            }
        }

        public void SetCulture(string cultureName)
        {
            var ci = new CultureInfo(cultureName);

            if (string.Equals(_culture.Name, ci.Name, StringComparison.OrdinalIgnoreCase))
                return;

            _culture = ci;

            // Sincroniza cultura de hilo (opcional)
            Thread.CurrentThread.CurrentCulture = ci;
            Thread.CurrentThread.CurrentUICulture = ci;

            // MUY IMPORTANTE: también mueve la cultura del .resx tipado (para MessageBox, etc.)
            Properties.Langs.Lang.Culture = ci;

            // Notifica a todos los bindings que usan el indexador
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
