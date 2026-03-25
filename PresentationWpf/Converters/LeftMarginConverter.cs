
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PresentationWpf.Converters;

public class LeftMarginConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double left = (double)value;
        return new Thickness(left + 10, 0, 0, 0);  // +10 for padding
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return null!;
    }
}
