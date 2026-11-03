using System.Globalization;
using System.Windows.Data;

namespace PresentationWpf.Converters;

public sealed class NullableDecimalConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is decimal amount
            ? amount.ToString("0.##", CultureInfo.InvariantCulture)
            : string.Empty;
    }

    public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var text = value?.ToString();
        if (string.IsNullOrWhiteSpace(text))
            return null;

        text = text.Trim();

        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var invariantResult))
            return invariantResult;

        if (decimal.TryParse(text, NumberStyles.Number, culture, out var cultureResult))
            return cultureResult;

        var normalized = text.Replace(',', '.');
        if (decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var normalizedResult))
            return normalizedResult;

        return Binding.DoNothing;
    }
}
