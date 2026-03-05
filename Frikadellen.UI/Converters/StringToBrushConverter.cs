using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Frikadellen.UI.Converters;

public class StringToBrushConverter : IValueConverter
{
    public static readonly StringToBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s && !string.IsNullOrEmpty(s))
        {
            try { return new SolidColorBrush(Color.Parse(s)); }
            catch { /* invalid color string — fall through to transparent */ }
        }
        return Brushes.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
