using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace PresentationWpf.Converters;

public class PermissionToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // parameter = required permission key
        string? requiredKey = parameter as string;

        if (requiredKey == null)
            return Visibility.Collapsed;

        // value = list of current permissions (IEnumerable<string>)
        var permissions = value as IEnumerable<string>;
        if (permissions == null)
            return Visibility.Collapsed;

        // check if permission exists
        return permissions.Contains(requiredKey)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
