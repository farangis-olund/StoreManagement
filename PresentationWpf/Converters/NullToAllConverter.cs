
using System.Globalization;
using System.Windows.Data;

namespace PresentationWpf.Converters;

public class NullToAllConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value ?? "Все";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value; // DO NOT TOUCH
}