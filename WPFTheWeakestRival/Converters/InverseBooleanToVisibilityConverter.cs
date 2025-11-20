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
            var isTrue = value is bool booleanValue && booleanValue;

            return isTrue
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                return visibility != Visibility.Visible;
            }

            return true;
        }
    }
}
