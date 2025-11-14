using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WPFTheWeakestRival.Converters
{
    [ValueConversion(typeof(bool), typeof(Visibility))]
    public sealed class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var b = value is bool v && v;
            return b ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility v) return v != Visibility.Visible;
            return true;
        }
    }
}
