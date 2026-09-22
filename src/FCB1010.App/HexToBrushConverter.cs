using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace FCB1010.App;

public sealed class HexToBrushConverter : IValueConverter
{
    public static readonly HexToBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string hex || string.IsNullOrWhiteSpace(hex))
            return Brushes.Gray;
        try { return SolidColorBrush.Parse(hex); }
        catch { return Brushes.Gray; }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
