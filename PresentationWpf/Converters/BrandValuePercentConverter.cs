using System.Globalization;
using System.Windows.Data;

namespace PresentationWpf.Converters;

public class BrandValuePercentConverter : IMultiValueConverter
{
    public object Convert(
        object[] values,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        if (values.Length < 3)
            return "";

        decimal value = values[0] is decimal v ? v : 0;
        bool isTotalRow = values[1] is bool b && b;
        decimal grandTotal = values[2] is decimal t ? t : 0;

        if (!isTotalRow)
            return value.ToString("N2");

        if (grandTotal == 0)
            return $"{value:N2}\n0.00%";

        decimal percent = value / grandTotal * 100;

        return $"{value:N2}\n{percent:N2}%";
    }

    public object[] ConvertBack(
        object value,
        Type[] targetTypes,
        object parameter,
        CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}